using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;

namespace Clockbuster
{
    public partial class MainForm : Form
    {
        private DateTime startTime;
        private System.Windows.Forms.Timer displayTimer;
        private bool isTracking = false;
        private string dbPath = "clockbuster.db"; // Changed from timely.db

        public MainForm()
        {
            InitializeComponent();
            InitializeDatabase();
            displayTimer = new System.Windows.Forms.Timer();
            displayTimer.Interval = 1000;
            displayTimer.Tick += DisplayTimer_Tick;
            this.FormClosing += Form1_FormClosing;
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
            this.Text = "Clockbuster - Time Tracker"; // Changed from Timely
            this.Width = 400;
            this.Height = 310;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 1. Create the MenuStrip
            MenuStrip menuStrip = new MenuStrip();

            // Top Level: "File"
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");

            // Sub-Level: "Database"
            ToolStripMenuItem dbMenu = new ToolStripMenuItem("Database");

            // Items under Database
            ToolStripMenuItem backupItem = new ToolStripMenuItem("Backup Database");
            backupItem.Click += BtnBackup_Click;

            ToolStripMenuItem locateItem = new ToolStripMenuItem("Locate in Explorer");
            locateItem.Click += LocateDb_Click;

            // Build the hierarchy: File -> Database -> [Backup, Locate]
            dbMenu.DropDownItems.Add(backupItem);
            dbMenu.DropDownItems.Add(locateItem);
            fileMenu.DropDownItems.Add(dbMenu);
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

        // New method to ensure database exists and is valid
        private bool EnsureDatabaseExists()
        {
            try
            {
                // Check if file exists
                if (!File.Exists(dbPath))
                {
                    MessageBox.Show("Database file was deleted or moved. Creating a new database...",
                        "Database Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    InitializeDatabase();
                    return true;
                }

                // Check if table exists
                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();
                    string checkTable = "SELECT name FROM sqlite_master WHERE type='table' AND name='sessions'";
                    using (var cmd = new SQLiteCommand(checkTable, conn))
                    {
                        var result = cmd.ExecuteScalar();
                        if (result == null)
                        {
                            MessageBox.Show("Database table is missing. Recreating database structure...",
                                "Database Corrupted", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                            // Recreate the table
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
                MessageBox.Show($"Database error: {ex.Message}\n\nPlease restart the application.",
                    "Critical Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void LocateDb_Click(object sender, EventArgs e)
        {
            try
            {
                string fullPath = Path.GetFullPath(dbPath);

                if (!File.Exists(fullPath))
                {
                    MessageBox.Show("Database file not found yet (it will be created on first launch).", "Info");
                    return;
                }

                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open explorer: {ex.Message}", "Error");
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
                MessageBox.Show("Please enter an activity name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        }

        private void BtnClockOut_Click(object sender, EventArgs e)
        {
            DateTime endTime = DateTime.Now;
            TimeSpan duration = endTime - startTime;
            double durationMinutes = duration.TotalMinutes;

            TextBox txtActivity = (TextBox)this.Controls["txtActivity"];

            // Ensure database exists before saving
            if (!EnsureDatabaseExists())
            {
                MessageBox.Show("Cannot save session due to database error.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SaveSession(txtActivity.Text, startTime, endTime, durationMinutes);

            isTracking = false;
            displayTimer.Stop();

            Button btnClockIn = (Button)this.Controls["btnClockIn"];
            Button btnClockOut = (Button)this.Controls["btnClockOut"];
            Label lblElapsed = (Label)this.Controls["lblElapsed"];
            Label lblStatus = (Label)this.Controls["lblStatus"];

            btnClockIn.Enabled = true;
            btnClockOut.Enabled = false;
            txtActivity.Enabled = true;
            txtActivity.Text = "";
            lblElapsed.Text = "00:00";
            lblStatus.Text = $"Session saved! Duration: {(int)duration.TotalMinutes}m {duration.Seconds}s";
            lblStatus.ForeColor = System.Drawing.Color.Green;
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
                MessageBox.Show($"Failed to save session: {ex.Message}", "Save Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnBackup_Click(object sender, EventArgs e)
        {
            if (!EnsureDatabaseExists())
                return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                sfd.FileName = $"clockbuster_backup_{timestamp}.db"; // Changed from timely
                sfd.Filter = "Database files (*.db)|*.db|All files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        File.Copy(dbPath, sfd.FileName, true);
                        MessageBox.Show("Backup created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}