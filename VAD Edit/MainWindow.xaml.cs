#if GOOGLE_STT
using Google.Cloud.Speech.V1;
#else
using System.Net.Http;
#endif
using NAudio.Wave;
using Newtonsoft.Json;
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
using System.Collections.Generic;
using System.Configuration;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private AudioChunkView currentChunkView = null;
        private AudioChunkView playingChunkView = null;
#if GOOGLE_STT
        private SpeechClient speechClient = null;
#else
        private HttpClient httpClient = null;
        private Dictionary<string, string> trtisLanguages = new Dictionary<string, string>()
        {
            {"en-US", "en"},
            {"fil-PH", "tl"},
            {"th-TH", "th"},
            {"zh-TW", "tw"},
        };
#endif
        private WaveStream waveStream = null;

        private string projectLocation = null;
        private string projectFileLocation = null;
        private string sourceLocation = null;
        private string wavLocation = null;
        private bool cancelFlag = true;
        private ProjectConfig projectConfig = null;
        private bool _Modified = false;
        private bool Modified
        {
            get
            {
                return _Modified;
            }
            set
            {
                _Modified = value;
                if (value)
                {
                    statusBar.Background = Brushes.OrangeRed;
                }
                else
                {
                    statusBar.Background = Brushes.DodgerBlue;
                }

                btnSaveProject.IsEnabled = value;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            Application.Current.MainWindow = this;

            AppDomain.CurrentDomain.UnhandledException += (o, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show(ex.GetType().ToString() + ":\n" + ex.Message + "\n" + ex.StackTrace, "Unhandled Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
            };

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
                    currentChunkView.SttText = null;
                    currentChunkView.VisualState = AudioChunkView.State.Idle;
                    Modified = true;
                }
                //btnAddChunk.IsEnabled = false;
            };

            waveView.NewSelection += delegate
            {
                btnAddChunk.IsEnabled = true;
            };

            Loaded += delegate
            {
                //this.EnableBlur();
                Modified = Modified;
            };

        }

        internal void RenewSpeechClient()
        {
#if GOOGLE_STT
            speechClient = SpeechClient.Create();
#else
            httpClient = new HttpClient();
#endif
            GC.Collect();
        }

        private void SetTitle()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = $"{version.Major}.{version.Minor}.{version.Build}";
#if GOOGLE_STT
            versionString += "_Google-STT";
#else
            versionString += "_NERA-STT";
