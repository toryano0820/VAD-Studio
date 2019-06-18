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
        WaveStream waveStream;

        public MainWindow()
        {
            InitializeComponent();
            SetTitleSuffix();

            speechClient = SpeechClient.Create();

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

        private void LoadStream(string filePath)
        {
            var fileName = filePath.Replace(@"\", "/").Split('/').Last();
            txtCount.Text = $"Count: 0";
            grdTime.Children.Clear();
            currentChunkView = null;
            playingChunkView = null;
            txtWait.Text = "Loading WAV... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;
            GC.Collect();
            //await waveView.SetWaveStreamAsync(new NAudio.Wave.WaveFileReader(dlg.FileName));
            waveView.SetWaveStream(new NAudio.Wave.WaveFileReader(filePath), (success) =>
            {
                if (success)
                {
                    txtChunkLocation.Text = System.IO.Path.Combine(OutputBasePath, fileName.Substring(0, fileName.Length - 4));
                    Directory.CreateDirectory(txtChunkLocation.Text);
                    SetTitleSuffix(filePath);

                    btnPause.IsEnabled = true;
                    btnPlay.IsEnabled = true;
                    btnSplit.IsEnabled = true;
                    btnExportAll.IsEnabled = true;
                    btnSTTAll.IsEnabled = true;
                    txtChunkLocation.IsEnabled = true;
                    waveStream = waveView.WaveStream;
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
                        LoadStream(chunkSaveLocation + ".wav");
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

            float minVolume = float.Parse(txtMinVolume.Text);
            float minLength = float.Parse(txtMinLength.Text);
            int maxSilenceMillis = int.Parse(txtMaxSilence.Text);

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

        private string GetNextFileName()
        {
            var chunkSaveLocation = Dispatcher.Invoke(() => txtChunkLocation.Text);
            var fileNames = Directory.EnumerateFiles(chunkSaveLocation, "*.wav").Select(s => s.Replace(@"\", "/").Split('/').Last());
            int i = 0;
            while (true)
            {
                var fileName = $"trimmed_{i++:D4}.wav";
                if (!fileNames.Contains(fileName))
                    return Path.Combine(chunkSaveLocation, fileName);
            }
        }

        private void DoSTT(AudioChunkView chunkView, bool suppressErrorDialogs = false)
        {
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
                    if (!suppressErrorDialogs)
                        MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                Dispatcher.Invoke(() =>
                {
                    grdMain.IsEnabled = true;
                    grdWait.Visibility = Visibility.Hidden;
                });
            }).Start();
        }

        private void DoExport(AudioChunkView chunkView, bool suppressErrorDialogs = false)
        {
            var chunkLocation = Dispatcher.Invoke(() => txtChunkLocation.Text);
            var speechText = Dispatcher.Invoke(() => chunkView.SpeechText);
            if (!string.IsNullOrWhiteSpace(speechText))
            {
                waveView.Pause();
                var savePath = GetNextFileName();

                if (System.IO.File.Exists(savePath))
                    System.IO.File.Delete(savePath);

                var oldPos = waveStream.Position;

                using (var writer = new WaveFileWriter(savePath, waveStream.WaveFormat))
                {
                    var start = (long)((Dispatcher.Invoke(() => chunkView.TimeRange.Start.TotalSeconds) / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
                    var end = (long)((Dispatcher.Invoke(() => chunkView.TimeRange.End.TotalSeconds) / waveStream.TotalTime.TotalSeconds) * waveStream.Length);

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

                File.AppendAllText(System.IO.Path.Combine(chunkLocation, "sentence_map.csv"), $"{savePath.Replace(@"\", "/").Split('/').Last()},\"{speechText}\"\n");

                Dispatcher.Invoke(() =>
                {
                    grdTime.Children.Remove(chunkView);
                    txtCount.Text = $"Count: {grdTime.Children.Count}";

                    if (!suppressErrorDialogs)
                        MessageBox.Show($"Done!\n{savePath}", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            else if (!suppressErrorDialogs)
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Text is empty!", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
        }

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

                if (!Utils.IsNetworkAvailable())
                {
                    MessageBox.Show("Please check internet connection.", "STT Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DoSTT(chunkView);
            };

            chunkView.ExportButtonClicked += delegate
            {
                DoExport(chunkView);
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
            foreach (var chunkView in grdTime.Children.OfType<AudioChunkView>().ToArray())
            {
                chunkView.SpeechText = "A";
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            txtWait.Text = "Exporting... Please wait...";
            grdWait.Visibility = Visibility.Visible;
            grdMain.IsEnabled = false;

            new Thread(() =>
            {
                foreach (var chunkView in Dispatcher.Invoke(() => grdTime.Children.OfType<AudioChunkView>().ToArray()))
                {
                    DoExport(chunkView, true);
                }

                Dispatcher.Invoke(() =>
                {
                    grdMain.IsEnabled = true;
                    grdWait.Visibility = Visibility.Hidden;
                    if (grdTime.Children.Count == 0)
                        MessageBox.Show($"All Done!\nSaved in \"{txtChunkLocation.Text}\"", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show($"Partially Done!\nSaved in \"{txtChunkLocation.Text}\"", "Export Info", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }).Start();
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
