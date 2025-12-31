using System;
using System.IO;
using System.Windows;

namespace ClockbusterWPF
{
    public partial class DeviceRenameWindow : Window
    {
        public string DeviceName { get; private set; }

        public DeviceRenameWindow(string currentName)
        {
            InitializeComponent();

            TxtDeviceName.Text = currentName;
            TxtDeviceName.SelectAll();
            TxtDeviceName.Focus();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string deviceName = TxtDeviceName.Text.Trim().ToLower();

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                MessageBox.Show("Please enter a device name.", "Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Clean the device name
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                deviceName = deviceName.Replace(c.ToString(), "");
            }

            if (string.IsNullOrWhiteSpace(deviceName))
            {
                MessageBox.Show("Device name contains only invalid characters. Please use letters and numbers.",
                    "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (deviceName.Length > 30)
                deviceName = deviceName.Substring(0, 30);

            DeviceName = deviceName;
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