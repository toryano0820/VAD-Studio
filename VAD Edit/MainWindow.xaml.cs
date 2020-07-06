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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static VADEdit.Utils;
using System.Collections.Generic;
using System.Configuration;
using VADEdit.Types;

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
        private TimeRange currentSelection = new TimeRange();
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
                if (_Modified == value)
                    return;

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
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                MessageBox.Show(ex.GetType().ToString() + ":\n" + ex.Message + "\n" + ex.StackTrace, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            Dispatcher.UnhandledException += (o, e) =>
            {
                var ex = e.Exception;
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                MessageBox.Show(ex.GetType().ToString() + ":\n" + ex.Message + "\n" + ex.StackTrace, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
                btnCancel.Visibility = Visibility.Visible;
                e.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                var ex = e.Exception;
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                MessageBox.Show(ex.GetType().ToString() + ":\n" + ex.Message + "\n" + ex.StackTrace, "Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
                btnCancel.Visibility = Visibility.Visible;
                e.SetObserved();
            };

            SettingsWindow.ButtonClicked += (o, e) =>
            {
                if (e != SettingsWindow.Button.Cancel)
                    ApplySettings();
            };

            Background = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.AppBackgroundColor);

            SetTitle();

            Settings.Save();

            AudioChunkView.StaticFocused += delegate
            {
                btnAddChunk.IsEnabled = false;
            };

            waveView.SelectionChanged += (o, e) =>
            {
                switch (e.Event)
                {
                    case WaveSelectionChangedEventArgs.SelectionEvent.Resize:
                        if (currentChunkView != null)
                        {
                            currentChunkView.TimeRange = e.TimeRange;
                            currentChunkView.SttText = "";
                            currentChunkView.VisualState = AudioChunkView.State.Idle;
                            Modified = true;
                        }
                        break;
                    case WaveSelectionChangedEventArgs.SelectionEvent.New:
                        foreach (var view in grdTime.Children.OfType<AudioChunkView>())
                            view.Unselect();
                        waveView.Focus();
                        btnAddChunk.IsEnabled = true;
                        break;
                    case WaveSelectionChangedEventArgs.SelectionEvent.ExternalCall:
                        break;
                    default: // WaveSelectionChangedEventArgs.SelectionEvent.Hide
                        foreach (var view in grdTime.Children.OfType<AudioChunkView>())
                            view.Unselect();
                        waveView.Focus();
                        break;

                }
            };

            waveView.PlayRangeEnded += delegate
            {
                if (playingChunkView != null)
                {
                    waveView.Pause();
                    playingChunkView.PlayButtonVisibility = Visibility.Visible;
                    playingChunkView = null;
                }
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

        private async Task LoadStream(string filePath, Func<Task> beforeHideWaitPanel = null)
        {
            sourceLocation = filePath;
            var fileName = filePath.Replace(@"\", "/").Split('/').Last();
            txtCount.Text = $"Count: 0";
            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            waveStream = null;
            txtWait.Text = "Loading WAV... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            btnCancel.Visibility = Visibility.Collapsed;
            btnSplit.IsEnabled = true;
            GC.Collect();

            var success = await waveView.SetWaveStream(filePath);

            btnExportAll.IsEnabled = false;
            btnSttAll.IsEnabled = false;
            btnRemoveChunks.IsEnabled = false;
            btnClearChunks.IsEnabled = false;
            btnResetChunks.IsEnabled = false;

            if (success)
            {
                SetTitle();
                btnRevealFolder.Content = filePath;
                waveStream = waveView.WaveStream;
            }
            else
            {
                projectLocation = null;
                sourceLocation = null;
                SetTitle();
                btnRevealFolder.Content = null;

                btnSplit.IsEnabled = false;
            }

            if (beforeHideWaitPanel != null)
                await beforeHideWaitPanel.Invoke();

            grdMain.IsEnabled = true;
            grdWait.Visibility = Visibility.Hidden;
            btnCancel.Visibility = Visibility.Visible;
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
            var exitCode = -1;

            try
            {
                await Task.Run(() =>
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
                    exitCode = ffmpeg.ExitCode;
                });


                if (exitCode == 0)
                {
                    await LoadStream(wavLocation);
                    Modified = true;
                }
                else
                    throw new Exception($"ffmpeg.exe exited with code {exitCode}");
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                btnConverLoad.IsEnabled = true;
            }
        }

        private async Task SplitSilence()
        {
            await Task.Yield();

            cancelFlag = false;
            waveView.Pause();

            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            ShowSelection(TimeRange.Zero);

            txtWait.Text = "VAD processing... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            btnAddChunk.IsEnabled = false;

            var waveTotalMillis = waveStream.TotalTime.TotalMilliseconds;
            var waveData = waveView.WaveFormData;
            var waveDataLength = waveData.Count();

            GC.Collect();

            var ranges = new List<TimeRange>();

            await Task.Run(() =>
            {
                if (Settings.SplitOnSilence)
                {
                    float minVolume = Settings.MinVolume;
                    int minLength = Settings.MinLength;
                    int maxSilenceMillis = Settings.MaxSilence;

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
                                break;


                            var secDataVolume = (waveData[(int)((i / maxWidth) * waveDataLength)] / max) * 100;
                            if (i == (int)maxWidth - 1 && start != -1)
                            {
                                end = i + 1;

                                if (end - start > minLength)
                                {
                                    ranges.Add(new TimeRange(TimeSpan.FromMilliseconds(start), TimeSpan.FromMilliseconds(end)));
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
                                            ranges.Add(new TimeRange(TimeSpan.FromMilliseconds(start), TimeSpan.FromMilliseconds(end)));
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
                        Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                }
                else
                {
                    var splitLength = Settings.SplitLength;

                    try
                    {
                        var waveMillisCounter = 0;
                        while (waveMillisCounter < waveTotalMillis)
                        {
                            if (cancelFlag)
                                break;

                            var end = waveMillisCounter + splitLength;
                            if (end > waveTotalMillis)
                                end = (int)waveTotalMillis;

                            ranges.Add(new TimeRange(TimeSpan.FromMilliseconds(waveMillisCounter), TimeSpan.FromMilliseconds(end)));

                            waveMillisCounter += splitLength;
                        }
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });

            foreach (var range in ranges)
            {
                if (cancelFlag)
                    break;
                await AddChunkView(range);
                await Task.Delay(10);
            }

            if (ranges.Count > 0)
                Modified = true;

            GC.Collect();

            if (grdTime.Children.OfType<AudioChunkView>().Count() > 0)
            {
                btnExportAll.IsEnabled = true;
                btnSttAll.IsEnabled = true;
                btnRemoveChunks.IsEnabled = true;
                btnClearChunks.IsEnabled = true;
                btnResetChunks.IsEnabled = true;
            }

            grdMain.IsEnabled = true;
            grdWait.Visibility = Visibility.Hidden;
        }

        private async Task<string[]> GetNextFileNames(int count = 1)
        {
            await Task.Yield();
            var chunklocation = Path.Combine(projectLocation, "EXPORT");
            var fileNames = Directory.EnumerateFiles(chunklocation, "*.wav").Select(s => s.Replace(@"\", "/").Split('/').Last());
            int i = 0;
            var availableFileNames = new List<string>();
            for (int j = 0; j < count; j++)
            {
                while (true)
                {
                    var fileName = $"trimmed_{i++:D4}.wav";
                    if (!fileNames.Contains(fileName))
                    {
                        availableFileNames.Add(Path.Combine(chunklocation, fileName));
                        break;
                    }
                }
            }

            return availableFileNames.ToArray();
        }

        private async Task DoStt(AudioChunkView chunkView, bool batchMode = false)
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
                    if (!batchMode)
                    {
                        MessageBox.Show($"Will not process audio longer than {maxSeconds} seconds.", "STT Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
                return;
            }

            var start = waveView.PositionFromTime(TimeSpan.FromSeconds(startSecond));
            var end = waveView.PositionFromTime(TimeSpan.FromSeconds(endSecond));

            try
            {
                var oldPos = waveStream.Position;

                using (var streamBuffer = new MemoryStream())
                {

                    Func<double> alignStart = () => start / (double)waveStream.WaveFormat.BlockAlign;
                    Func<double> alignEnd = () => end / (double)waveStream.WaveFormat.BlockAlign;

                    while (alignStart() != (int)alignStart())
                    {
                        start++;
                    }

                    while (alignEnd() != (int)alignEnd())
                    {
                        end++;
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
                                if (!string.IsNullOrWhiteSpace(alternative.Transcript))
                                {
                                    chunkView.SttText = alternative.Transcript.ToLower();
                                    chunkView.SpeechText = alternative.Transcript.ToLower();
                                    chunkView.VisualState = AudioChunkView.State.STTSuccess;
                                }
                                else
                                    chunkView.VisualState = AudioChunkView.State.Error;
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
                Dispatcher.Invoke(() =>
                {
                    chunkView.VisualState = AudioChunkView.State.Error;
                });
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                Dispatcher.Invoke(() =>
                {
                    if (!batchMode)
                    {
                        MessageBox.Show(ex.Message, "STT Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
            }
        }

        private async Task DoExport(AudioChunkView[] chunkViews)
        {
            var chunkLocation = Path.Combine(projectLocation, "EXPORT");
            if (!Directory.Exists(chunkLocation))
                Directory.CreateDirectory(chunkLocation);

            var chunksCount = chunkViews.Count();

            var savePaths = await GetNextFileNames(chunksCount);

            for (int i = 0; i < chunksCount; i++)
            {
                var chunkView = chunkViews[i];
                if (cancelFlag)
                    break;

                await DoExport(chunkView, savePaths[i], true);
            }
        }


        private async Task DoExport(AudioChunkView chunkView, string savePath = null, bool batchMode = false)
        {
            chunkView.BringIntoView();
            if (string.IsNullOrWhiteSpace(chunkView.SpeechText))
            {
                chunkView.VisualState = AudioChunkView.State.Error;
                if (!batchMode)
                    MessageBox.Show("Text is empty!", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var chunkLocation = Path.Combine(projectLocation, "EXPORT");
            if (!batchMode && !Directory.Exists(chunkLocation))
                Directory.CreateDirectory(chunkLocation);

            if (string.IsNullOrWhiteSpace(savePath))
                savePath = (await GetNextFileNames(1)).First();

            var speechText = chunkView.SpeechText;
            var gSttText = chunkView.SttText;

            waveView.Pause();

            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            var oldPos = waveStream.Position;
            var chunkStart = chunkView.TimeRange.Start.TotalSeconds;
            var chunkEnd = chunkView.TimeRange.End.TotalSeconds;

            await Task.Run(async () =>
            {
                using (var writer = new WaveFileWriter(savePath, waveStream.WaveFormat))
                {
                    var start = waveView.PositionFromTime(TimeSpan.FromSeconds(chunkStart)); //(long)((chunkStart / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
                    var end = waveView.PositionFromTime(TimeSpan.FromSeconds(chunkEnd)); //(long)((chunkEnd / waveStream.TotalTime.TotalSeconds) * waveStream.Length);

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
                                await writer.WriteAsync(buffer, 0, bytesRead);
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

            });

            chunkView.VisualState = AudioChunkView.State.ExportSuccess;
            if (!batchMode)
                MessageBox.Show($"Done!\n{savePath}", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task AddChunkView(TimeRange timeRange, string gSttText = "", string speechText = "", int insertIndex = -1, bool focus = false)
        {
            await Task.Yield();

            var chunkView = new AudioChunkView()
            {
                TimeRange = timeRange,
                SttText = gSttText,
                SpeechText = speechText
            };

            await AddChunkView(chunkView, insertIndex, focus);
        }

        private async Task AddChunkView(AudioChunkView chunkView, int insertIndex = -1, bool focus = false)
        {
            await Task.Yield();

            chunkView.PlayButtonClicked += delegate
            {
                ShowSelection(chunkView.TimeRange);
                waveView.Play(chunkView.TimeRange);
                chunkView.PlayButtonVisibility = Visibility.Hidden;
                playingChunkView = chunkView;
            };

            chunkView.StopButtonClicked += delegate
            {
                waveView.Pause();
                waveStream.CurrentTime = chunkView.TimeRange.Start;
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

            chunkView.SttButtonClicked += async delegate
            {
                waveView.Pause();

                txtWait.Text = "STT processing... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                btnCancel.Visibility = Visibility.Collapsed;
                grdMain.IsEnabled = false;
                await DoStt(chunkView);
                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
                btnCancel.Visibility = Visibility.Visible;
                Modified = true;
            };

            chunkView.DuplicateButtonClicked += async delegate
            {
                await AddChunkView(chunkView.TimeRange, chunkView.SttText, chunkView.SpeechText, grdTime.Children.IndexOf(chunkView) + 1);
                Modified = true;
            };

            chunkView.ExportButtonClicked += async delegate
            {
                await DoExport(chunkView);
                Modified = true;
            };

            chunkView.DeleteButtonClicked += delegate
            {
                ShowSelection(TimeRange.Zero);
                var index = grdTime.Children.IndexOf(chunkView);
                grdTime.Children.Remove(chunkView);
                var views = grdTime.Children.OfType<AudioChunkView>();
                if (views.Count() > 0)
                {
                    foreach (var view in views)
                        view.Unselect();
                }
                else
                {
                    btnExportAll.IsEnabled = false;
                    btnSttAll.IsEnabled = false;
                    btnRemoveChunks.IsEnabled = false;
                    btnClearChunks.IsEnabled = false;
                    btnResetChunks.IsEnabled = false;
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

            chunkView.ResetButtonClicked += delegate
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
                    await Task.Delay(10);
                chunkView.Select();
                chunkScroller.BringIntoView();
            }

            txtCount.Text = $"Count: {grdTime.Children.Count}";
            chunkView.UpdateVisuals();
        }

        private void ShowSelection(TimeRange range)
        {
            if (range == currentSelection && waveView.Player.PlaybackState == PlaybackState.Playing)
                return;

            currentSelection = range;
            waveView.ShowSelection(range);
        }

        private async void Split_Click(object sender, RoutedEventArgs e)
        {
            if (grdTime.Children.Count > 0 && MessageBox.Show("Existing chunks will be overwritten.\nContinue?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                return;
            await SplitSilence();
        }

        private async void SttAll_Click(object sender, RoutedEventArgs e)
        {
            var chunkViews = grdTime.Children.OfType<AudioChunkView>().Where((cv) => cv.VisualState != AudioChunkView.State.Error && string.IsNullOrEmpty(cv.SpeechText)).Take(Settings.BatchSize).ToArray();

            if (chunkViews.Count() == 0)
            {
                MessageBox.Show("All chunks have been processed.", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else // if (MessageBox.Show("Are you sure you want to do STT for all valid chunks?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                cancelFlag = false;

                ShowSelection(TimeRange.Zero);
                waveView.Pause();

                txtWait.Text = "STT processing... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                foreach (var chunkView in chunkViews)
                {
                    if (cancelFlag)
                        break;

                    await DoStt(chunkView, true);
                    Modified = true;
                }
                grdWait.Visibility = Visibility.Hidden;
                grdMain.IsEnabled = true;
                GC.Collect();
            }
        }

        private async void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            var chunkViews = grdTime.Children.OfType<AudioChunkView>().Where((cv) => cv.VisualState != AudioChunkView.State.Error && cv.VisualState != AudioChunkView.State.ExportSuccess && !string.IsNullOrEmpty(cv.SpeechText)).Take(Settings.BatchSize).ToArray();

            if (chunkViews.Count() == 0)
            {
                MessageBox.Show("No chunks are ready for exporting.", "Data Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else // if (MessageBox.Show("Are you sure you want to export all valid chunks?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                cancelFlag = false;

                ShowSelection(TimeRange.Zero);
                txtWait.Text = "Exporting... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                btnCancel.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                await DoExport(chunkViews);

                grdMain.IsEnabled = true;
                grdWait.Visibility = Visibility.Hidden;
                Modified = true;
            }
        }

        private void ApplySettings()
        {
            SetTitle();
            Background = (SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.AppBackgroundColor);
            waveView.UpdateVisuals();
            foreach (var chunkView in grdTime.Children.OfType<AudioChunkView>())
            {
                chunkView.UpdateVisuals();
            }
        }

        private void Options_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow.Show();
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
                        break;
                    case MessageBoxResult.Yes:
                        SaveProject();
                        cancelFlag = true;
                        break;
                    default:
                        cancelFlag = true;
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

        private async void AddChunk_Click(object sender, RoutedEventArgs e)
        {
            btnAddChunk.IsEnabled = false;

            await AddChunkView(
                new TimeRange(
                    waveView.TimeFromPosition(waveView.SelectionStart),
                    waveView.TimeFromPosition(waveView.SelectionEnd)
                ),
                insertIndex: grdTime.Children.Contains(currentChunkView) ? grdTime.Children.IndexOf(currentChunkView) + 1 : 0,
                focus: true
            );

            Modified = true;
        }

        private void RevealFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("Explorer.exe", projectLocation);
        }

        private async void NewProject_Click(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (grdWait.IsVisible)
                return;
            waveView.Pause();
            var pi = NewProjectWindow.GetNewProjectInfo();
            if (pi != null)
            {
                if (Modified)
                {
                    var msgResult = MessageBox.Show("There are unsaved changes.\nDo you want to save them before creating new project?", "Operation Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    switch (msgResult)
                    {
                        case MessageBoxResult.Cancel:
                            return;
                        case MessageBoxResult.Yes:
                            SaveProject();
                            cancelFlag = true;
                            break;
                        default:
                            cancelFlag = true;
                            break;
                    }

                    Modified = false;
                }
                projectLocation = Path.Combine(pi.Value.ProjectBaseLocation, pi.Value.ProjectName);

                projectFileLocation = Path.Combine(projectLocation, pi.Value.ProjectName + ".vadedit");
                sourceLocation = pi.Value.MediaLocation;
                Directory.CreateDirectory(projectLocation);
                projectConfig = new ProjectConfig(projectFileLocation, pi.Value.ProjectName);
                await ConvertLoad();
                btnRevealFolder.IsEnabled = true;
                Modified = true;
            }
        }

        private async void OpenProject_Click(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (grdWait.IsVisible)
                return;
            if (Modified)
            {
                var msgResult = MessageBox.Show("There are unsaved changes.\nDo you want to save them before opening another project?", "Operation Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                switch (msgResult)
                {
                    case MessageBoxResult.Cancel:
                        return;
                    case MessageBoxResult.Yes:
                        SaveProject();
                        cancelFlag = true;
                        break;
                    default:
                        cancelFlag = true;
                        break;
                }

                Modified = false;
            }

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
                projectFileLocation = Path.Combine(projectLocation, projectConfig.GetProjectName() + ".vadedit");
                wavLocation = await projectConfig.GetWavPath();
                if (!string.IsNullOrWhiteSpace(wavLocation))
                {
                    await LoadStream(wavLocation, async () =>
                    {
                        txtWait.Text = "Loading Chunks... Please wait...";
                        grdTime.Children.Clear();
                        var projChunks = projectConfig.GetAudioChunkViews();
                        while (projChunks.MoveNext())
                        {
                            await AddChunkView(projChunks.Current);
                            await Task.Delay(10);
                        }

                        if (grdTime.Children.OfType<AudioChunkView>().Count() > 0)
                        {
                            btnExportAll.IsEnabled = true;
                            btnSttAll.IsEnabled = true;
                            btnRemoveChunks.IsEnabled = true;
                            btnClearChunks.IsEnabled = true;
                            btnResetChunks.IsEnabled = true;
                        }
                        btnRevealFolder.IsEnabled = true;
                    });
                }
            }
            dlg.Dispose();
            GC.Collect();
        }

        private async void SaveProject()
        {
            if (grdWait.IsVisible)
                return;
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

        private void RemoveChunks_Click(object sender, RoutedEventArgs e)
        {
            var msgResult = MessageBox.Show("Existing chunks will be removed.\nContinue?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            switch (msgResult)
            {
                case MessageBoxResult.No:
                    return;
                default:
                    break;
            }

            grdTime.Children.Clear();
            txtCount.Text = $"Count: 0";

            GC.Collect();

            Modified = true;
        }

        private void ClearChunks_Click(object sender, RoutedEventArgs e)
        {
            var msgResult = MessageBox.Show("Existing chunks will be cleared.\nContinue?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            switch (msgResult)
            {
                case MessageBoxResult.No:
                    return;
                default:
                    break;
            }

            foreach (var view in grdTime.Children.OfType<AudioChunkView>())
            {
                view.SpeechText = "";
                view.SttText = "";
                view.VisualState = AudioChunkView.State.Idle;
            }

            Modified = true;
        }

        private void ResetChunks_Click(object sender, RoutedEventArgs e)
        {
            var msgResult = MessageBox.Show("Existing chunks will be reset to Idle.\nContinue?", "Operation Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            switch (msgResult)
            {
                case MessageBoxResult.No:
                    return;
                default:
                    break;
            }

            foreach (var view in grdTime.Children.OfType<AudioChunkView>())
            {
                view.VisualState = AudioChunkView.State.Idle;
            }

            Modified = true;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

}
