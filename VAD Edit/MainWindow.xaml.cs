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
using System.Windows.Media;
using static VADEdit.Utils;

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
        private bool cancelFlag = true;

        public MainWindow()
        {
            InitializeComponent();

            Application.Current.MainWindow = this;
            Background = (SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AppBackgroundColor);

            SetTitle();

            Settings.Save();

            AudioChunkView.StaticFocused += delegate
            {
                btnAddChunk.IsEnabled = false;
            };

            waveView.SelectionChanged += (o, e) =>
            {
                if (currentChunkView != null)
                {
                    currentChunkView.TimeRange = e;
                    waveView.PlayRangeEnd = currentChunkView.TimeRange.End;
                    currentChunkView.GSttText = null;
                    currentChunkView.VisualState = AudioChunkView.State.Idle;
                }
                btnAddChunk.IsEnabled = false;
            };

            waveView.NewSelection += delegate
            {
                btnAddChunk.IsEnabled = true;
            };

            Loaded += delegate
            {
                //this.EnableBlur();
            };
        }

        internal void RenewSpeechClient()
        {
            speechClient = SpeechClient.Create();
            GC.Collect();
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
            btnCancel.Visibility = Visibility.Collapsed;
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
                btnCancel.Visibility = Visibility.Visible;
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
                btnAddChunk.IsEnabled = false;
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
                btnCancel.Visibility = Visibility.Collapsed;
                grdMain.IsEnabled = false;
                btnAddChunk.IsEnabled = false;
                var chunkSaveLocation = System.IO.Path.Combine(OutputBasePath, dlg.SafeFileName.Substring(0, dlg.SafeFileName.Length - 4));
                while (File.Exists(chunkSaveLocation + ".wav"))
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
#if false //DEBUG
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
                        //File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                        Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }).Start();
            }
            dlg.Dispose();
        }

        private void SplitSilence()
        {
            cancelFlag = false;
            waveView.Pause();

            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);

            txtWait.Text = "VAD processing... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            btnAddChunk.IsEnabled = false;

            var waveTotalMillis = waveStream.TotalTime.TotalMilliseconds;
            var waveData = waveView.WaveFormData;
            var waveDataLength = waveData.Length;

            GC.Collect();

            if (Settings.SplitOnSilence)
            {

                float minVolume = Settings.MinVolume;
                int minLength = Settings.MinLength;
                int maxSilenceMillis = Settings.MaxSilence;

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
                            if (cancelFlag)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    grdMain.IsEnabled = true;
                                    grdWait.Visibility = Visibility.Hidden;
                                });
                                break;
                            }
                            var secDataVolume = (waveData[(int)((i / maxWidth) * waveDataLength)] / max) * 100;
                            if (i == (int)maxWidth - 1 && start != -1)
                            {
                                end = i + 1;

                                if (end - start > minLength)
                                {
                                    Thread.Sleep(10);
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
                                            Thread.Sleep(10);
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
                        //File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                        Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
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
            else
            {
                var splitLength = Settings.SplitLength;

                new Thread(() =>
                {
                    try
                    {
                        var waveMillisCounter = 0;
                        while (waveMillisCounter < waveTotalMillis)
                        {
                            if (cancelFlag)
                            {
                                cancelFlag = false;
                                Dispatcher.Invoke(() =>
                                {
                                    grdMain.IsEnabled = true;
                                    grdWait.Visibility = Visibility.Hidden;
                                });
                                break;
                            }

                            Thread.Sleep(10);
                            var end = waveMillisCounter + splitLength;
                            if (end > waveTotalMillis)
                                end = (int)waveTotalMillis;
                            Dispatcher.Invoke(() =>
                            {
                                AddItemView(new TimeRange(TimeSpan.FromMilliseconds(waveMillisCounter), TimeSpan.FromMilliseconds(end)));
                            });

                            waveMillisCounter += splitLength;
                        }
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        //File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                        Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
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
            Dispatcher.Invoke(() =>
            {
                chunkView.BringIntoView();
                if (!string.IsNullOrWhiteSpace(chunkView.SpeechText))
                    return;
            });

            var startSecond = Dispatcher.Invoke(() => chunkView.TimeRange.Start.TotalSeconds);
            var endSecond = Dispatcher.Invoke(() => chunkView.TimeRange.End.TotalSeconds);

            if (endSecond - startSecond > 30)
            {
                Dispatcher.Invoke(() =>
                {
                    //File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [WARN]: Processing STT on audio longer than 30 seconds:\n    {streamFileName}: {chunkView.TimeRange}\n");
                    chunkView.VisualState = AudioChunkView.State.Error;
                    Logger.Log($"Processing STT on audio longer than 30 seconds:\n    {streamFileName}: {chunkView.TimeRange}", Logger.Type.Warn);
                    if (!suppressErrorDialogs)
                    {
                        MessageBox.Show("Will not process audio longer than 30 seconds.", "Operation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    finishedCallback?.Invoke();
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

                    var response = speechClient.Recognize(
                        new RecognitionConfig()
                        {
                            Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = 16000,
                            LanguageCode = Settings.LanguageCode,
                        }, new RecognitionAudio()
                        {
                            Content = Google.Protobuf.ByteString.CopyFrom(streamBuffer.ToArray())
                        }
                    );

                    foreach (var result in response.Results)
                    {
                        foreach (var alternative in result.Alternatives)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                chunkView.GSttText = alternative.Transcript.ToLower();
                                chunkView.SpeechText = alternative.Transcript.ToLower();
                                chunkView.VisualState = AudioChunkView.State.STTSuccess;
                                finishedCallback?.Invoke();
                            });
                            return;
                        }
                    }

                    throw new Exception("unrecognizable stream");
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                //File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                Dispatcher.Invoke(() =>
                {
                    chunkView.VisualState = AudioChunkView.State.Error;
                });
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                Dispatcher.Invoke(() =>
                {
                    if (!suppressErrorDialogs)
                    {
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finishedCallback?.Invoke();
                });
            }
        }

        private void DoExport(AudioChunkView chunkView, bool suppressErrorDialogs = false)
        {
            Dispatcher.Invoke(() =>
            {
                chunkView.BringIntoView();
                if (string.IsNullOrWhiteSpace(chunkView.SpeechText))
                {
                    chunkView.VisualState = AudioChunkView.State.Error;
                    return;
                }
            });

            var chunkLocation = Dispatcher.Invoke(() => txtChunkLocation.Text);
            var speechText = Dispatcher.Invoke(() => chunkView.SpeechText);
            var gSttText = Dispatcher.Invoke(() => chunkView.GSttText);

            if (!string.IsNullOrWhiteSpace(speechText))
            {
                waveView.Pause();
                var savePath = GetNextFileName();

                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                }

                var oldPos = waveStream.Position;
                var chunkStart = Dispatcher.Invoke(() => chunkView.TimeRange.Start.TotalSeconds);
                var chunkEnd = Dispatcher.Invoke(() => chunkView.TimeRange.End.TotalSeconds);

                using (var writer = new WaveFileWriter(savePath, waveStream.WaveFormat))
                {
                    var start = (long)((chunkStart / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
                    var end = (long)((chunkEnd / waveStream.TotalTime.TotalSeconds) * waveStream.Length);

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

                if (Settings.IncludeAudioLengthMillis)
                    line += $"{(long)TimeSpan.FromSeconds(chunkEnd - chunkStart).TotalMilliseconds},";

                if (Settings.IncludeSttResult)
                    line += $"\"{gSttText}\",";

                line += $"\"{speechText}\"\n";

                File.AppendAllText(System.IO.Path.Combine(chunkLocation, "sentence_map.csv"), line);
                Dispatcher.Invoke(() =>
                {
                    chunkView.VisualState = AudioChunkView.State.ExportSuccess;
                    if (!suppressErrorDialogs)
                        MessageBox.Show($"Done!\n{savePath}", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    chunkView.VisualState = AudioChunkView.State.Error;
                    if (!suppressErrorDialogs)
                        MessageBox.Show("Text is empty!", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }

        private async void AddItemView(TimeRange timeRange, string gSttText = null, string speechText = null, int insertIndex = -1, bool focus = false)
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
                btnCancel.Visibility = Visibility.Collapsed;
                grdMain.IsEnabled = false;

                new Thread(() =>
                {
                    DoStt(chunkView, finishedCallback: () =>
                    {
                        grdMain.IsEnabled = true;
                        grdWait.Visibility = Visibility.Hidden;
                        btnCancel.Visibility = Visibility.Visible;
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

            if (focus)
            {
                while (!chunkView.IsLoaded)
                    await Task.Delay(1);
                chunkView.Select();
                chunkScroller.BringIntoView();
            }

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
            var chunkViews = grdTime.Children.OfType<AudioChunkView>().Where((cv) => cv.VisualState != AudioChunkView.State.Error && string.IsNullOrEmpty(cv.SpeechText)).Take(Settings.BatchSize).ToArray();

            if (chunkViews.Count() == 0)
            {
                MessageBox.Show("All chunks have been processed.", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else // if (MessageBox.Show("Are you sure you want to do STT for all valid chunks?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                cancelFlag = false;

                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);
                waveView.Pause();

                txtWait.Text = "STT processing... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                new Thread(() =>
                {
                    foreach (var chunkView in chunkViews)
                    {
                        if (cancelFlag)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                grdMain.IsEnabled = true;
                                grdWait.Visibility = Visibility.Hidden;
                            });
                            break;
                        }

                        if (chunkView == chunkViews.Last())
                        {
                            DoStt(chunkView, true, () =>
                            {
                                grdMain.IsEnabled = true;
                                grdWait.Visibility = Visibility.Hidden;
                            });
                            break;
                        }
                        else
                        {
                            DoStt(chunkView, true);
                        }

                        Thread.Sleep(200);
                        GC.Collect();
                    }
                }).Start();
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            var chunkViews = grdTime.Children.OfType<AudioChunkView>().Where((cv) => cv.VisualState != AudioChunkView.State.Error && cv.VisualState != AudioChunkView.State.ExportSuccess && !string.IsNullOrEmpty(cv.SpeechText)).Take(Settings.BatchSize).ToArray();

            if (chunkViews.Count() == 0)
            {
                MessageBox.Show("No chunks are ready for exporting.", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else // if (MessageBox.Show("Are you sure you want to export all valid chunks?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                cancelFlag = false;

                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);
                txtWait.Text = "Exporting... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                new Thread(() =>
                {
                    foreach (var chunkView in chunkViews)
                    {
                        if (cancelFlag)
                            break;

                        DoExport(chunkView, true);

                        Thread.Sleep(100);
                        GC.Collect();
                    }

                    Dispatcher.Invoke(() =>
                    {
                        grdMain.IsEnabled = true;
                        grdWait.Visibility = Visibility.Hidden;
                        //MessageBox.Show($"Done!\nSaved in \"{txtChunkLocation.Text}\"", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
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
            Background = (SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AppBackgroundColor);
            waveView.InvalidateVisual();
            foreach (var chunkView in grdTime.Children.OfType<AudioChunkView>())
            {
                chunkView.InvalidateVisual();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (waveStream != null && MessageBox.Show("Are you sure you want to exit?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                e.Cancel = true;
            }

            cancelFlag = true;

            base.OnClosing(e);
        }

        private void btnCancelLongProcess_Click(object sender, RoutedEventArgs e)
        {
            if (cancelFlag)
            {
                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
            }
            cancelFlag = true;
        }

        private void AddChunk_Click(object sender, RoutedEventArgs e)
        {
            btnAddChunk.IsEnabled = false;

            var totLen = waveView.WaveStream.Length;
            var totTime = waveView.WaveStream.TotalTime.TotalSeconds;

            AddItemView(
                new TimeRange(
                    (waveView.SelectionStart / totLen) * totTime,
                    (waveView.SelectionEnd / totLen) * totTime
                ),
                insertIndex: grdTime.Children.Contains(currentChunkView) ? grdTime.Children.IndexOf(currentChunkView) + 1 : 0,
                focus: true
            );
        }
    }

}
