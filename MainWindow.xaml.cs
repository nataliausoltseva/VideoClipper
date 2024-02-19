using FFMpegCore;
using FFMpegCore.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualBasic;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
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
    /// 

    public sealed partial class MainWindow : Window
    {
        private TimeSpan startTimestamp;
        private TimeSpan endTimeStamp;
        private StorageFile file;
        private MediaSource clippedSource;
        private TimeSpan originalFileDuration;

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
                originalVideoLabel.Text = "Original video:";
                originalVideoMediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
                originalVideoMediaPlayer.AreTransportControlsEnabled = true;
                originalFileName.Text = file.Name;
                originalFilePath.Text = file.Path;
                processVideoButton.IsEnabled = startTimestampText.Text != "" || endTimestampText.Text != "";
                var mediaInfo = FFProbe.Analyse(file.Path);
                originalFileDuration = mediaInfo.Duration;
            }
        }

        private void processVideoButton_Click(object sender, RoutedEventArgs e)
        {
            clippedSource?.Dispose();

            string originalFileName = Path.GetFileNameWithoutExtension(file.Path);
            string directory = Path.GetDirectoryName(file.Path);
            string fileExtension = Path.GetExtension(file.Path);

            string outputFile = directory + "\\" + originalFileName + "-clipped" + fileExtension;

            endTimeStamp = endTimestampText.Text != "" ? getTimeSpan(endTimestampText, endTimestampErrorLabel) : originalFileDuration;
            startTimestamp = startTimestampText.Text != "" ? getTimeSpan(startTimestampText, startTimestampErrorLabel) : TimeSpan.Zero;

            bool noErrors = endTimestampErrorLabel.Text == "" && endTimestampErrorLabel.Text == "";

            if (noErrors)
            {
                FFMpegArguments
                .FromFileInput(file.Path)
                .OutputToFile(outputFile, true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithConstantRateFactor(21)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithVariableBitrate(4)
                    .WithFastStart()
                    .Seek(startTimestamp)
                    .EndSeek(endTimeStamp))
                .ProcessSynchronously();

                var uri = new System.Uri(outputFile);
                clippedSource = MediaSource.CreateFromUri(uri);
                clippedVideoLabel.Text = "Clipped video:";
                clippedVideoMediaPlayer.Source = clippedSource;
                clippedVideoMediaPlayer.AreTransportControlsEnabled = true;
                clippedFileName.Text = originalFileName + "-clipped" + fileExtension;
                clippedFilePath.Text = outputFile;
            }
        }

        private TimeSpan getTimeSpan(TextBox timeSpan, TextBlock errorElement)
        {
            try
            {
                errorElement.Text = "";
                return TimeSpan.Parse(timeSpan.Text);
            } catch {
                errorElement.Text = "The format is wrong.";
                return TimeSpan.Zero;
            }
        }

        private void startTimestampText_TextChanged(object sender, RoutedEventArgs e)
        {
            processVideoButton.IsEnabled = (startTimestampText.Text != "" || endTimestampText.Text != "") && file != null;
        }

        private void endTimestampText_TextChanged(object sender, RoutedEventArgs e)
        {
            processVideoButton.IsEnabled = (startTimestampText.Text != "" || endTimestampText.Text != "") && file != null;
        }
    }
}
