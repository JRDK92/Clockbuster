using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Clockbuster
{
    public partial class MainForm : Form
    {
        private DateTime startTime;
        private System.Windows.Forms.Timer displayTimer;
        private System.Windows.Forms.Timer statusTimer;
        private bool isTracking = false;
        private string dbPath = "clockbuster.db";

        public MainForm()
        {
            InitializeComponent();
            InitializeDatabase();
            displayTimer = new System.Windows.Forms.Timer();
            displayTimer.Interval = 1000;
            displayTimer.Tick += DisplayTimer_Tick;

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 3000; // 3 seconds
            statusTimer.Tick += StatusTimer_Tick;

            this.FormClosing += Form1_FormClosing;
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

        private void InitializeComponent()
        {
            this.Text = "Clockbuster - Time Tracker";
            this.Width = 400;
            this.Height = 310;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. Create the MenuStrip
            MenuStrip menuStrip = new MenuStrip();

            // Top Level: "File"
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");

            // Sub-Level: "Data" (changed from "Database")
            ToolStripMenuItem dataMenu = new ToolStripMenuItem("Data");

            // Items under Data
            ToolStripMenuItem backupItem = new ToolStripMenuItem("Backup Database");
            backupItem.Click += BtnBackup_Click;

            ToolStripMenuItem locateItem = new ToolStripMenuItem("Locate in Explorer");
            locateItem.Click += LocateDb_Click;

            ToolStripMenuItem exportCsvItem = new ToolStripMenuItem("Export to CSV");
            exportCsvItem.Click += ExportToCsv_Click;

            // Build the hierarchy: File -> Data -> [Backup, Locate, Export to CSV]
            dataMenu.DropDownItems.Add(backupItem);
            dataMenu.DropDownItems.Add(locateItem);
            dataMenu.DropDownItems.Add(new ToolStripSeparator());
            dataMenu.DropDownItems.Add(exportCsvItem);
            fileMenu.DropDownItems.Add(dataMenu);
            menuStrip.Items.Add(fileMenu);

            // Top Level: "View"
            ToolStripMenuItem viewMenu = new ToolStripMenuItem("View");

            ToolStripMenuItem timeclockDataItem = new ToolStripMenuItem("Timeclock Data");
            timeclockDataItem.Click += ViewTimeclockData_Click;

            viewMenu.DropDownItems.Add(timeclockDataItem);
            menuStrip.Items.Add(viewMenu);

            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // 2. Adjust controls (shifted down by 30px to accommodate menu)
            Label lblActivity = new Label
            {
                Text = "Activity:",
                Left = 20,
                Top = 50,
                Width = 80
            };

            TextBox txtActivity = new TextBox
            {
                Name = "txtActivity",
                Left = 110,
                Top = 50,
                Width = 250
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
                Width = 340,
                Height = 40,
                Font = new System.Drawing.Font("Arial", 24, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            Label lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "",
                Left = 20,
                Top = 180,
                Width = 340,
                Height = 30,
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

            // Restart the timer to clear status after 3 seconds
            statusTimer.Stop();
            statusTimer.Start();
        }

        private void ExportToCsv_Click(object sender, EventArgs e)
        {
            // Step 1: Select database file to export
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";
                ofd.Title = "Select Database to Export";
                ofd.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(dbPath));

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                string selectedDb = ofd.FileName;

                // Step 2: Select CSV save location
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    sfd.FileName = $"clockbuster_export_{timestamp}.csv";
                    sfd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    sfd.Title = "Save CSV Export";

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    string csvPath = sfd.FileName;

                    // Step 3: Perform export
                    try
                    {
                        ShowStatus("Exporting data to CSV...", System.Drawing.Color.Blue);
                        Application.DoEvents(); // Force UI update

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

                // Check if sessions table exists
                string checkTable = "SELECT name FROM sqlite_master WHERE type='table' AND name='sessions'";
                using (var cmd = new SQLiteCommand(checkTable, conn))
                {
                    var result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        throw new Exception("Database does not contain a 'sessions' table.");
                    }
                }

                // Export data
                string query = "SELECT id, activity_name, start_time, end_time, duration_minutes FROM sessions ORDER BY start_time";
                using (var cmd = new SQLiteCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    using (StreamWriter writer = new StreamWriter(csvFilePath, false, Encoding.UTF8))
                    {
                        // Write header
                        writer.WriteLine("ID,Activity,Start Time,End Time,Duration (minutes)");

                        // Write data rows
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
            // If field contains comma, quote, or newline, wrap in quotes and escape internal quotes
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
            if (string.IsNullOrWhiteSpace(txtActivity.Text))
            {
                ShowStatus("Please enter an activity name.", System.Drawing.Color.Red);
                return;
            }

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