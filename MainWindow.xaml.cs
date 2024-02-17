using FFMpegCore;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.ViewManagement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace VideoClipper
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private TimeSpan startTimestamp;
        private TimeSpan endTimeStamp;
        private StorageFile file;
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void addVideoButton_Click(object sender, RoutedEventArgs e)
        {
            var openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add(".mp4");
            openPicker.FileTypeFilter.Add(".mkv");
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;

            // Retrieve the window handle (HWND) of the current WinUI 3 window.
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            // Initialize the file picker with the window handle (HWND).
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // Open the picker for the user to pick a file
            file = await openPicker.PickSingleFileAsync();
            

            if (file != null)
            {
                mediaPlayerElement.Source = MediaSource.CreateFromStorageFile(file);
                mediaPlayerElement.AreTransportControlsEnabled = true;
            }
        }

        private void processVideoButton_Click(object sender, RoutedEventArgs e)
        {
            string originalFileName = Path.GetFileNameWithoutExtension(file.Path);
            string directory = Path.GetDirectoryName(file.Path);
            string fileExtension = Path.GetExtension(file.Path);

            string outputFile = directory + "\\" + originalFileName + "-clipped" + fileExtension;
            
            endTimeStamp = TimeSpan.Parse(endTimestampText.Text);
            startTimestamp = TimeSpan.Parse(startTimestampText.Text);

            processVideoButton.Content = outputFile;
            FFMpeg.SubVideo(file.Path,
                outputFile,
                startTimestamp,
                endTimeStamp
            );
        }

        private void startTimestampText_TextChanged(object sender, RoutedEventArgs e)
        {
            processVideoButton.IsEnabled = startTimestampText.Text != "" && endTimestampText.Text != "" && file.Name != null;
        }

        private void endTimestampText_TextChanged(object sender, RoutedEventArgs e)
        {
            processVideoButton.IsEnabled = startTimestampText.Text != "" && endTimestampText.Text != "" && file.Name != null;
        }
    }
}
