using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace Clockbuster
{
    public partial class MainForm : Form
    {
        private DateTime startTime;
        private System.Windows.Forms.Timer displayTimer;
        private System.Windows.Forms.Timer statusTimer;
        private bool isTracking = false;
        /* Database path - set in constructor to use AppData folder */
        private string dbPath;

        public MainForm()
        {
            InitializeComponent();

            /* Don't initialize database or device name yet - wait for first clock in */
            dbPath = null;

            displayTimer = new System.Windows.Forms.Timer();
            displayTimer.Interval = 1000;
            displayTimer.Tick += DisplayTimer_Tick;

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 3000;
            statusTimer.Tick += StatusTimer_Tick;

            this.FormClosing += Form1_FormClosing;
        }

        /* Get or prompt for a unique device identifier */
        private string GetSafeDeviceName()
        {
            /* Check if user has already named this device */
            if (!string.IsNullOrEmpty(Properties.Settings.Default.DeviceId))
            {
                return Properties.Settings.Default.DeviceId;
            }

            /* First run - suggest a default name */
            string suggested = $"{Environment.UserName}-{Environment.MachineName}".ToLower();
            string machineLower = Environment.MachineName.ToLower();
            if (machineLower == "laptop" || machineLower == "desktop" || machineLower == "pc")
            {
                suggested = $"{Environment.UserName}-{machineLower}";
            }

            /* Clean the suggestion */
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                suggested = suggested.Replace(c.ToString(), "");
            }

            if (suggested.Length > 30)
                suggested = suggested.Substring(0, 30);

            /* Prompt user to name their device */
            string deviceName = PromptForDeviceName(suggested);

            /* Save it for future runs */
            Properties.Settings.Default.DeviceId = deviceName;
            Properties.Settings.Default.Save();

            return deviceName;
        }
        /* Show dialog asking user to name their device */
        private string PromptForDeviceName(string defaultName)
        {
            Form prompt = new Form()
            {
                Width = 450,
                Height = 200,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = "Name This Device",
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false
            };

            Label introLabel = new Label()
            {
                Left = 20,
                Top = 20,
                Width = 400,
                Height = 40,
                Text = "This app creates separate database files for each device.\nPlease give this device a unique name:"
            };

            Label nameLabel = new Label()
            {
                Left = 20,
                Top = 70,
                Width = 100,
                Text = "Device name:"
            };

            TextBox textBox = new TextBox()
            {
                Left = 20,
                Top = 95,
                Width = 400,
                Text = defaultName
            };

            Label exampleLabel = new Label()
            {
                Left = 20,
                Top = 120,
                Width = 400,
                Height = 20,
                Text = "Examples: work-laptop, home-pc, johns-desktop",
                ForeColor = System.Drawing.Color.Gray,
                Font = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Italic)
            };

            Button confirmation = new Button()
            {
                Text = "OK",
                Left = 330,
                Width = 90,
                Top = 145,
                DialogResult = DialogResult.OK
            };

            confirmation.Click += (sender, e) => { prompt.Close(); };

            prompt.Controls.Add(introLabel);
            prompt.Controls.Add(nameLabel);
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(exampleLabel);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            /* Select all text so user can easily replace it */
            textBox.SelectAll();
            textBox.Focus();

            if (prompt.ShowDialog() == DialogResult.OK)
            {
                string name = textBox.Text.Trim().ToLower();

                /* Clean the input */
                char[] invalidChars = Path.GetInvalidFileNameChars();
                foreach (char c in invalidChars)
                {
                    name = name.Replace(c.ToString(), "");
                }

                /* If user cleared it or entered invalid characters only, use default */
                if (string.IsNullOrWhiteSpace(name))
                    name = defaultName;

                if (name.Length > 30)
                    name = name.Substring(0, 30);

                return name;
            }

            /* User closed dialog - use default */
            return defaultName;
        }
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            Label lblStatus = (Label)this.Controls["lblStatus"];
            lblStatus.Text = "";
            statusTimer.Stop();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isTracking)
            {
                DialogResult result = MessageBox.Show(
                    "WARNING: You haven't clocked out. Do you still wish to exit?",
                    "Unsaved Session",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        /* Allow user to change their device name */
        private void RenameDevice_Click(object sender, EventArgs e)
        {
            if (isTracking)
            {
                MessageBox.Show("Please clock out before renaming device.",
                    "Active Session", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string currentName = Properties.Settings.Default.DeviceId;
            if (string.IsNullOrEmpty(currentName))
                currentName = GetSafeDeviceName();

            string newName = PromptForDeviceName(currentName);

            /* If name didn't change, do nothing */
            if (newName == currentName)
            {
                ShowStatus("Device name unchanged.", System.Drawing.Color.Orange);
                return;
            }

            /* Save new device name */
            Properties.Settings.Default.DeviceId = newName;
            Properties.Settings.Default.Save();

            /* Ask if user wants to rename the database file too */
            DialogResult rename = MessageBox.Show(
                $"Device renamed to: {newName}\n\n" +
                $"Would you like to rename your database file to match?\n\n" +
                $"Current: {Path.GetFileName(dbPath)}\n" +
                $"New: clockbuster_{newName}.db",
                "Rename Database File?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (rename == DialogResult.Yes)
            {
                try
                {
                    string currentDir = Path.GetDirectoryName(dbPath);
                    string newPath = Path.Combine(currentDir, $"clockbuster_{newName}.db");

                    /* Check if target file already exists */
                    if (File.Exists(newPath))
                    {
                        ShowStatus("A database with that name already exists!", System.Drawing.Color.Red);
                        return;
                    }

                    /* Rename the file */
                    File.Move(dbPath, newPath);

                    /* Update our path reference */
                    dbPath = newPath;
                    Properties.Settings.Default.DatabasePath = dbPath;
                    Properties.Settings.Default.Save();

                    ShowStatus($"Device and database renamed to: {newName}", System.Drawing.Color.Green);
                }
                catch (Exception ex)
                {
                    ShowStatus($"File rename failed: {ex.Message}", System.Drawing.Color.Red);
                }
            }
            else
            {
                ShowStatus($"Device name updated to: {newName}", System.Drawing.Color.Green);
            }
        }

        private void InitializeComponent()
        {
            /* Main window configuration */
            this.Text = "Clockbuster - Time Tracker";
            this.Width = 450;
            this.Height = 310;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;



            MenuStrip menuStrip = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");

            ToolStripMenuItem dataMenu = new ToolStripMenuItem("Data");

            ToolStripMenuItem backupItem = new ToolStripMenuItem("Backup Database");
            backupItem.Click += BtnBackup_Click;

            ToolStripMenuItem locateItem = new ToolStripMenuItem("Locate in Explorer");
            locateItem.Click += LocateDb_Click;

            ToolStripMenuItem exportCsvItem = new ToolStripMenuItem("Export to CSV");
            exportCsvItem.Click += ExportToCsv_Click;

            ToolStripMenuItem mergeDataItem = new ToolStripMenuItem("Merge Data");
            mergeDataItem.Click += MergeData_Click;

            dataMenu.DropDownItems.Add(backupItem);
            dataMenu.DropDownItems.Add(locateItem);
            dataMenu.DropDownItems.Add(new ToolStripSeparator());
            dataMenu.DropDownItems.Add(exportCsvItem);
            dataMenu.DropDownItems.Add(mergeDataItem);
            fileMenu.DropDownItems.Add(dataMenu);
            menuStrip.Items.Add(fileMenu);

            ToolStripMenuItem viewMenu = new ToolStripMenuItem("View");

            ToolStripMenuItem timeclockDataItem = new ToolStripMenuItem("Timeclock Data");
            timeclockDataItem.Click += ViewTimeclockData_Click;

            viewMenu.DropDownItems.Add(timeclockDataItem);
            menuStrip.Items.Add(viewMenu);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            Label lblActivity = new Label
            {
                Text = "Activity:",
                Left = 20,
                Top = 50,
                Width = 80
            };
            /* Text input box where user types the activity name they're tracking */
            TextBox txtActivity = new TextBox
            {
                Name = "txtActivity",
                Left = 110,
                Top = 50,
                Width = 300
            };

            Button btnClockIn = new Button
            {
                Name = "btnClockIn",
                Text = "Clock In",
                Left = 20,
                Top = 90,
                Width = 100
            };
            btnClockIn.Click += BtnClockIn_Click;

            Button btnClockOut = new Button
            {
                Name = "btnClockOut",
                Text = "Clock Out",
                Left = 130,
                Top = 90,
                Width = 100,
                Enabled = false
            };
            btnClockOut.Click += BtnClockOut_Click;

            Label lblElapsed = new Label
            {
                Name = "lblElapsed",
                Text = "00:00",
                Left = 20,
                Top = 130,
                Width = 390,
                Height = 40,
                Font = new System.Drawing.Font("Arial", 24, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            /* Status message label - shows feedback to user, wraps text to prevent cutoff */
            Label lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "",
                Left = 20,
                Top = 180,
                Width = 390,
                Height = 50,
                AutoSize = false,
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Regular),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                ForeColor = System.Drawing.Color.Green
            };

            this.Controls.Add(lblActivity);
            this.Controls.Add(txtActivity);
            this.Controls.Add(btnClockIn);
            this.Controls.Add(btnClockOut);
            this.Controls.Add(lblElapsed);
            this.Controls.Add(lblStatus);


            ToolStripMenuItem changeLocationItem = new ToolStripMenuItem("Change Database Location");
            changeLocationItem.Click += ChangeDbLocation_Click;
            dataMenu.DropDownItems.Add(changeLocationItem);

            ToolStripMenuItem renameDeviceItem = new ToolStripMenuItem("Rename This Device");
            renameDeviceItem.Click += RenameDevice_Click;
            dataMenu.DropDownItems.Add(renameDeviceItem);
        }

        private void ChangeDbLocation_Click(object sender, EventArgs e)
        {
            if (isTracking)
            {
                MessageBox.Show("Please clock out before changing database location.",
                    "Active Session", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Choose Database Location for This Device";
                sfd.Filter = "Database files (*.db)|*.db";

                /* Suggest device-specific filename */
                string deviceName = GetSafeDeviceName();
                sfd.FileName = $"clockbuster_{deviceName}.db";
                sfd.CheckFileExists = false;

                /* Try to find Google Drive folder */
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

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string newDbPath = sfd.FileName;

                    /* Check if we're trying to use the same database we're already using */
                    if (Path.GetFullPath(newDbPath) == Path.GetFullPath(dbPath))
                    {
                        ShowStatus("Already using this database location.", System.Drawing.Color.Orange);
                        return;
                    }

                    bool targetExists = File.Exists(newDbPath);
                    bool currentExists = File.Exists(dbPath);

                    /* If target exists, ask what to do */
                    if (targetExists)
                    {
                        DialogResult result = MessageBox.Show(
                            $"The file '{Path.GetFileName(newDbPath)}' already exists.\n\n" +
                            "This might be from a previous session on this PC.\n\n" +
                            "What would you like to do?\n\n" +
                            "• YES = Keep existing file and add any new data from current session\n" +
                            "• NO = Replace with current session data\n" +
                            "• CANCEL = Choose a different location",
                            "File Exists",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);

                        if (result == DialogResult.Cancel)
                            return;

                        if (result == DialogResult.Yes && currentExists)
                        {
                            /* Merge current into target */
                            try
                            {
                                ShowStatus("Merging data...", System.Drawing.Color.Blue);
                                Application.DoEvents();
                                int merged = MergeDatabaseInto(dbPath, newDbPath);
                                ShowStatus($"Merged {merged} records!", System.Drawing.Color.Green);
                            }
                            catch (Exception ex)
                            {
                                ShowStatus($"Merge failed: {ex.Message}", System.Drawing.Color.Red);
                                return;
                            }
                        }
                        else if (result == DialogResult.No && currentExists)
                        {
                            /* Overwrite with current */
                            try
                            {
                                File.Copy(dbPath, newDbPath, true);
                                ShowStatus("Database copied to new location!", System.Drawing.Color.Green);
                            }
                            catch (Exception ex)
                            {
                                ShowStatus($"Failed to copy: {ex.Message}", System.Drawing.Color.Red);
                                return;
                            }
                        }
                    }
                    else if (currentExists)
                    {
                        /* No conflict - just copy current data to new location */
                        try
                        {
                            File.Copy(dbPath, newDbPath, false);
                            ShowStatus("Database moved to new location!", System.Drawing.Color.Green);
                        }
                        catch (Exception ex)
                        {
                            ShowStatus($"Failed to copy: {ex.Message}", System.Drawing.Color.Red);
                            return;
                        }
                    }

                    /* Update the stored path and save it */
                    dbPath = newDbPath;
                    Properties.Settings.Default.DatabasePath = dbPath;
                    Properties.Settings.Default.Save();

                    /* Ensure the database exists at the new location */
                    InitializeDatabase();

                    ShowStatus($"Now using: {Path.GetFileName(dbPath)}", System.Drawing.Color.Green);
                }
            }
        }

        private string FindGoogleDriveFolder()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            /* Common Google Drive folder names */
            string[] possibleNames = { "Google Drive", "GoogleDrive", "GDrive" };

            foreach (string name in possibleNames)
            {
                string path = Path.Combine(userProfile, name);
                if (Directory.Exists(path))
                    return path;
            }

            return null;
        }
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
                MessageBox.Show($"Database initialization failed: {ex.Message}", "Database Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool EnsureDatabaseExists()
        {
            try
            {
                if (!File.Exists(dbPath))
                {
                    ShowStatus("Database file was deleted or moved. Creating a new database...", System.Drawing.Color.Orange);
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
                            ShowStatus("Database table is missing. Recreating database structure...", System.Drawing.Color.Orange);

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
                ShowStatus($"Database error: {ex.Message}", System.Drawing.Color.Red);
                return false;
            }
        }

        private void ShowStatus(string message, System.Drawing.Color color)
        {
            Label lblStatus = (Label)this.Controls["lblStatus"];
            lblStatus.Text = message;
            lblStatus.ForeColor = color;

            statusTimer.Stop();
            statusTimer.Start();
        }

        private void MergeData_Click(object sender, EventArgs e)
        {
            if (!EnsureDatabaseExists())
                return;

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
                ofd.Title = "Select Database Files to Merge";
                ofd.Multiselect = true;
                ofd.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath));

                if (ofd.ShowDialog() != DialogResult.OK || ofd.FileNames.Length == 0)
                    return;

                try
                {
                    ShowStatus("Merging data...", System.Drawing.Color.Blue);
                    Application.DoEvents();

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
                        ShowStatus($"Merged {totalMerged} records with {errors.Count} error(s).", System.Drawing.Color.Orange);
                        MessageBox.Show(string.Join("\n", errors), "Merge Warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        ShowStatus($"Successfully merged {totalMerged} records!", System.Drawing.Color.Green);
                    }
                }
                catch (Exception ex)
                {
                    ShowStatus($"Merge failed: {ex.Message}", System.Drawing.Color.Red);
                }
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

        private class SessionRecord
        {
            public string ActivityName { get; set; }
            public string StartTime { get; set; }
            public string EndTime { get; set; }
            public double DurationMinutes { get; set; }
        }

        private void ExportToCsv_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
                ofd.Title = "Select Database to Export";
                ofd.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath));

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                string selectedDb = ofd.FileName;

                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    sfd.FileName = $"clockbuster_export_{timestamp}.csv";
                    sfd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    sfd.Title = "Save CSV Export";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    string csvPath = sfd.FileName;

                    try
                    {
                        ShowStatus("Exporting data to CSV...", System.Drawing.Color.Blue);
                        Application.DoEvents();

                        int recordCount = ExportDatabaseToCsv(selectedDb, csvPath);

                        ShowStatus($"Completed! Exported {recordCount} records.", System.Drawing.Color.Green);
                    }
                    catch (Exception ex)
                    {
                        ShowStatus($"Export failed: {ex.Message}", System.Drawing.Color.Red);
                    }
                }
            }
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

        private void LocateDb_Click(object sender, EventArgs e)
        {
            try
            {
                string fullPath = Path.GetFullPath(dbPath);

                if (!File.Exists(fullPath))
                {
                    ShowStatus("Database file not found yet (it will be created on first launch).", System.Drawing.Color.Orange);
                    return;
                }

                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
            }
            catch (Exception ex)
            {
                ShowStatus($"Unable to open explorer: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private void ViewTimeclockData_Click(object sender, EventArgs e)
        {
            if (!EnsureDatabaseExists())
                return;

            TimeclockViewerForm viewerForm = new TimeclockViewerForm(dbPath);
            viewerForm.ShowDialog();
        }

        private void BtnClockIn_Click(object sender, EventArgs e)
        {
            TextBox txtActivity = (TextBox)this.Controls["txtActivity"];
            string activityText = txtActivity.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(activityText))
            {
                ShowStatus("Please enter an activity name.", System.Drawing.Color.Red);
                return;
            }

            /* Check if this is first time use - database not set up yet */
            if (string.IsNullOrEmpty(dbPath))
            {
                /* Prompt for device name */
                string deviceName = GetSafeDeviceName();

                /* Prompt for storage location */
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Title = "Choose Where to Save Your Clockbuster Data";
                    sfd.Filter = "Database files (*.db)|*.db";
                    sfd.FileName = $"clockbuster_{deviceName}.db";
                    sfd.CheckFileExists = false;

                    /* Try to suggest Google Drive or Documents folder */
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

                    if (sfd.ShowDialog() != DialogResult.OK)
                    {
                        ShowStatus("Clock in cancelled - no storage location selected.", System.Drawing.Color.Orange);
                        return;
                    }

                    dbPath = sfd.FileName;
                    Properties.Settings.Default.DatabasePath = dbPath;
                    Properties.Settings.Default.Save();
                }

                /* Now initialize the database */
                InitializeDatabase();
            }

            txtActivity.Text = activityText;
            startTime = DateTime.Now;
            isTracking = true;
            displayTimer.Start();

            Button btnClockIn = (Button)this.Controls["btnClockIn"];
            Button btnClockOut = (Button)this.Controls["btnClockOut"];
            btnClockIn.Enabled = false;
            btnClockOut.Enabled = true;
            txtActivity.Enabled = false;

            ShowStatus("Tracking started...", System.Drawing.Color.Green);
        }
        private void BtnClockOut_Click(object sender, EventArgs e)
        {
            DateTime endTime = DateTime.Now;
            TimeSpan duration = endTime - startTime;
            double durationMinutes = duration.TotalMinutes;

            TextBox txtActivity = (TextBox)this.Controls["txtActivity"];

            if (!EnsureDatabaseExists())
            {
                ShowStatus("Cannot save session due to database error.", System.Drawing.Color.Red);
                return;
            }

            SaveSession(txtActivity.Text, startTime, endTime, durationMinutes);

            isTracking = false;
            displayTimer.Stop();

            Button btnClockIn = (Button)this.Controls["btnClockIn"];
            Button btnClockOut = (Button)this.Controls["btnClockOut"];
            Label lblElapsed = (Label)this.Controls["lblElapsed"];

            btnClockIn.Enabled = true;
            btnClockOut.Enabled = false;
            txtActivity.Enabled = true;
            txtActivity.Text = "";
            lblElapsed.Text = "00:00";

            ShowStatus($"Session saved! Duration: {(int)duration.TotalMinutes}m {duration.Seconds}s", System.Drawing.Color.Green);
        }

        private void DisplayTimer_Tick(object sender, EventArgs e)
        {
            if (isTracking)
            {
                TimeSpan elapsed = DateTime.Now - startTime;
                Label lblElapsed = (Label)this.Controls["lblElapsed"];
                lblElapsed.Text = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
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
                ShowStatus($"Failed to save session: {ex.Message}", System.Drawing.Color.Red);
            }
        }

        private void BtnBackup_Click(object sender, EventArgs e)
        {
            if (!EnsureDatabaseExists())
                return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                sfd.FileName = $"clockbuster_backup_{timestamp}.db";
                sfd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.Copy(dbPath, sfd.FileName, true);
                        ShowStatus("Backup created successfully!", System.Drawing.Color.Green);
                    }
                    catch (Exception ex)
                    {
                        ShowStatus($"Backup failed: {ex.Message}", System.Drawing.Color.Red);
                    }
                }
            }
        }
    }
}
