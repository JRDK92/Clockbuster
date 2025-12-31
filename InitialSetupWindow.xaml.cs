using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ClockbusterWPF
{
    public partial class InitialSetupWindow : Window
    {
        public string DeviceName { get; private set; }
        public string DatabasePath { get; private set; }

        public InitialSetupWindow()
        {
            InitializeComponent();
            string suggested = GenerateDefaultDeviceName();
            TxtDeviceName.Text = suggested;
            TxtDeviceName.SelectAll();
            TxtDeviceName.Focus();
        }

        private string GenerateDefaultDeviceName()
        {
            string suggested = $"{Environment.UserName}-{Environment.MachineName}".ToLower();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                suggested = suggested.Replace(c.ToString(), "");
            }
            return suggested.Length > 30 ? suggested.Substring(0, 30) : suggested;
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            string deviceName = TxtDeviceName.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                MessageBox.Show("Please enter a device name.", "Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DeviceName = deviceName;
            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Visible;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Step2Panel.Visibility = Visibility.Collapsed;
            Step1Panel.Visibility = Visibility.Visible;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog();
            dialog.Title = "Select Database Storage Folder";
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (dialog.ShowDialog() == true)
            {
                string folderPath = dialog.FolderName;
                DatabasePath = Path.Combine(folderPath, $"clockbuster_{DeviceName}.db");
                TxtLocationDisplay.Text = folderPath;
                TxtLocationDisplay.Foreground = System.Windows.Media.Brushes.Black;
                BtnFinish.IsEnabled = true;
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(DatabasePath)) return;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}