#endif
            Title = $"VAD Edit v{versionString} [{Settings.LanguageCode}]{(string.IsNullOrWhiteSpace(projectLocation) ? "" : $" [{projectLocation}]")}";
        }

        private void Play_Clicked(object sender, RoutedEventArgs e)
        {
            waveView.Play();
        }

        private async Task LoadStream(string filePath, Func<Task> beforeHideWaitPanel=null)
        {
            sourceLocation = filePath;
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
            await waveView.SetWaveStream(new NAudio.Wave.WaveFileReader(filePath), async (success) =>
            {
                btnExportAll.IsEnabled = false;
                btnSttAll.IsEnabled = false;

                if (success)
                {
                    SetTitle();

                    btnPause.IsEnabled = true;
                    btnPlay.IsEnabled = true;
                    waveStream = waveView.WaveStream;
                }
                else
                {
                    projectLocation = null;
                    sourceLocation = null;
                    SetTitle();

                    btnPause.IsEnabled = false;
                    btnPlay.IsEnabled = false;
                    btnSplit.IsEnabled = false;
                    btnSttAll.IsEnabled = false;
                    btnExportAll.IsEnabled = false;
                }

                if (beforeHideWaitPanel != null)
                    await beforeHideWaitPanel.Invoke();

                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
                btnCancel.Visibility = Visibility.Visible;
            });
        }

        private async void ConvertLoad_Click(object sender, RoutedEventArgs args)
        {
            waveView.Pause();

            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "Media Files|*.wav;*.mp3;*.flac;*.mp4;*.avi;*.flv",
                InitialDirectory = Settings.LastMediaLocation,
                Title = "Load Media"
            };
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                sourceLocation = dlg.FileName;
                await ConvertLoad();
            }
            dlg.Dispose();
            GC.Collect();
        }

        private async Task ConvertLoad()
        {
            txtCount.Text = $"Count: 0";
            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            txtWait.Text = "Converting to WAV... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            btnCancel.Visibility = Visibility.Collapsed;
            grdMain.IsEnabled = false;
            btnConverLoad.IsEnabled = false;
            btnAddChunk.IsEnabled = false;

            await Task.Delay(100);

            Settings.LastMediaLocation = Path.GetDirectoryName(sourceLocation);
            Settings.Save();
            var baseFileName = Path.GetFileNameWithoutExtension(sourceLocation);
            wavLocation = Path.Combine(projectLocation, baseFileName);
            while (File.Exists(wavLocation + ".wav"))
            {
                if (Regex.IsMatch(wavLocation, @"_\d+$"))
                    wavLocation = Regex.Replace(wavLocation, @"_\d+$", "_" + (int.Parse(wavLocation.Split('_').Last()) + 1).ToString());
                else
                    wavLocation = wavLocation + "_1";
            }
            GC.Collect();

            wavLocation = wavLocation + ".wav";

            var thread = new Thread(() =>
            {
                try
                {
                    var ffmpeg = new Process();
#if false //DEBUG
                        ffmpeg.StartInfo.FileName = "cmd.exe";
                        ffmpeg.StartInfo.Arguments = $"/K ffmpeg.exe -y -i \"{dlg.FileName}\" -c copy -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{chunkSaveLocation}.wav\"";
                        ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
#else
                    ffmpeg.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe ");
                    ffmpeg.StartInfo.Arguments = $"-y -i \"{sourceLocation}\" -c copy -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{wavLocation}\"";
                    ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif
                    ffmpeg.StartInfo.UseShellExecute = true;
                    ffmpeg.Start();
                    ffmpeg.WaitForExit();

                    if (ffmpeg.ExitCode == 0)
                    {
                        Dispatcher.Invoke(async () =>
                        {
                            await LoadStream(wavLocation);
                            Modified = true;
                        });
                    }
                    else
                        throw new Exception($"ffmpeg.exe exited with code {ffmpeg.ExitCode}");
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    //File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                    Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        btnConverLoad.IsEnabled = true;
                    });
                }
            });
            thread.Start();

            while (thread.IsAlive)
                await Task.Delay(10);
        }

        private void SplitSilence()
        {
            cancelFlag = false;
            waveView.Pause();

            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero));

            txtWait.Text = "VAD processing... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            btnAddChunk.IsEnabled = false;

            var waveTotalMillis = waveStream.TotalTime.TotalMilliseconds;
            var waveData = waveView.WaveFormData;
            var waveDataLength = waveData.Count();

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
                                    Thread.Sleep(5);
                                    Dispatcher.Invoke(() =>
                                    {
                                        AddChunkView(new TimeRange(TimeSpan.FromMilliseconds(start), TimeSpan.FromMilliseconds(end)));
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
                                            Thread.Sleep(5);
                                            Dispatcher.Invoke(() =>
                                            {
                                                AddChunkView(new TimeRange(TimeSpan.FromMilliseconds(start), TimeSpan.FromMilliseconds(end)));
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
                                AddChunkView(new TimeRange(TimeSpan.FromMilliseconds(waveMillisCounter), TimeSpan.FromMilliseconds(end)));
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
            var chunkSaveLocation = Dispatcher.Invoke(() => projectLocation);
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

        private async void DoStt(AudioChunkView chunkView, bool suppressErrorDialogs = false, Action finishedCallback = null)
        {
            Dispatcher.Invoke(() =>
            {
                chunkView.BringIntoView();
                if (!string.IsNullOrWhiteSpace(chunkView.SpeechText))
                    return;
            });

            var startSecond = Dispatcher.Invoke(() => chunkView.TimeRange.Start.TotalSeconds);
            var endSecond = Dispatcher.Invoke(() => chunkView.TimeRange.End.TotalSeconds);
            var maxSeconds = int.Parse(ConfigurationManager.AppSettings.Get("MaxSeconds"));

            if (endSecond - startSecond > maxSeconds)
            {
                Dispatcher.Invoke(() =>
                {
                    chunkView.VisualState = AudioChunkView.State.Error;
                    Logger.Log($"Processing STT on audio longer than {maxSeconds} seconds:\n    {sourceLocation}: {chunkView.TimeRange}", Logger.Type.Warn);
                    if (!suppressErrorDialogs)
                    {
                        MessageBox.Show($"Will not process audio longer than {maxSeconds} seconds.", "Operation Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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

#if GOOGLE_STT
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
#else
                    var httpPayload = new MultipartFormDataContent();
                    httpPayload.Add(new StreamContent(streamBuffer), "raw", "in.wav");
                    var httpRsp = await httpClient.PostAsync($"{Settings.KinpoSttInferHost}/api/sttinfer/{trtisLanguages[Settings.LanguageCode]}", httpPayload);
                    var response = new
                    {
                        Results = new[] {
                            new {
                                Alternatives = new[] {
                                    new {
                                        Transcript =  (string)(JsonConvert.DeserializeObject(await httpRsp.Content.ReadAsStringAsync()) as dynamic).prediction
                                    }
                                }
                            }
                        }
                    };

#endif

                    foreach (var result in response.Results)
                    {
                        foreach (var alternative in result.Alternatives)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                chunkView.SttText = alternative.Transcript.ToLower();
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

            var chunkLocation = Dispatcher.Invoke(() => projectLocation);
            var speechText = Dispatcher.Invoke(() => chunkView.SpeechText);
            var gSttText = Dispatcher.Invoke(() => chunkView.SttText);

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

        private async void AddChunkView(TimeRange timeRange, string gSttText = null, string speechText = null, int insertIndex = -1, bool focus = false)
        {
            var chunkView = new AudioChunkView()
            {
                TimeRange = timeRange,
                SttText = gSttText,
                SpeechText = speechText
            };

            AddChunkView(chunkView, insertIndex, focus);

            await Task.Yield();
        }

        private async void AddChunkView(AudioChunkView chunkView, int insertIndex = -1, bool focus = false)
        {
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
                DoStt(chunkView, finishedCallback: () =>
                {
                    grdMain.IsEnabled = true;
                    grdWait.Visibility = Visibility.Hidden;
                    btnCancel.Visibility = Visibility.Visible;
                    Modified = true;
                });
            };

            chunkView.DuplicateButtonClicked += delegate
            {
                AddChunkView(chunkView.TimeRange, chunkView.SttText, chunkView.SpeechText, grdTime.Children.IndexOf(chunkView) + 1);
                Modified = true;
            };

            chunkView.ExportButtonClicked += delegate
            {
                DoExport(chunkView);

                Modified = true;
            };

            chunkView.DeleteButtonClicked += delegate
            {
                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero));
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
                Modified = true;
            };

            chunkView.TextChanged += delegate
            {
                Modified = true;
            };

            chunkView.IndexChanged += delegate
            {
                Modified = true;
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

            chunkView.UpdateVisuals();

            txtCount.Text = $"Count: {grdTime.Children.Count}";
        }

        private void ShowSelection(TimeRange range)
        {
            var totalMs = waveStream.TotalTime.TotalMilliseconds;
            var lenWs = waveStream.Length;
            waveView.SelectionStart = (range.Start.TotalMilliseconds / totalMs) * lenWs;
            waveView.SelectionEnd = (range.End.TotalMilliseconds / totalMs) * lenWs;
            if (waveView.Player.PlaybackState != PlaybackState.Playing)
            {
                waveStream.Position = (long)waveView.SelectionStart;
                waveView.RenderPositionLine(true);
            }
            waveView.InvalidateVisual();
        }

        private void Split_Click(object sender, RoutedEventArgs e)
        {
            if (grdTime.Children.Count > 0 && MessageBox.Show("Existing chunks will be overwritten.\nContinue?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                return;
            SplitSilence();
            Modified = true;
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            waveView.Pause();
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

                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero));
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
                                Modified = true;
                            });
                            break;
                        }

                        DoStt(chunkView, true, () =>
                        {
                            if (chunkViews.Count(cv => cv.VisualState == AudioChunkView.State.Idle) == 0)
                            {
                                grdWait.Visibility = Visibility.Hidden;
                                grdMain.IsEnabled = true;
                            }
                        });
#if GOOGLE_STT
                        //Thread.Sleep(200);
#else
                        Thread.Sleep(50);
#endif
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

                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero));
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
                        Modified = true;
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
            try
            {
                SettingsWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Background = (SolidColorBrush)(new BrushConverter()).ConvertFromString(Settings.AppBackgroundColor);
            waveView.InvalidateVisual();
            foreach (var chunkView in grdTime.Children.OfType<AudioChunkView>())
            {
                chunkView.UpdateVisuals();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Modified)
            {
                var msgResult = MessageBox.Show("There are unsaved changes.\nDo you want to save them before exiting?", "Operation Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                switch (msgResult)
                {
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        cancelFlag = true;
                        break;
                    case MessageBoxResult.Yes:
                        SaveProject();
                        break;
                    case MessageBoxResult.No:
                        break;
                    default:
                        break;
                }
            }

            base.OnClosing(e);
        }

        private void CancelLongProcess_Click(object sender, RoutedEventArgs e)
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

            AddChunkView(
                new TimeRange(
                    (waveView.SelectionStart / totLen) * totTime,
                    (waveView.SelectionEnd / totLen) * totTime
                ),
                insertIndex: grdTime.Children.Contains(currentChunkView) ? grdTime.Children.IndexOf(currentChunkView) + 1 : 0,
                focus: true
            );
        }

        private void RevealFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("Explorer.exe", projectLocation);
        }

        private async void NewProject_Click(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            waveView.Pause();
            var pi = NewProjectWindow.GetNewProjectInfo();
            if (pi != null)
            {
                projectLocation = Path.Combine(pi.Value.ProjectBaseLocation, pi.Value.ProjectName);
                projectFileLocation = Path.Combine(projectLocation, "Project.vadedit");
                sourceLocation = pi.Value.MediaLocation;
                Directory.CreateDirectory(projectLocation);
                projectConfig = new ProjectConfig(projectFileLocation, pi.Value.ProjectName);
                await ConvertLoad();
                btnRevealFolder.IsEnabled = true;
                btnSaveProject.IsEnabled = true;
                Modified = true;
            }
        }

        private async void OpenProject_Click(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "VAD Project|*.vadedit",
                InitialDirectory = Settings.ProjectBaseLocation,
                Title = "Open Project"
            };
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                projectLocation = Path.GetDirectoryName(dlg.FileName);
                projectConfig = new ProjectConfig(dlg.FileName);
                wavLocation = Path.Combine(projectLocation, await projectConfig.GetWavPath());
                if (!string.IsNullOrWhiteSpace(wavLocation))
                {
                    await LoadStream(wavLocation, async () =>
                    {
                        txtWait.Text = "Loading Chunks... Please wait...";
                        grdTime.Children.Clear();
                        var projChunks = projectConfig.GetAudioChunkViews();
                        while (projChunks.MoveNext())
                        {
                            AddChunkView(projChunks.Current);
                            await Task.Delay(1);
                        }
                        await Task.Delay(100);
                        Modified = false;
                    });
                    btnRevealFolder.IsEnabled = true;
                }
            }
            dlg.Dispose();
            GC.Collect();
        }

        private async void SaveProject()
        {
            if (Modified && projectConfig != null)
            {
                txtWait.Text = "Saving Project... Please wait...";
                btnCancel.Visibility = Visibility.Collapsed;
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;
                await Task.Delay(100);
                await projectConfig.SetWavPath(wavLocation);
                await projectConfig.SetAudioChunkViews(grdTime.Children.OfType<AudioChunkView>().ToList());
                await Task.Delay(100);
                Modified = false;
                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
                btnCancel.Visibility = Visibility.Visible;
            }
        }

        private void SaveProject_Click(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            SaveProject();
        }
    }

}
