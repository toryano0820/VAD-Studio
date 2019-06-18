using Google.Cloud.Speech.V1;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace VADEdit
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string OutputBasePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "sentence_extractor");

        AudioChunkView currentChunkView;
        AudioChunkView playingChunkView;
        SpeechClient speechClient;

        public MainWindow()
        {
            InitializeComponent();
            SetTitleSuffix();

            speechClient = SpeechClient.Create();


            // SAMPLE
            ConfigLoader.Load("sample.vad");
            var c = new Models.INIConfig(Models.INIConfig.ConfigType.Section, "general");
            Console.WriteLine(string.Join("\n", c.Keys));
            // ~SAMPLE

            txtMaxSilence.PreviewTextInput += (o, e) =>
            {
                var textBox = o as TextBox;
                var proposedText = textBox.Text;
                proposedText = proposedText.Remove(textBox.SelectionStart, textBox.SelectionLength);
                proposedText = proposedText.Insert(textBox.SelectionStart, e.Text);

                if (!int.TryParse(proposedText, out int res))
                {
                    e.Handled = true;
                }
            };

            txtMinVolume.PreviewTextInput += (o, e) =>
            {
                var textBox = o as TextBox;
                var proposedText = textBox.Text;
                proposedText = proposedText.Remove(textBox.SelectionStart, textBox.SelectionLength);
                proposedText = proposedText.Insert(textBox.SelectionStart, e.Text);

                if (!float.TryParse(proposedText, out float res) || !(res >= 0 && res <= 100))
                {
                    e.Handled = true;
                }
            };

            txtMinLength.PreviewTextInput += (o, e) =>
            {
                var textBox = o as TextBox;
                var proposedText = textBox.Text;
                proposedText = proposedText.Remove(textBox.SelectionStart, textBox.SelectionLength);
                proposedText = proposedText.Insert(textBox.SelectionStart, e.Text);

                if (!float.TryParse(proposedText, out float res) || res < 0)
                {
                    e.Handled = true;
                }
            };

            waveView.SelectionAdjusted += delegate
            {
                if (currentChunkView != null)
                {
                    currentChunkView.TimeRange = new TimeRange()
                    {
                        Start = TimeSpan.FromSeconds((waveView.SelectionStart / waveView.WaveStream.Length) * waveView.WaveStream.TotalTime.TotalSeconds),
                        End = TimeSpan.FromSeconds((waveView.SelectionEnd / waveView.WaveStream.Length) * waveView.WaveStream.TotalTime.TotalSeconds)
                    };
                    waveView.PlayRangeEnd = currentChunkView.TimeRange.End;
                }
            };

            Loaded += delegate
            {
                //this.EnableBlur();
            };
        }

        private void SetTitleSuffix(string suffix = null)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"VAD Edit v{version.Major}.{version.Minor}.{version.Build}{(string.IsNullOrWhiteSpace(suffix) ? "" : $" [{suffix}]")}";
        }

        private void Play_Clicked(object sender, RoutedEventArgs e)
        {
            waveView.Play();
        }

        private void Load_Clicked(object sender, RoutedEventArgs e)
        {
            Load();
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
                txtCount.Text = $"Count: 0";
                grdTime.Children.Clear();
                currentChunkView = null;
                playingChunkView = null;
                txtWait.Text = "Loading WAV... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;
                GC.Collect();
                //await waveView.SetWaveStreamAsync(new NAudio.Wave.WaveFileReader(dlg.FileName));
                waveView.SetWaveStream(new NAudio.Wave.WaveFileReader(dlg.FileName), (success) =>
                {
                    if (success)
                    {
                        txtChunkLocation.Text = System.IO.Path.Combine(OutputBasePath, dlg.SafeFileName.Substring(0, dlg.SafeFileName.Length - 4));
                        Directory.CreateDirectory(txtChunkLocation.Text);
                        SetTitleSuffix(dlg.FileName);

                        btnPause.IsEnabled = true;
                        btnPlay.IsEnabled = true;
                        btnSplit.IsEnabled = true;
                        btnExportAll.IsEnabled = true;
                        btnSTTAll.IsEnabled = true;
                        txtChunkLocation.IsEnabled = true;
                    }
                    else
                    {
                        txtChunkLocation.Text = null;
                        SetTitleSuffix();

                        btnPause.IsEnabled = false;
                        btnPlay.IsEnabled = false;
                        btnSplit.IsEnabled = false;
                        btnExportAll.IsEnabled = false;
                        btnSTTAll.IsEnabled = false;
                        txtChunkLocation.IsEnabled = false;
                    }

                    grdMain.IsEnabled = true;
                    grdWait.Visibility = Visibility.Hidden;
                });
            }
            dlg.Dispose();
        }

        private void ConvertLoad()
        {
            waveView.Pause();

            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Filter = "Media Files|*.wav;*.mp3;*.flac;*.mp4;*.avi"
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
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        txtWait.Text = "Loading WAV... Please wait...";
                        waveView.SetWaveStream(new NAudio.Wave.WaveFileReader(chunkSaveLocation + ".wav"), (success) =>
                        {
                            if (success)
                            {
                                txtChunkLocation.Text = chunkSaveLocation;
                                Directory.CreateDirectory(chunkSaveLocation);
                                SetTitleSuffix(chunkSaveLocation + ".wav");

                                btnPause.IsEnabled = true;
                                btnPlay.IsEnabled = true;
                                btnSplit.IsEnabled = true;
                                btnExportAll.IsEnabled = true;
                                btnSTTAll.IsEnabled = true;
                                txtChunkLocation.IsEnabled = true;
                            }
                            else
                            {
                                txtChunkLocation.Text = null;
                                SetTitleSuffix();

                                btnPause.IsEnabled = false;
                                btnPlay.IsEnabled = false;
                                btnSplit.IsEnabled = false;
                                btnExportAll.IsEnabled = false;
                                btnSTTAll.IsEnabled = false;
                                txtChunkLocation.IsEnabled = false;
                            }

                            grdMain.IsEnabled = true;
                            grdWait.Visibility = Visibility.Hidden;
                        });
                    });
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
            longSentenceCounter = 0;

            float minVolume = float.Parse(txtMinVolume.Text);
            float minLength = float.Parse(txtMinLength.Text);
            int maxSilenceMillis = int.Parse(txtMaxSilence.Text);

            txtWait.Text = "VAD processing... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            var waveTotalMillis = waveView.WaveStream.TotalTime.TotalMilliseconds;
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
                                    AddItemView(start, end);
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
                                            AddItemView(start, end);
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
                    grdMain.IsEnabled = true;
                    grdWait.Visibility = Visibility.Hidden;
                });
            }).Start();
        }

        int longSentenceCounter = 0;
        private void AddItemView(double msStart, double msEnd)
        {
            var chunkView = new AudioChunkView()
            {
                TimeRange = new TimeRange()
                {
                    Start = TimeSpan.FromMilliseconds(msStart),
                    End = TimeSpan.FromMilliseconds(msEnd)
                }
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
                waveView.WaveStream.CurrentTime = chunkView.TimeRange.Start;
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

                if (!Utils.IsNetworkAvailable())
                {
                    MessageBox.Show("Please check internet connection.", "STT Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var waveStream = waveView.WaveStream;
                var savePath = System.IO.Path.Combine(txtChunkLocation.Text, "temp.wav");
                var start = (long)((chunkView.TimeRange.Start.TotalSeconds / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
                var end = (long)((chunkView.TimeRange.End.TotalSeconds / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
                txtWait.Text = "STT processing... Please wait...";
                grdWait.Visibility = Visibility.Visible;
                grdMain.IsEnabled = false;

                new Thread(() =>
                {
                    try
                    {
                        if (System.IO.File.Exists(savePath))
                            System.IO.File.Delete(savePath);

                        var oldPos = waveStream.Position;

                        using (var writer = new WaveFileWriter(savePath, waveStream.WaveFormat))
                        {

                            Func<double> alignStart = () => start / (double)waveStream.WaveFormat.BlockAlign;
                            Func<double> alignEnd = () => end / (double)waveStream.WaveFormat.BlockAlign;

                            while (alignStart() != (int)alignStart())
                                start += 1;

                            while (alignEnd() != (int)alignEnd())
                                end += 1;

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

                        var response = speechClient.Recognize(new RecognitionConfig()
                        {
                            Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                            SampleRateHertz = waveStream.WaveFormat.SampleRate,
                            LanguageCode = "fil-PH",
                        }, RecognitionAudio.FromFile(savePath));

                        foreach (var result in response.Results)
                        {
                            foreach (var alternative in result.Alternatives)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    chunkView.SpeechText = alternative.Transcript;
                                });
                                break;
                            }
                            break;
                        }

                        File.Delete(savePath);
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        File.AppendAllText("error.log", $"{DateTime.Now.ToString("yyyyMMddHHmmss")} [ERROR]: {ex.Message}:\n{ex.StackTrace}\n");
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        grdMain.IsEnabled = true;
                        grdWait.Visibility = Visibility.Hidden;
                    });
                }).Start();
            };

            chunkView.ExportButtonClicked += delegate
            {
                if (!string.IsNullOrWhiteSpace(chunkView.SpeechText))
                {
                    waveView.Pause();
                    var savePath = System.IO.Path.Combine(txtChunkLocation.Text, chunkView.SpeechText + ".wav");
                    var isRetry = false;

                    retry:

                    try
                    {
                        if (System.IO.File.Exists(savePath))
                            System.IO.File.Delete(savePath);

                        var oldPos = waveView.WaveStream.Position;

                        using (var writer = new WaveFileWriter(savePath, waveView.WaveStream.WaveFormat))
                        {
                            var start = (long)((chunkView.TimeRange.Start.TotalSeconds / waveView.WaveStream.TotalTime.TotalSeconds) * waveView.WaveStream.Length);
                            var end = (long)((chunkView.TimeRange.End.TotalSeconds / waveView.WaveStream.TotalTime.TotalSeconds) * waveView.WaveStream.Length);

                            Func<double> alignStart = () => start / (double)waveView.WaveStream.WaveFormat.BlockAlign;
                            Func<double> alignEnd = () => end / (double)waveView.WaveStream.WaveFormat.BlockAlign;

                            while (alignStart() != (int)alignStart())
                                start += 1;

                            while (alignEnd() != (int)alignEnd())
                                end += 1;

                            waveView.WaveStream.Position = start;
                            byte[] buffer = new byte[1024];
                            while (waveView.WaveStream.Position < end)
                            {
                                int bytesRequired = (int)(end - waveView.WaveStream.Position);
                                if (bytesRequired > 0)
                                {
                                    int bytesToRead = Math.Min(bytesRequired, buffer.Length);
                                    int bytesRead = waveView.WaveStream.Read(buffer, 0, bytesToRead);
                                    if (bytesRead > 0)
                                    {
                                        writer.Write(buffer, 0, bytesRead);
                                    }
                                }
                            }
                        }
                        waveView.WaveStream.Position = oldPos;
                        if (isRetry)
                        {
                            File.WriteAllText(savePath + ".txt", chunkView.SpeechText);
                        }

                        MessageBox.Show($"Done!\n{savePath}", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (PathTooLongException)
                    {
                        isRetry = true;
                        savePath = System.IO.Path.Combine(txtChunkLocation.Text, $"long_sentence_{++longSentenceCounter:D4}.wav");
                        goto retry;
                    }
                }
                else
                    MessageBox.Show("Text is empty!", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            chunkView.DeleteButtonClicked += delegate
            {
                grdTime.Children.Remove(chunkView);
                txtCount.Text = $"Count: {grdTime.Children.Count}";
                ShowSelection(new TimeRange(TimeSpan.Zero, TimeSpan.Zero), false);
            };

            grdTime.Children.Add(chunkView);

            txtCount.Text = $"Count: {grdTime.Children.Count}";
        }

        private void ShowSelection(TimeRange range, bool allowSelectionChange = true)
        {
            waveView.AllowSelectionChange = allowSelectionChange;
            waveView.SelectionStart = (range.Start.TotalMilliseconds / waveView.WaveStream.TotalTime.TotalMilliseconds) * waveView.WaveStream.Length;
            waveView.SelectionEnd = (range.End.TotalMilliseconds / waveView.WaveStream.TotalTime.TotalMilliseconds) * waveView.WaveStream.Length;
            if (waveView.Player.PlaybackState != PlaybackState.Playing)
            {
                waveView.WaveStream.Position = (int)waveView.SelectionStart;
                waveView.RenderPositionLine(true);
            }
            waveView.InvalidateVisual();
        }

        private void Split_Click(object sender, RoutedEventArgs e)
        {
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


        private void STTAll_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {

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
    }

}
