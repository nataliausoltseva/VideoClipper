using FFMpegCore;
using FFMpegCore.Enums;
using Instances;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
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
        private FFMpegArgumentProcessor args;
        private string outputFilePath;
        private TimeSpan duration;
        private double bitrate;
        private bool isFfmpegInstalled = true;

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
                originalFileSize.Text = getFileSize(file.Path);

                processVideoButton.IsEnabled = startTimestampText.Text != "" || endTimestampText.Text != "" || timeDurationText.Text != "";
                
                if (isFfmpegInstalled)
                {
                    var mediaInfo = FFProbe.Analyse(file.Path);
                    originalFileDuration = mediaInfo.Duration;
                    bitrate = mediaInfo.PrimaryVideoStream.BitRate / 1000000;

                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file.Path);
                    string directory = System.IO.Path.GetDirectoryName(file.Path);
                    string fileExtension = System.IO.Path.GetExtension(file.Path);
                    outputFilePath = directory + "\\" + fileName + "-clipped" + fileExtension;

                    UpdateArguments();
                }
                else
                {
                    generateContenDialog();
                }
            }
        }

        private void processVideoButton_Click(object sender, RoutedEventArgs e)
        {
            clippedSource?.Dispose();

            bool noErrors = endTimestampErrorLabel.Text == "" && endTimestampErrorLabel.Text == "";

            if (noErrors)
            {
                if (isFfmpegInstalled)
                {
                    args.ProcessSynchronously();

                    var uri = new Uri(outputFilePath);
                    clippedSource = MediaSource.CreateFromUri(uri);
                    clippedVideoLabel.Text = "Clipped video:";
                    clippedVideoMediaPlayer.Source = clippedSource;
                    clippedVideoMediaPlayer.AreTransportControlsEnabled = true;

                    clippedFileName.Text = originalFileName.Text + "-clipped" + System.IO.Path.GetExtension(file.Path);
                    clippedFilePath.Text = outputFilePath;
                    clippedFileSize.Text = getFileSize(outputFilePath);
                }
                else
                {
                    generateContenDialog();
                }
            }
        }

        private void UpdateArguments()
        {
            if (isFfmpegInstalled)
            {
                args = FFMpegArguments
                .FromFileInput(file.Path)
                .OutputToFile(outputFilePath, true, options => getOptions(options));

                double totalSeconds = timeDurationText.Text != "" ? duration.TotalSeconds : Math.Abs(startTimestamp.TotalSeconds - endTimeStamp.TotalSeconds);

                double bitrateToUse = bitrate;
                if (shouldEncodeVideo)
                {
                    bitrateToUse = (int)VideoVariableBitrateSlider.Value;
                }

                OuputCommand.Text = "ffmpeg " + args.Arguments.ToString();

                double videoFileSize = bitrateToUse * totalSeconds / 8 + (0.128 * totalSeconds);
                EstimatedFileSize.Text = videoFileSize.ToString() + "MB";
            }
            else
            {
                generateContenDialog();
            }
        }

        private FFMpegArgumentOptions getOptions(FFMpegArgumentOptions options)
        {
            options = options
                     .Seek(startTimestamp)
                     .WithFastStart();

            if (timeDurationText.Text != "")
            {
                options = options.WithDuration(duration);
            } else
            {
                options = options.EndSeek(endTimeStamp);
            }

            if (shouldEncodeVideo)
            {
                int videoBitrate = (int)VideoVariableBitrateSlider.Value * 1000;
                options = options
                    .WithVideoCodec(getVideoCodecType())
                    .WithCustomArgument("-maxrate " + videoBitrate)
                    .WithVideoBitrate(videoBitrate);
            } else
            {
                options = options.CopyChannel(Channel.Video);
            }

            options = options.WithCustomArgument("-map 0:v -map 0:a");
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
            try
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
             catch
            {
                return TimeSpan.Zero;
            }
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
            processVideoButton.IsEnabled = (startTimestampText.Text != "" || endTimestampText.Text != "" || timeDurationText.Text != "") && file != null;

            if (file != null)
            {
                startTimestamp = startTimestampText.Text != "" ? getTimeSpan(startTimestampText, startTimestampErrorLabel) : TimeSpan.Zero;
                UpdateArguments();
            }
        }

        private void endTimestampText_TextChanged(object sender, RoutedEventArgs e)
        {
            processVideoButton.IsEnabled = (startTimestampText.Text != "" || endTimestampText.Text != "" || timeDurationText.Text != "") && file != null;

            if (file != null)
            {
                endTimeStamp = endTimestampText.Text != "" ? getTimeSpan(endTimestampText, endTimestampErrorLabel) : originalFileDuration;
                UpdateArguments();
            }
        }

        private void onMenuItemClick(object sender, RoutedEventArgs e)
        {
            dropdownDurationLabel.Content = (sender as MenuFlyoutItem).Text;

            if (file != null)
            {
                duration = getDurationTimeSpan();
                UpdateArguments();
            }
        }

        private void onVideoCodecItemClick(object sender, RoutedEventArgs e)
        {
            dropdownVideoCodec.Content = (sender as MenuFlyoutItem).Text;

            if (file != null)
            {
                UpdateArguments();
            }
        }

        private void onAudioCodecItemClick(object sender, RoutedEventArgs e)
        {
            dropdownAudioCodec.Content = (sender as MenuFlyoutItem).Text;

            if (file != null)
            {
                UpdateArguments();
            }
        }

        private void AudioVariableBitrateText_TextChanged(object sender, RoutedEventArgs e)
        {
            AudioVariableBitrateSlider.Value = Double.Parse(AudioVariableBitrateText.Text);

            if (file != null)
            {
                UpdateArguments();
            }
        }

        private void VideoVariableBitrateText_TextChanged(object sender, RoutedEventArgs e)
        {
            VideoVariableBitrateSlider.Value = Double.Parse(VideoVariableBitrateText.Text);

            if (file != null)
            {
                UpdateArguments();
            }
        }

        private void AudioVariableBitrateSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (AudioVariableBitrateText != null)
            {
                AudioVariableBitrateText.Text = AudioVariableBitrateSlider.Value.ToString();
            }

            if (file != null)
            {
                UpdateArguments();
            }
        }
        private void VideoVariableBitrateSlider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (VideoVariableBitrateText != null)
            {
                VideoVariableBitrateText.Text = VideoVariableBitrateSlider.Value.ToString();
            }

            if (file != null)
            {
                UpdateArguments();
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

            if (file != null)
            {
                UpdateArguments();
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

            if (file != null)
            {
                UpdateArguments();
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

            if (file != null)
            {
                UpdateArguments();
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

            if (file != null)
            {
                UpdateArguments();
            }
        }

        private void TimeDurationText_TextChanged(object sender, TextChangedEventArgs e)
        {
            processVideoButton.IsEnabled = (startTimestampText.Text != "" || endTimestampText.Text != "" || timeDurationText.Text != "") && file != null;
            duration = getDurationTimeSpan();
            if (file != null)
            {
                UpdateArguments();
            }
        }

        private string getFileSize(string filePath)
        {
            decimal fileSizeInMB = new FileInfo(filePath).Length;
            string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            while (Math.Round(fileSizeInMB / 1024) >= 1)
            {
                fileSizeInMB /= 1024;
                counter++;
            }
            return string.Format("{0:n2}{1}", fileSizeInMB, suffixes[counter]);
        }

        private void RootPanel_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Instance.Finish(GlobalFFOptions.GetFFMpegBinaryPath(), "-version");
            }
            catch
            {
                generateContenDialog();
            }
        }

        private async void generateContenDialog()
        {
            isFfmpegInstalled = false;
            ContentDialog errorDialog = new()
            {
                XamlRoot = rootPanel.XamlRoot,
                Title = "FFMPEG not found",
                Content = "Try running \"winget install ffmpeg\" in a powershell.",
                CloseButtonText = "Ok",
            };

            await errorDialog.ShowAsync();

            if (processVideoButton != null)
            {
                ToolTip toolTip = new()
                {
                    Content = "FFMPEG not installed"
                    
                };
                ToolTipService.SetToolTip(processVideoButton, toolTip);
            }
        }
    }
}
