using System;
using System.Data.SQLite;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Text;
using ClockbusterWPF.Properties;

namespace ClockbusterWPF
{
    public partial class MainWindow : Window
    {
        private DateTime startTime;
        private DispatcherTimer displayTimer;
        private DispatcherTimer statusTimer;
        private bool isTracking = false;
        private string dbPath;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize timers
            displayTimer = new DispatcherTimer();
            displayTimer.Interval = TimeSpan.FromSeconds(1);
            displayTimer.Tick += DisplayTimer_Tick;

            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(3);
            statusTimer.Tick += StatusTimer_Tick;

            this.Closing += MainWindow_Closing;

            // Check if this is first run - if so, run initial setup
            if (string.IsNullOrEmpty(Properties.Settings.Default.DeviceId) ||
                string.IsNullOrEmpty(Properties.Settings.Default.DatabasePath))
            {
                RunInitialSetup();
            }
            else
            {
                // Load existing settings
                dbPath = Properties.Settings.Default.DatabasePath;
                EnsureDatabaseExists();
            }
        }

        #region Initial Setup

        private void RunInitialSetup()
        {
            var setupWindow = new InitialSetupWindow();
            if (setupWindow.ShowDialog() == true)
            {
                // Save the device name and database path from setup
                Properties.Settings.Default.DeviceId = setupWindow.DeviceName;
                Properties.Settings.Default.DatabasePath = setupWindow.DatabasePath;
                Properties.Settings.Default.Save();

                dbPath = setupWindow.DatabasePath;
                InitializeDatabase();

                ShowStatus("Setup complete! Ready to track time.", Brushes.Green);
            }
            else
            {
                // User cancelled setup - close the app
                MessageBox.Show("Initial setup is required to use Clockbuster.",
                    "Setup Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
            }
        }

        #endregion

        #region Database Operations

        private void InitializeDatabase()
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    SQLiteConnection.CreateFile(dbPath);
                }

                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();
                    string createTable = @"CREATE TABLE IF NOT EXISTS sessions (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        activity_name TEXT NOT NULL,
                        start_time TEXT NOT NULL,
                        end_time TEXT NOT NULL,
                        duration_minutes REAL NOT NULL
                    )";
                    using (var cmd = new SQLiteCommand(createTable, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database initialization failed: {ex.Message}",
                    "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool EnsureDatabaseExists()
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    ShowStatus("Database file was deleted or moved. Creating a new database...", Brushes.Orange);
                    InitializeDatabase();
                    return true;
                }

                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();
                    string checkTable = "SELECT name FROM sqlite_master WHERE type='table' AND name='sessions'";
                    using (var cmd = new SQLiteCommand(checkTable, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result == null)
                        {
                            ShowStatus("Database table is missing. Recreating database structure...", Brushes.Orange);

                            string createTable = @"CREATE TABLE IF NOT EXISTS sessions (
                                id INTEGER PRIMARY KEY AUTOINCREMENT,
                                activity_name TEXT NOT NULL,
                                start_time TEXT NOT NULL,
                                end_time TEXT NOT NULL,
                                duration_minutes REAL NOT NULL
                            )";
                            using (var createCmd = new SQLiteCommand(createTable, conn))
                            {
                                createCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowStatus($"Database error: {ex.Message}", Brushes.Red);
                return false;
            }
        }

        private void SaveSession(string activityName, DateTime start, DateTime end, double durationMinutes)
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();
                    string insert = @"INSERT INTO sessions (activity_name, start_time, end_time, duration_minutes) 
                                      VALUES (@activity, @start, @end, @duration)";
                    using (var cmd = new SQLiteCommand(insert, conn))
                    {
                        cmd.Parameters.AddWithValue("@activity", activityName);
                        cmd.Parameters.AddWithValue("@start", start.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@end", end.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@duration", durationMinutes);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Failed to save session: {ex.Message}", Brushes.Red);
            }
        }

        private int MergeDatabaseInto(string sourceDbPath, string targetDbPath)
        {
            int mergedCount = 0;
            List<SessionRecord> records = new List<SessionRecord>();

            using (var sourceConn = new SQLiteConnection($"Data Source={sourceDbPath};Version=3;"))
            {
                sourceConn.Open();

                string checkTable = "SELECT name FROM sqlite_master WHERE type='table' AND name='sessions'";
                using (var cmd = new SQLiteCommand(checkTable, sourceConn))
                {
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        throw new Exception("Source database does not contain a 'sessions' table.");
                    }
                }

                string query = "SELECT activity_name, start_time, end_time, duration_minutes FROM sessions";
                using (var cmd = new SQLiteCommand(query, sourceConn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(new SessionRecord
                        {
                            ActivityName = reader["activity_name"].ToString(),
                            StartTime = reader["start_time"].ToString(),
                            EndTime = reader["end_time"].ToString(),
                            DurationMinutes = Convert.ToDouble(reader["duration_minutes"])
                        });
                    }
                }
            }

            using (var targetConn = new SQLiteConnection($"Data Source={targetDbPath};Version=3;"))
            {
                targetConn.Open();

                using (var transaction = targetConn.BeginTransaction())
                {
                    try
                    {
                        foreach (var record in records)
                        {
                            string checkExisting = @"SELECT COUNT(*) FROM sessions 
                                                     WHERE activity_name = @activity 
                                                     AND start_time = @start 
                                                     AND end_time = @end";

                            using (var checkCmd = new SQLiteCommand(checkExisting, targetConn))
                            {
                                checkCmd.Parameters.AddWithValue("@activity", record.ActivityName);
                                checkCmd.Parameters.AddWithValue("@start", record.StartTime);
                                checkCmd.Parameters.AddWithValue("@end", record.EndTime);

                                long count = (long)checkCmd.ExecuteScalar();
                                if (count > 0)
                                    continue;
                            }

                            string insert = @"INSERT INTO sessions (activity_name, start_time, end_time, duration_minutes) 
                                            VALUES (@activity, @start, @end, @duration)";

                            using (var insertCmd = new SQLiteCommand(insert, targetConn))
                            {
                                insertCmd.Parameters.AddWithValue("@activity", record.ActivityName);
                                insertCmd.Parameters.AddWithValue("@start", record.StartTime);
                                insertCmd.Parameters.AddWithValue("@end", record.EndTime);
                                insertCmd.Parameters.AddWithValue("@duration", record.DurationMinutes);
                                insertCmd.ExecuteNonQuery();
                                mergedCount++;
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return mergedCount;
        }

        private int ExportDatabaseToCsv(string dbFilePath, string csvFilePath)
        {
            int recordCount = 0;

            using (var conn = new SQLiteConnection($"Data Source={dbFilePath};Version=3;"))
            {
                conn.Open();

                string checkTable = "SELECT name FROM sqlite_master WHERE type='table' AND name='sessions'";
                using (var cmd = new SQLiteCommand(checkTable, conn))
                {
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        throw new Exception("Database does not contain a 'sessions' table.");
                    }
                }

                string query = "SELECT id, activity_name, start_time, end_time, duration_minutes FROM sessions ORDER BY start_time";
                using (var cmd = new SQLiteCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    using (StreamWriter writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
                    {
                        writer.WriteLine("ID,Activity,Start Time,End Time,Duration (minutes)");

                        while (reader.Read())
                        {
                            recordCount++;

                            string id = reader["id"].ToString();
                            string activity = EscapeCsvField(reader["activity_name"].ToString());
                            string startTime = reader["start_time"].ToString();
                            string endTime = reader["end_time"].ToString();
                            string duration = reader["duration_minutes"].ToString();

                            writer.WriteLine($"{id},{activity},{startTime},{endTime},{duration}");
                        }
                    }
                }
            }

            return recordCount;
        }

        private string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }

        private class SessionRecord
        {
            public string ActivityName { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public double DurationMinutes { get; set; }
        }

        #endregion

        #region Clock In/Out Logic

        private void BtnClockIn_Click(object sender, RoutedEventArgs e)
        {
            string activityText = TxtActivity.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(activityText))
            {
                ShowStatus("Please enter an activity name.", Brushes.Red);
                return;
            }

            if (!EnsureDatabaseExists())
            {
                ShowStatus("Cannot start session due to database error.", Brushes.Red);
                return;
            }

            TxtActivity.Text = activityText;
            startTime = DateTime.Now;
            isTracking = true;
            displayTimer.Start();

            BtnClockIn.IsEnabled = false;
            BtnClockOut.IsEnabled = true;
            TxtActivity.IsEnabled = false;

            ShowStatus("Tracking started...", Brushes.Green);
        }

        private void BtnClockOut_Click(object sender, RoutedEventArgs e)
        {
            DateTime endTime = DateTime.Now;
            TimeSpan duration = endTime - startTime;
            double durationMinutes = duration.TotalMinutes;

            if (!EnsureDatabaseExists())
            {
                ShowStatus("Cannot save session due to database error.", Brushes.Red);
                return;
            }

            SaveSession(TxtActivity.Text, startTime, endTime, durationMinutes);

            isTracking = false;
            displayTimer.Stop();

            BtnClockIn.IsEnabled = true;
            BtnClockOut.IsEnabled = false;
            TxtActivity.IsEnabled = true;
            TxtActivity.Text = "";
            LblElapsed.Text = "00:00";

            ShowStatus($"Session saved! Duration: {(int)duration.TotalMinutes}m {duration.Seconds}s", Brushes.Green);
        }

        #endregion

        #region Timers

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            if (isTracking)
            {
                TimeSpan elapsed = DateTime.Now - startTime;
                LblElapsed.Text = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            LblStatus.Text = "";
            statusTimer.Stop();
        }

        #endregion

        #region Menu Handlers

        private void MenuBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureDatabaseExists())
                return;

            SaveFileDialog sfd = new SaveFileDialog();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sfd.FileName = $"clockbuster_backup_{timestamp}.db";
            sfd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    File.Copy(dbPath, sfd.FileName, true);
                    ShowStatus("Backup created successfully!", Brushes.Green);
                }
                catch (Exception ex)
                {
                    ShowStatus($"Backup failed: {ex.Message}", Brushes.Red);
                }
            }
        }

        private void MenuLocate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fullPath = Path.GetFullPath(dbPath);

                if (!File.Exists(fullPath))
                {
                    ShowStatus("Database file not found.", Brushes.Orange);
                    return;
                }

                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
            }
            catch (Exception ex)
            {
                ShowStatus($"Unable to open explorer: {ex.Message}", Brushes.Red);
            }
        }

        private void MenuExportCsv_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
            ofd.Title = "Select Database to Export";
            ofd.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath));

            if (ofd.ShowDialog() != true)
                return;

            string selectedDb = ofd.FileName;

            SaveFileDialog sfd = new SaveFileDialog();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            sfd.FileName = $"clockbuster_export_{timestamp}.csv";
            sfd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
            sfd.Title = "Save CSV Export";

            if (sfd.ShowDialog() != true)
                return;

            string csvPath = sfd.FileName;

            try
            {
                ShowStatus("Exporting data to CSV...", Brushes.Blue);
                int recordCount = ExportDatabaseToCsv(selectedDb, csvPath);
                ShowStatus($"Completed! Exported {recordCount} records.", Brushes.Green);
            }
            catch (Exception ex)
            {
                ShowStatus($"Export failed: {ex.Message}", Brushes.Red);
            }
        }

        private void MenuMerge_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureDatabaseExists())
                return;

            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
            ofd.Title = "Select Database Files to Merge";
            ofd.Multiselect = true;
            ofd.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath));

            if (ofd.ShowDialog() != true || ofd.FileNames.Length == 0)
                return;

            try
            {
                ShowStatus("Merging data...", Brushes.Blue);

                int totalMerged = 0;
                List<string> errors = new List<string>();

                foreach (string sourceDb in ofd.FileNames)
                {
                    if (Path.GetFullPath(sourceDb) == Path.GetFullPath(dbPath))
                    {
                        errors.Add($"Skipped: {Path.GetFileName(sourceDb)} (cannot merge with itself)");
                        continue;
                    }

                    try
                    {
                        int merged = MergeDatabaseInto(sourceDb, dbPath);
                        totalMerged += merged;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error merging {Path.GetFileName(sourceDb)}: {ex.Message}");
                    }
                }

                if (errors.Count > 0)
                {
                    ShowStatus($"Merged {totalMerged} records with {errors.Count} error(s).", Brushes.Orange);
                    MessageBox.Show(string.Join("\n", errors), "Merge Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    ShowStatus($"Successfully merged {totalMerged} records!", Brushes.Green);
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Merge failed: {ex.Message}", Brushes.Red);
            }
        }

        private void MenuChangeLocation_Click(object sender, RoutedEventArgs e)
        {
            if (isTracking)
            {
                MessageBox.Show("Please clock out before changing database location.",
                    "Active Session", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Choose Database Location for This Device";
            sfd.Filter = "Database files (*.db)|*.db";

            string deviceName = Properties.Settings.Default.DeviceId;
            sfd.FileName = $"clockbuster_{deviceName}.db";
            sfd.CheckFileExists = false;

            string gDriveRoot = FindGoogleDriveFolder();
            if (gDriveRoot != null)
            {
                string gDriveDocs = Path.Combine(gDriveRoot, "Documents");
                if (Directory.Exists(gDriveDocs))
                    sfd.InitialDirectory = gDriveDocs;
            }
            else
            {
                sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            if (sfd.ShowDialog() == true)
            {
                string newDbPath = sfd.FileName;

                if (Path.GetFullPath(newDbPath) == Path.GetFullPath(dbPath))
                {
                    ShowStatus("Already using this database location.", Brushes.Orange);
                    return;
                }

                bool targetExists = File.Exists(newDbPath);
                bool currentExists = File.Exists(dbPath);

                if (targetExists)
                {
                    MessageBoxResult result = MessageBox.Show(
                        $"The file '{Path.GetFileName(newDbPath)}' already exists.\n\n" +
                        "This might be from a previous session on this PC.\n\n" +
                        "What would you like to do?\n\n" +
                        "• YES = Keep existing file and add any new data from current session\n" +
                        "• NO = Replace with current session data\n" +
                        "• CANCEL = Choose a different location",
                        "File Exists",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel)
                        return;

                    if (result == MessageBoxResult.Yes && currentExists)
                    {
                        try
                        {
                            ShowStatus("Merging data...", Brushes.Blue);
                            int merged = MergeDatabaseInto(dbPath, newDbPath);
                            ShowStatus($"Merged {merged} records!", Brushes.Green);
                        }
                        catch (Exception ex)
                        {
                            ShowStatus($"Merge failed: {ex.Message}", Brushes.Red);
                            return;
                        }
                    }
                    else if (result == MessageBoxResult.No && currentExists)
                    {
                        try
                        {
                            File.Copy(dbPath, newDbPath, true);
                            ShowStatus("Database copied to new location!", Brushes.Green);
                        }
                        catch (Exception ex)
                        {
                            ShowStatus($"Failed to copy: {ex.Message}", Brushes.Red);
                            return;
                        }
                    }
                }
                else if (currentExists)
                {
                    try
                    {
                        File.Copy(dbPath, newDbPath, false);
                        ShowStatus("Database moved to new location!", Brushes.Green);
                    }
                    catch (Exception ex)
                    {
                        ShowStatus($"Failed to copy: {ex.Message}", Brushes.Red);
                        return;
                    }
                }

                dbPath = newDbPath;
                Properties.Settings.Default.DatabasePath = dbPath;
                Properties.Settings.Default.Save();

                InitializeDatabase();
                ShowStatus($"Now using: {Path.GetFileName(dbPath)}", Brushes.Green);
            }
        }

        private void MenuRenameDevice_Click(object sender, RoutedEventArgs e)
        {
            if (isTracking)
            {
                MessageBox.Show("Please clock out before renaming device.",
                    "Active Session", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string currentName = Properties.Settings.Default.DeviceId;
            var renameWindow = new DeviceRenameWindow(currentName);

            if (renameWindow.ShowDialog() == true)
            {
                string newName = renameWindow.DeviceName;

                if (newName == currentName)
                {
                    ShowStatus("Device name unchanged.", Brushes.Orange);
                    return;
                }

                Properties.Settings.Default.DeviceId = newName;
                Properties.Settings.Default.Save();

                MessageBoxResult rename = MessageBox.Show(
                    $"Device renamed to: {newName}\n\n" +
                    $"Would you like to rename your database file to match?\n\n" +
                    $"Current: {Path.GetFileName(dbPath)}\n" +
                    $"New: clockbuster_{newName}.db",
                    "Rename Database File?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (rename == MessageBoxResult.Yes)
                {
                    try
                    {
                        string currentDir = Path.GetDirectoryName(dbPath);
                        string newPath = Path.Combine(currentDir, $"clockbuster_{newName}.db");

                        if (File.Exists(newPath))
                        {
                            ShowStatus("A database with that name already exists!", Brushes.Red);
                            return;
                        }

                        File.Move(dbPath, newPath);
                        dbPath = newPath;
                        Properties.Settings.Default.DatabasePath = dbPath;
                        Properties.Settings.Default.Save();

                        ShowStatus($"Device and database renamed to: {newName}", Brushes.Green);
                    }
                    catch (Exception ex)
                    {
                        ShowStatus($"File rename failed: {ex.Message}", Brushes.Red);
                    }
                }
                else
                {
                    ShowStatus($"Device name updated to: {newName}", Brushes.Green);
                }
            }
        }

        private void MenuViewData_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureDatabaseExists())
                return;

            // TODO: Create TimeclockViewerWindow
            MessageBox.Show("Timeclock viewer coming soon!", "Feature", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Utility Methods

        private string FindGoogleDriveFolder()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] possibleNames = { "Google Drive", "GoogleDrive", "GDrive" };

            foreach (string name in possibleNames)
            {
                string path = Path.Combine(userProfile, name);
                if (Directory.Exists(path))
                    return path;
            }

            return null;
        }

        private void ShowStatus(string message, Brush color)
        {
            LblStatus.Text = message;
            LblStatus.Foreground = color;

            statusTimer.Stop();
            statusTimer.Start();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isTracking)
            {
                MessageBoxResult result = MessageBox.Show(
                    "WARNING: You haven't clocked out. Do you still wish to exit?",
                    "Unsaved Session",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        #endregion
    }
}