using Google.Cloud.Speech.V1;
using NAudio.Wave;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string OutputBasePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "sentence_extractor");

        private AudioChunkView currentChunkView = null;
        private AudioChunkView playingChunkView = null;
        private SpeechClient speechClient = null;
        private WaveStream waveStream = null;
        private string streamFileName = null;

        public MainWindow()
        {
            InitializeComponent();

            SetTitle();

            speechClient = SpeechClient.Create();

            waveView.SelectionAdjusted += delegate
            {
                if (currentChunkView != null)
                {
                    currentChunkView.TimeRange = new TimeRange()
                    {
                        Start = TimeSpan.FromSeconds((waveView.SelectionStart / waveStream.Length) * waveStream.TotalTime.TotalSeconds),
                        End = TimeSpan.FromSeconds((waveView.SelectionEnd / waveStream.Length) * waveStream.TotalTime.TotalSeconds)
                    };
                    waveView.PlayRangeEnd = currentChunkView.TimeRange.End;
                }
            };

            Loaded += delegate
            {
                //this.EnableBlur();
            };
        }

        private void SetTitle()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"VAD Edit v{version.Major}.{version.Minor}.{version.Build} [{Settings.LanguageCode}]{(string.IsNullOrWhiteSpace(streamFileName) ? "" : $" [{streamFileName}]")}";
        }

        private void Play_Clicked(object sender, RoutedEventArgs e)
        {
            waveView.Play();
        }

        private void Load_Clicked(object sender, RoutedEventArgs e)
        {
            Load();
        }

        private void LoadStream(string filePath)
        {
            streamFileName = filePath;
            var fileName = filePath.Replace(@"\", "/").Split('/').Last();
            txtCount.Text = $"Count: 0";
            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            txtWait.Text = "Loading WAV... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            btnSplit.IsEnabled = true;
            GC.Collect();

            waveView.SetWaveStream(new NAudio.Wave.WaveFileReader(filePath), (success) =>
            {
                btnExportAll.IsEnabled = false;
                btnSttAll.IsEnabled = false;

                if (success)
                {
                    txtChunkLocation.Text = System.IO.Path.Combine(OutputBasePath, fileName.Substring(0, fileName.Length - 4));
                    Directory.CreateDirectory(txtChunkLocation.Text);
                    SetTitle();

                    btnPause.IsEnabled = true;
                    btnPlay.IsEnabled = true;
                    txtChunkLocation.IsEnabled = true;
                    waveStream = waveView.WaveStream;
                }
                else
                {
                    txtChunkLocation.Text = null;
                    streamFileName = null;
                    SetTitle();

                    btnPause.IsEnabled = false;
                    btnPlay.IsEnabled = false;
                    btnSplit.IsEnabled = false;
                    btnSttAll.IsEnabled = false;
                    btnExportAll.IsEnabled = false;
                    txtChunkLocation.IsEnabled = false;
                }

                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
            });
        }

        private void Load()
        {
            waveView.Pause();

            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "WAVE Audio Files|*.wav"
            };
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                LoadStream(dlg.FileName);
            }
            dlg.Dispose();
        }

        private void ConvertLoad()
        {
            waveView.Pause();

            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "Media Files|*.wav;*.mp3;*.flac;*.mp4;*.avi;*.flv"
            };
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                txtCount.Text = $"Count: 0";
                grdTime.Children.Clear();
                currentChunkView = null;
                playingChunkView = null;
                txtWait.Text = "Converting to WAV... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;
                var chunkSaveLocation = System.IO.Path.Combine(OutputBasePath, dlg.SafeFileName.Substring(0, dlg.SafeFileName.Length - 4));
                while(File.Exists(chunkSaveLocation + ".wav"))
                {
                    if (Regex.IsMatch(chunkSaveLocation, @"_\d+$"))
                        chunkSaveLocation = Regex.Replace(chunkSaveLocation, @"_\d+$", "_" + (int.Parse(chunkSaveLocation.Split('_').Last()) + 1).ToString());
                    else
                        chunkSaveLocation = chunkSaveLocation + "_1";
                }
                Directory.CreateDirectory(OutputBasePath);
                GC.Collect();

                new Thread(() =>
                {
                    try
                    {
                        var ffmpeg = new Process();
#if DEBUG
                        ffmpeg.StartInfo.FileName = "cmd.exe";
                        ffmpeg.StartInfo.Arguments = $"/K ffmpeg.exe -y -i \"{dlg.FileName}\" -c copy -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{chunkSaveLocation}.wav\"";
                        ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
#else
                        ffmpeg.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe ");
                        ffmpeg.StartInfo.Arguments = $"-y -i \"{dlg.FileName}\" -c copy -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{chunkSaveLocation}.wav\"";
                        ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif
                        ffmpeg.StartInfo.UseShellExecute = true;
                        ffmpeg.Start();
                        ffmpeg.WaitForExit();

                        if (ffmpeg.ExitCode == 0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LoadStream(chunkSaveLocation + ".wav");
                            });
                        }
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }).Start();
            }
            dlg.Dispose();
        }

        private void SplitSilence()
        {
            waveView.Pause();

            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);

            float minVolume = Settings.MinVolume;
            int minLength = Settings.MinLength;
            int maxSilenceMillis = Settings.MaxSilence;

            txtWait.Text = "VAD processing... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            var waveTotalMillis = waveStream.TotalTime.TotalMilliseconds;
            var waveData = waveView.WaveFormData;
            var waveDataLength = waveData.Length;
            GC.Collect();

            new Thread(() =>
            {
                try
                {
                    int start = -1;
                    int end = -1;

                    int silenceCtr = 0;
                    var maxWidth = waveTotalMillis;
                    var max = waveData.Max();

                    for (int i = 0; i < (int)maxWidth; i++)
                    {
                        var secDataVolume = (waveData[(int)((i / maxWidth) * waveDataLength)] / max) * 100;
                        if (i == (int)maxWidth - 1 && start != -1)
                        {
                            end = i + 1;

                            if (end - start > minLength)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    AddItemView(new TimeRange(TimeSpan.FromMilliseconds(start), TimeSpan.FromMilliseconds(end)));
                                });
                            }

                            start = -1;
                            end = -1;
                            silenceCtr = 0;
                        }
                        else if (secDataVolume < minVolume)
                        {
                            if (start != -1 && silenceCtr >= maxSilenceMillis)
                            {
                                if (i > start)
                                {
                                    end = i - (maxSilenceMillis - 100);

                                    if (end - start > minLength)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            AddItemView(new TimeRange(TimeSpan.FromMilliseconds(start), TimeSpan.FromMilliseconds(end)));
                                        });
                                    }
                                }

                                start = -1;
                                end = -1;
                                silenceCtr = 0;
                                continue;
                            }
                            silenceCtr++;
                        }
                        else if (start == -1)
                        {
                            silenceCtr = 0;
                            start = Math.Max(i - 100, 0);
                            end = -1;
                        }
                        else
                        {
                            silenceCtr = 0;
                            end = -1;
                        }
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                    MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                GC.Collect();

                Dispatcher.Invoke(() =>
                {
                    if (grdTime.Children.OfType<AudioChunkView>().Count() > 0)
                    {
                        btnExportAll.IsEnabled = true;
                        btnSttAll.IsEnabled = true;
                    }
                    grdMain.IsEnabled = true;
                    grdWait.Visibility = Visibility.Hidden;
                });
            }).Start();
        }

        private string GetNextFileName()
        {
            var chunkSaveLocation = Dispatcher.Invoke(() => txtChunkLocation.Text);
            var fileNames = Directory.EnumerateFiles(chunkSaveLocation, "*.wav").Select(s => s.Replace(@"\", "/").Split('/').Last());
            int i = 0;
            while (true)
            {
                var fileName = $"trimmed_{i++:D4}.wav";
                if (!fileNames.Contains(fileName))
                {
                    return Path.Combine(chunkSaveLocation, fileName);
                }
            }
        }

        private void DoStt(AudioChunkView chunkView, bool suppressErrorDialogs = false, Action finishedCallback = null)
        {
            var startSecond = Dispatcher.Invoke(() => chunkView.TimeRange.Start.TotalSeconds);
            var endSecond = Dispatcher.Invoke(() => chunkView.TimeRange.End.TotalSeconds);

            if (endSecond - startSecond > 30)
            {
                Dispatcher.Invoke(() =>
                {
                    File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [WARN]: Processing STT on audio longer than 30 seconds:\n    {streamFileName}: {chunkView.TimeRange}\n");
                    finishedCallback?.Invoke();
                    if (!suppressErrorDialogs)
                    {
                        MessageBox.Show("Will not process audio longer than 30 seconds.", "Operation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
                return;
            }

            var start = (long)((startSecond / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
            var end = (long)((endSecond / waveStream.TotalTime.TotalSeconds) * waveStream.Length);

            try
            {
                var oldPos = waveStream.Position;

                using (var streamBuffer = new MemoryStream())
                {

                    Func<double> alignStart = () => start / (double)waveStream.WaveFormat.BlockAlign;
                    Func<double> alignEnd = () => end / (double)waveStream.WaveFormat.BlockAlign;

                    while (alignStart() != (int)alignStart())
                    {
                        start += 1;
                    }

                    while (alignEnd() != (int)alignEnd())
                    {
                        end += 1;
                    }

                    waveStream.Position = start;

                    byte[] buffer = new byte[1024];
                    while (waveStream.Position < end)
                    {
                        int bytesRequired = (int)(end - waveStream.Position);
                        if (bytesRequired > 0)
                        {
                            int bytesToRead = Math.Min(bytesRequired, buffer.Length);
                            int bytesRead = waveStream.Read(buffer, 0, bytesToRead);
                            if (bytesRead > 0)
                            {
                                streamBuffer.Write(buffer, 0, bytesRead);
                            }
                        }
                    }

                    waveStream.Position = oldPos;
                    streamBuffer.Position = 0;

                    var streamingCall = speechClient.StreamingRecognize();

                    streamingCall.WriteAsync(new StreamingRecognizeRequest()
                    {
                        StreamingConfig = new StreamingRecognitionConfig()
                        {
                            Config = new RecognitionConfig()
                            {
                                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                                SampleRateHertz = 16000,
                                LanguageCode = Settings.LanguageCode,
                            }
                        }
                    }).Wait();

                    streamingCall.WriteAsync(
                        new StreamingRecognizeRequest()
                        {
                            AudioContent = Google.Protobuf.ByteString.CopyFrom(streamBuffer.ToArray()),
                        }).Wait();

                    streamingCall.WriteCompleteAsync().Wait();

                    new Thread(async () =>
                    {
                        try
                        {
                            while (await streamingCall.ResponseStream.MoveNext(default(CancellationToken)))
                            {
                                foreach (var result in streamingCall.ResponseStream.Current.Results)
                                {
                                    foreach (var alternative in result.Alternatives)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            chunkView.GSttText = alternative.Transcript;
                                        });
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                            if (!suppressErrorDialogs)
                            {
                                MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }

                        Dispatcher.Invoke(() =>
                        {
                            finishedCallback?.Invoke();
                        });
                    }).Start();
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                if (!suppressErrorDialogs)
                {
                    MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DoExport(AudioChunkView chunkView, bool suppressErrorDialogs = false)
        {
            var chunkLocation = Dispatcher.Invoke(() => txtChunkLocation.Text);
            var speechText = Dispatcher.Invoke(() => chunkView.SpeechText);
            var gSttText = Dispatcher.Invoke(() => chunkView.GSttText);

            if (!string.IsNullOrWhiteSpace(speechText))
            {
                waveView.Pause();
                var savePath = GetNextFileName();

                if (System.IO.File.Exists(savePath))
                {
                    System.IO.File.Delete(savePath);
                }

                var oldPos = waveStream.Position;

                using (var writer = new WaveFileWriter(savePath, waveStream.WaveFormat))
                {
                    var start = (long)((Dispatcher.Invoke(() => chunkView.TimeRange.Start.TotalSeconds) / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
                    var end = (long)((Dispatcher.Invoke(() => chunkView.TimeRange.End.TotalSeconds) / waveStream.TotalTime.TotalSeconds) * waveStream.Length);

                    Func<double> alignStart = () => start / (double)waveStream.WaveFormat.BlockAlign;
                    Func<double> alignEnd = () => end / (double)waveStream.WaveFormat.BlockAlign;

                    while (alignStart() != (int)alignStart())
                    {
                        start += 1;
                    }

                    while (alignEnd() != (int)alignEnd())
                    {
                        end += 1;
                    }

                    waveStream.Position = start;
                    byte[] buffer = new byte[1024];
                    while (waveStream.Position < end)
                    {
                        int bytesRequired = (int)(end - waveStream.Position);
                        if (bytesRequired > 0)
                        {
                            int bytesToRead = Math.Min(bytesRequired, buffer.Length);
                            int bytesRead = waveStream.Read(buffer, 0, bytesToRead);
                            if (bytesRead > 0)
                            {
                                writer.Write(buffer, 0, bytesRead);
                            }
                        }
                    }
                }
                waveStream.Position = oldPos;

                var line = $"{savePath.Replace(@"\", "/").Split('/').Last()},";

                if (Settings.IncludeAudioFileSize)
                    line += $"{new FileInfo(savePath).Length},";

                if (Settings.IncludeSttResult)
                    line += $"\"{gSttText}\",";

                line += $"\"{speechText}\"\n";

                File.AppendAllText(System.IO.Path.Combine(chunkLocation, "sentence_map.csv"), line);

                Dispatcher.Invoke(() =>
                {
                    //grdTime.Children.Remove(chunkView);
                    txtCount.Text = $"Count: {grdTime.Children.Count}";

                    if (!suppressErrorDialogs)
                    {
                        MessageBox.Show($"Done!\n{savePath}", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
            else if (!suppressErrorDialogs)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Text is empty!", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }

        private void AddItemView(TimeRange timeRange, string gSttText = null, string speechText = null, int insertIndex = -1)
        {
            var chunkView = new AudioChunkView()
            {
                TimeRange = timeRange,
                GSttText = gSttText,
                SpeechText = speechText
            };

            chunkView.PlayButtonClicked += delegate
            {
                ShowSelection(chunkView.TimeRange);
                waveView.Play(chunkView.TimeRange);
                waveView.InvalidateVisual();
                chunkView.PlayButtonVisibility = Visibility.Hidden;
                playingChunkView = chunkView;
            };

            chunkView.StopButtonClicked += delegate
            {
                waveView.Pause();
                waveStream.CurrentTime = chunkView.TimeRange.Start;
                waveView.InvalidateVisual();
                chunkView.PlayButtonVisibility = Visibility.Visible;
                playingChunkView = null;
            };

            chunkView.GotSelectionFocus += delegate
            {
                if (playingChunkView != null && playingChunkView != chunkView)
                {
                    waveView.Pause();
                    playingChunkView.PlayButtonVisibility = Visibility.Visible;
                    playingChunkView = null;
                }
                ShowSelection(chunkView.TimeRange);
                currentChunkView = chunkView;
            };

            chunkView.SttButtonClicked += delegate
            {
                waveView.Pause();

                txtWait.Text = "STT processing... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                new Thread(() =>
                {
                    DoStt(chunkView, finishedCallback: () =>
                    {
                        grdMain.IsEnabled = true;
                        grdWait.Visibility = Visibility.Hidden;
                    });
                }).Start();
            };

            chunkView.DuplicateButtonClicked += delegate
            {
                AddItemView(chunkView.TimeRange, chunkView.GSttText, chunkView.SpeechText, grdTime.Children.IndexOf(chunkView) + 1);
            };

            chunkView.ExportButtonClicked += delegate
            {
                DoExport(chunkView);
            };

            chunkView.DeleteButtonClicked += delegate
            {
                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);
                var index = grdTime.Children.IndexOf(chunkView);
                grdTime.Children.Remove(chunkView);
                if (grdTime.Children.Count > 0)
                    (grdTime.Children[index == grdTime.Children.Count ? index - 1 : index] as AudioChunkView)?.Select();
                else
                {
                    btnExportAll.IsEnabled = false;
                    btnSttAll.IsEnabled = false;
                }
                txtCount.Text = $"Count: {grdTime.Children.Count}";
            };

            if (insertIndex == -1)
                grdTime.Children.Add(chunkView);
            else
                grdTime.Children.Insert(insertIndex, chunkView);

            txtCount.Text = $"Count: {grdTime.Children.Count}";
        }

        private void ShowSelection(TimeRange range, bool allowSelectionChange = true)
        {
            waveView.AllowSelectionChange = allowSelectionChange;
            waveView.SelectionStart = (range.Start.TotalMilliseconds / waveStream.TotalTime.TotalMilliseconds) * waveStream.Length;
            waveView.SelectionEnd = (range.End.TotalMilliseconds / waveStream.TotalTime.TotalMilliseconds) * waveStream.Length;
            if (waveView.Player.PlaybackState != PlaybackState.Playing)
            {
                waveStream.Position = (int)waveView.SelectionStart;
                waveView.RenderPositionLine(true);
            }
            waveView.InvalidateVisual();
        }

        private void Split_Click(object sender, RoutedEventArgs e)
        {
            if (grdTime.Children.Count > 0 && MessageBox.Show("Existing chunks will be overwritten.\nContinue?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                return;
            SplitSilence();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            waveView.Pause();
        }

        private void Convert_Click(object sender, RoutedEventArgs e)
        {
            ConvertLoad();
        }


        private void SttAll_Click(object sender, RoutedEventArgs e)
        {
            var chunkViews = grdTime.Children.OfType<AudioChunkView>().Where(c => string.IsNullOrWhiteSpace(c.SpeechText)).ToArray();

            if (chunkViews.Count() == 0)
            {
                MessageBox.Show("All chunks already have text value.", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (MessageBox.Show("Are you sure you want to do STT for all valid chunks?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);
                waveView.Pause();

                txtWait.Text = "STT processing... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                new Thread(() =>
                {
                    int ctr = 0;
                    foreach (var chunkView in chunkViews)
                    {
                        if (ctr++ == chunkViews.Count() - 1)
                        {
                            DoStt(chunkView, true, () =>
                            {
                                grdMain.IsEnabled = true;
                                grdWait.Visibility = Visibility.Hidden;
                            });
                        }
                        else
                        {
                            DoStt(chunkView, true);
                        }
                    }
                }).Start();
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            var chunkViews = grdTime.Children.OfType<AudioChunkView>().Where(c => !string.IsNullOrWhiteSpace(c.SpeechText)).ToArray();

            if (chunkViews.Count() == 0)
            {
                MessageBox.Show("No chunks has been given text value.", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (MessageBox.Show("Are you sure you want to export all valid chunks?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);
                txtWait.Text = "Exporting... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                new Thread(() =>
                {
                    foreach (var chunkView in chunkViews)
                    {
                        DoExport(chunkView, true);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        grdMain.IsEnabled = true;
                        grdWait.Visibility = Visibility.Hidden;
                        if (grdTime.Children.Count == 0)
                        {
                            MessageBox.Show($"All Done!\nSaved in \"{txtChunkLocation.Text}\"", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show($"Partially Done!\nSaved in \"{txtChunkLocation.Text}\"", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    });
                }).Start();
            }
        }

        private void waveView_PlayRangeEnded(object sender, EventArgs e)
        {
            if (playingChunkView != null)
            {
                waveView.Pause();
                playingChunkView.PlayButtonVisibility = Visibility.Visible;
                playingChunkView = null;
            }
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow.Show();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (waveStream != null && MessageBox.Show("Are you sure you want to exit?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                e.Cancel = true;
            }

            base.OnClosing(e);
        }
    }

}
