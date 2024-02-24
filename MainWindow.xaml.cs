using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using Windows.Media.Core;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VideoClipper
{
    public sealed partial class MainWindow : Window
    {
        private TimeSpan startTimestamp;
        private TimeSpan endTimeStamp;
        private StorageFile file;
        private MediaSource clippedSource;
        private TimeSpan originalFileDuration;
        private bool shouldEncodeVideo;
        private bool shouldEncodeAudio;

        public MainWindow()
        {
            InitializeComponent();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
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
                .OutputToFile(outputFile, true, options => getOptions(options))
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

        private FFMpegArgumentOptions getOptions(FFMpegArgumentOptions options)
        {
            options = options
                     .Seek(startTimestamp)
                     .WithFastStart();

            if (timeDurationText.Text != "")
            {
                TimeSpan duration = getDurationTimeSpan();
                options = options.WithDuration(duration);
            } else
            {
                options = options.EndSeek(endTimeStamp);
            }

            if (shouldEncodeVideo)
            {
                options = options
                    .WithVideoCodec(getVideoCodecType())
                    .WithVariableBitrate((int)VideoVariableBitrateSlider.Value);
            } else
            {
                options = options.CopyChannel(Channel.Video);
            }

            if (shouldEncodeAudio)
            {
                options = options
                     .WithAudioCodec(getAudioCodecType())
                     .WithVariableBitrate((int)AudioVariableBitrateSlider.Value);
            }
            else
            {
                options = options.CopyChannel(Channel.Audio);
            }

            return options;
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

        private TimeSpan getDurationTimeSpan()
        {
            int duration = Int32.Parse(timeDurationText.Text);
            string type = dropdownDurationLabel.Content.ToString();

            switch (type)
            {
                case "mins":
                    duration *= 60;
                    break;
                case "hrs":
                    duration *= 60 * 60;
                    break;
            }

            return TimeSpan.FromSeconds(duration);
        }

        private Codec getVideoCodecType()
        {
            string type = dropdownDurationLabel.Content.ToString();

            Codec videoCodec = VideoCodec.LibX264;

            switch (type)
            {
                case "H.264/AVC":
                    videoCodec = VideoCodec.LibX264;
                    break;
                case "H.265/HEVC":
                    videoCodec = VideoCodec.LibX265;
                    break;
                case "VP9":
                    videoCodec = VideoCodec.LibVpx;
                    break;
            }

            return videoCodec;
        }

        private Codec getAudioCodecType()
        {
            string type = dropdownAudioCodec.Content.ToString();

            Codec audioCodec = AudioCodec.Aac;

            switch (type)
            {
                case "ACC":
                    audioCodec = AudioCodec.Aac;
                    break;
                case "AC3":
                    audioCodec = AudioCodec.Ac3;
                    break;
                case "MP3":
                    audioCodec = AudioCodec.LibMp3Lame;
                    break;
            }

            return audioCodec;
        }

        private void startTimestampText_TextChanged(object sender, RoutedEventArgs e)
        {
            processVideoButton.IsEnabled = (startTimestampText.Text != "" || endTimestampText.Text != "") && file != null;
        }

        private void endTimestampText_TextChanged(object sender, RoutedEventArgs e)
        {
            processVideoButton.IsEnabled = (startTimestampText.Text != "" || endTimestampText.Text != "") && file != null;
        }

        private void onMenuItemClick(object sender, RoutedEventArgs e)
        {
            dropdownDurationLabel.Content = (sender as MenuFlyoutItem).Text;
        }

        private void onVideoCodecItemClick(object sender, RoutedEventArgs e)
        {
            dropdownVideoCodec.Content = (sender as MenuFlyoutItem).Text;
        }

        private void onAudioCodecItemClick(object sender, RoutedEventArgs e)
        {
            dropdownAudioCodec.Content = (sender as MenuFlyoutItem).Text;
        }

        private void AudioVariableBitrateText_TextChanged(object sender, RoutedEventArgs e)
        {
            AudioVariableBitrateSlider.Value = Double.Parse(AudioVariableBitrateText.Text);
        }

        private void VideoVariableBitrateText_TextChanged(object sender, RoutedEventArgs e)
        {
            VideoVariableBitrateSlider.Value = Double.Parse(VideoVariableBitrateText.Text);
        }

        private void AudioVariableBitrateSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (AudioVariableBitrateText != null)
            {
                AudioVariableBitrateText.Text = AudioVariableBitrateSlider.Value.ToString();
            }
        }
        private void VideoVariableBitrateSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (VideoVariableBitrateText != null)
            {
                VideoVariableBitrateText.Text = VideoVariableBitrateSlider.Value.ToString();
            }
        }

        private void EncodeVideo_Checked(object sender, RoutedEventArgs e)
        {
            shouldEncodeVideo = true;

            if (dropdownVideoCodec != null && VideoVariableBitrateSlider != null && VideoVariableBitrateText != null)
            {
                dropdownVideoCodec.IsEnabled = true;
                VideoVariableBitrateSlider.IsEnabled = true;
                VideoVariableBitrateText.IsEnabled = true;
            }
        }

        private void EncodeVideo_Unchecked(object sender, RoutedEventArgs e)
        {
            shouldEncodeVideo = false;
            
            if (dropdownVideoCodec != null && VideoVariableBitrateSlider != null && VideoVariableBitrateText != null)
            {
                dropdownVideoCodec.IsEnabled = false;
                VideoVariableBitrateSlider.IsEnabled = false;
                VideoVariableBitrateText.IsEnabled = false;
            }
        }

        private void EncodeAudio_Checked(object sender, RoutedEventArgs e)
        {
            shouldEncodeAudio = true;
            
            if (dropdownAudioCodec != null && AudioVariableBitrateSlider != null && AudioVariableBitrateText != null)
            {
                dropdownAudioCodec.IsEnabled = true;
                AudioVariableBitrateSlider.IsEnabled = true;
                AudioVariableBitrateText.IsEnabled = true;
            }
        }

        private void EncodeAudio_Unchecked(object sender, RoutedEventArgs e)
        {
            shouldEncodeAudio = false;
            if (dropdownAudioCodec != null && AudioVariableBitrateSlider != null && AudioVariableBitrateText != null)
            {
                dropdownAudioCodec.IsEnabled = false;
                AudioVariableBitrateSlider.IsEnabled = false;
                AudioVariableBitrateText.IsEnabled = false;
            }
        }
    }
}
