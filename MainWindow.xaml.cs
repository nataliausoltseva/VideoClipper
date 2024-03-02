using FFMpegCore;
using Instances;
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

    public class CommandClass
    {
        public string key { get; set; }
        public string value { get; set; }
        public bool useTheKey { get; set; }
        public int orderNumber { get; set; }
        public bool isValueFirst { get; set; }

        public CommandClass(string key, string value, bool useTheKey = false, int orderNumber = 0, bool isValueFirst = false)
        {
            this.key = key;
            this.value = value;
            this.useTheKey = useTheKey;
            this.orderNumber = orderNumber;
            this.isValueFirst = isValueFirst;
        }
    }

    public sealed partial class MainWindow : Window
    {
        private TimeSpan startTimestamp = TimeSpan.Zero;
        private TimeSpan endTimeStamp;
        private StorageFile file;
        private MediaSource clippedSource;
        private TimeSpan originalFileDuration;
        private bool shouldEncodeVideo;
        private bool shouldEncodeAudio;
        private string outputFilePath;
        private TimeSpan duration;
        private double bitrate;
        private bool isFfmpegInstalled = true;
        private CommandClass[] finalCommandCombination = new[]
        {
            new CommandClass("-i", null, false, 0, false),
            new CommandClass("-ss", null, false, 1, false),
            new CommandClass("-movflags", null, false, 2, false),
            new CommandClass("faststart", null, false, 3, false),
            new CommandClass("-t", null, false, 4, false),
            new CommandClass("-to", null, false, 4, false),
            new CommandClass("-c:v", null, false, 5, false),
            new CommandClass("-maxrate", null, false, 6, false),
            new CommandClass("-b:v", null, false, 7, false),
            new CommandClass("copy", null, false, 6, false),
            new CommandClass("-map 0:v", null, false, 8, false),
            new CommandClass("-map 0:a", null, false, 9, false),
            new CommandClass("-c:a", null, false, 10, false),
            new CommandClass("-vbr", null, false, 11, false),
            new CommandClass("copy", null, false, 11, false),
            new CommandClass("-y", null, false, 12, true),
        };

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

                    if (endTimestampText.Text != "" && endTimeStamp.TotalSeconds > originalFileDuration.TotalSeconds)
                    {
                        endTimeStamp = originalFileDuration;
                        endTimestampText.Text = originalFileDuration.ToString();
                    }
                    if (timeDurationText.Text != "" && duration.TotalSeconds > originalFileDuration.TotalSeconds)
                    {
                        duration = originalFileDuration;
                        setDurationLabel();
                    }
                }
                
                else
                {
                    generateContenDialog();
                }

                string fileName = Path.GetFileNameWithoutExtension(file.Path);
                string directory = Path.GetDirectoryName(file.Path);
                string fileExtension = Path.GetExtension(file.Path);
                outputFilePath = directory + "\\" + fileName + "-clipped" + fileExtension;
                finalCommandCombination.FirstOrDefault(k => k.key == "-i").value = "\"" + file.Path.ToString() + "\"";
                UpdateArguments();
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
                    FFMpegArguments
                        .FromFileInput(file.Path)
                        .OutputToFile(outputFilePath, true, options => options.WithCustomArgument(getFinalOutputCommand(true)))
                        .ProcessSynchronously();

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

            finalCommandCombination.FirstOrDefault(k => k.key == "-y").value = "\"" + outputFilePath.ToString() + "\"";

            double totalSeconds = timeDurationText.Text != "" ? duration.TotalSeconds : Math.Abs(startTimestamp.TotalSeconds - endTimeStamp.TotalSeconds);

            double bitrateToUse = bitrate;
            if (shouldEncodeVideo)
            {
                bitrateToUse = (int)VideoVariableBitrateSlider.Value;
            }

            double videoFileSize = bitrateToUse * totalSeconds / 8 + (0.128 * totalSeconds);
            EstimatedFileSize.Text = videoFileSize.ToString() + "MB";
            setOptions();

            OuputCommand.Text = getFinalOutputCommand();
        }

        private void setOptions()
        {
            finalCommandCombination.FirstOrDefault(k => k.key == "-ss").value = startTimestamp.ToString();
            finalCommandCombination.FirstOrDefault(k => k.key == "-movflags").useTheKey = true;
            finalCommandCombination.FirstOrDefault(k => k.key == "faststart").useTheKey = true;

            finalCommandCombination.FirstOrDefault(k => k.key == "-t").value = timeDurationText.Text != "" ? duration.ToString() :  null;
            finalCommandCombination.FirstOrDefault(k => k.key == "-to").value = timeDurationText.Text == "" ? endTimeStamp.ToString() : null;

            int videoBitrate = (int)VideoVariableBitrateSlider.Value * 1000;
            finalCommandCombination.FirstOrDefault(k => k.key == "-c:v").value = shouldEncodeVideo ? getVideoCodecType() : null;
            finalCommandCombination.FirstOrDefault(k => k.key == "-c:v").useTheKey = true;
            finalCommandCombination.FirstOrDefault(k => k.key == "-maxrate").value = shouldEncodeVideo ? videoBitrate.ToString() : null;
            finalCommandCombination.FirstOrDefault(k => k.key == "-b:v").value = shouldEncodeVideo ? videoBitrate.ToString() + "k" : null;
            finalCommandCombination.FirstOrDefault(k => k.key == "copy" && k.orderNumber == 6).useTheKey = !shouldEncodeVideo;

            finalCommandCombination.FirstOrDefault(k => k.key == "-map 0:v").useTheKey = true;
            finalCommandCombination.FirstOrDefault(k => k.key == "-map 0:a").useTheKey = true;

            finalCommandCombination.FirstOrDefault(k => k.key == "-c:a").value = shouldEncodeAudio ? getAudioCodecType() : null;
            finalCommandCombination.FirstOrDefault(k => k.key == "-c:a").useTheKey = true;
            finalCommandCombination.FirstOrDefault(k => k.key == "-vbr").value = shouldEncodeAudio ? AudioVariableBitrateSlider.Value.ToString() : null;
            finalCommandCombination.FirstOrDefault(k => k.key == "copy" && k.orderNumber == 11).useTheKey = !shouldEncodeAudio;
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

        private void setDurationLabel()
        {
            string type = dropdownDurationLabel.Content.ToString();

            switch (type)
            {
                case "mins":
                    timeDurationText.Text = Math.Round(originalFileDuration.TotalMinutes, 2).ToString();
                    break;
                case "hrs":
                    timeDurationText.Text = Math.Round(originalFileDuration.TotalHours, 2).ToString();
                    break;
                case "secs":
                    timeDurationText.Text = Math.Round(originalFileDuration.TotalSeconds, 2).ToString();
                    break;
            }
        }

        private string getVideoCodecType()
        {
            string type = dropdownVideoCodec.Content.ToString();
            return type switch
            {
                "H.264/AVC" => "libx264",
                "H.265/HEVC" => "libx265",
                "VP9" => "libvpx",
                _ => "libx264",
            };
        }

        private string getAudioCodecType()
        {
            string type = dropdownAudioCodec.Content.ToString();
            return type switch
            {
                "ACC" => "aac",
                "AC3" => "ac3",
                "MP3" => "libmp3lame",
                _ => "aac",
            };
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

                if (file != null && endTimeStamp.TotalSeconds > originalFileDuration.TotalSeconds)
                {
                    endTimeStamp = originalFileDuration;
                    endTimestampText.Text = originalFileDuration.ToString();
                }
            }
            UpdateArguments();
        }

        private void onMenuItemClick(object sender, RoutedEventArgs e)
        {
            dropdownDurationLabel.Content = (sender as MenuFlyoutItem).Text;

            if (file != null)
            {
                duration = getDurationTimeSpan();

                if (duration.TotalSeconds > originalFileDuration.TotalSeconds)
                {
                    duration = originalFileDuration;
                    setDurationLabel();
                }
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
                if (duration.TotalSeconds > originalFileDuration.TotalSeconds)
                {
                    duration = originalFileDuration;
                    setDurationLabel();
                }

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

        private string getFinalOutputCommand(bool skipFileNames = false)
        {
            CommandClass[] availableCommands = finalCommandCombination.Where(c => c.value != null || c.useTheKey).OrderBy(c => c.orderNumber).ToArray();
            string finalCommand = "";
            foreach (CommandClass commandClass in availableCommands)
            {
                if (skipFileNames && (commandClass.key == "-i" || commandClass.key == "-y"))
                {
                    continue;
                }

                if (commandClass.value != null)
                {
                    if (commandClass.isValueFirst)
                    {
                        finalCommand += (" " + commandClass.value) + (" " + commandClass.key);
                    } else
                    {
                        finalCommand += (" " + commandClass.key) + (" " + commandClass.value);
                    }
                    
                } else if (commandClass.useTheKey)
                {
                    finalCommand += (" " + commandClass.key);
                }
            }
            return skipFileNames ? finalCommand : "ffmpeg" + finalCommand;
        }
    }
}
