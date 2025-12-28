using System;
using System.Data;
using System.Data.SQLite;
using System.Windows.Forms;

namespace Clockbuster
{
    public partial class TimeclockViewerForm : Form
    {
        private DataGridView dataGridView;
        private string dbPath;

        public TimeclockViewerForm(string databasePath)
        {
            dbPath = databasePath;
            InitializeComponent();
            SetupControls();
            LoadData();
        }

        private void SetupControls()
        {
            this.Text = "Timeclock Data Viewer";
            this.Width = 800;
            this.Height = 500;
            this.StartPosition = FormStartPosition.CenterScreen;

            dataGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };

            Button btnRefresh = new Button
            {
                Text = "Refresh",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            btnRefresh.Click += BtnRefresh_Click;

            this.Controls.Add(dataGridView);
            this.Controls.Add(btnRefresh);
        }

        private void LoadData()
        {
            try
            {
                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();
                    string query = "SELECT id, activity_name, start_time, end_time, duration_minutes FROM sessions ORDER BY start_time DESC";

                    using (var adapter = new SQLiteDataAdapter(query, conn))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        dataGridView.DataSource = dt;

                        // Make columns more readable
                        if (dataGridView.Columns.Count > 0)
                        {
                            dataGridView.Columns["id"].HeaderText = "ID";
                            dataGridView.Columns["activity_name"].HeaderText = "Activity";
                            dataGridView.Columns["start_time"].HeaderText = "Start Time";
                            dataGridView.Columns["end_time"].HeaderText = "End Time";
                            dataGridView.Columns["duration_minutes"].HeaderText = "Duration (min)";
                            dataGridView.Columns["duration_minutes"].DefaultCellStyle.Format = "F2";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            LoadData();
        }
    }
}