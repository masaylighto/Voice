using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Voice
{
    public partial class DashboardView : UserControl
    {
        private AudioEngine? _audioEngine;
        private DispatcherTimer? _recordingTimer;
        private DateTime _recordingStartTime;
        private string? _lastWavPath;
        private VoiceAnalysisSession? _lastSession;
        private bool _isPlayingBack;

        private string? _currentCategory;
        private int _currentPromptIndex = 0;
        private readonly Queue<float> _liveVolumeHistory = new Queue<float>();
        private const int MaxLiveVolumePoints = 240;

        // Event to notify MainWindow that we completed a recording and want to view analysis
        public event Action<VoiceAnalysisSession, string>? AnalysisRequested;

        public DashboardView()
        {
            InitializeComponent();
            
            // Set up recording timer (updates UI every 100ms)
            _recordingTimer = new DispatcherTimer();
            _recordingTimer.Interval = TimeSpan.FromMilliseconds(100);
            _recordingTimer.Tick += RecordingTimer_Tick;

            // Handle canvas size changes
            this.SizeChanged += (s, e) => ResetVisualizer();

            // Populate reading prompts categories
            foreach (var cat in ReadingPrompts.Categories.Keys)
            {
                PromptCategoryCombo.Items.Add(cat);
            }
            if (PromptCategoryCombo.Items.Count > 0)
            {
                PromptCategoryCombo.SelectedIndex = 0;
            }
        }

        public void SetAudioEngine(AudioEngine audioEngine)
        {
            _audioEngine = audioEngine;
            _audioEngine.VolumeCaptured += AudioEngine_VolumeCaptured;
            _audioEngine.RecordingFinished += AudioEngine_RecordingFinished;
        }

        private void ResetVisualizer()
        {
            DrawGridLines();
            DrawLivePitchContour();
        }

        private void DrawGridLines()
        {
            if (WaveCanvas.ActualWidth <= 0 || WaveCanvas.ActualHeight <= 0)
            {
                return;
            }

            const float minPitch = 65f;
            const float maxPitch = 300f;
            double width = WaveCanvas.ActualWidth;
            double chartHeight = Math.Max(1, WaveCanvas.ActualHeight - 42);
            Func<float, double> pitchToY = pitch =>
                Math.Clamp((maxPitch - pitch) / (maxPitch - minPitch) * chartHeight, 0, chartHeight);

            SetLivePitchBand(LiveMasculineBand, width, pitchToY, minPitch, 130f);
            SetLivePitchBand(LiveAndrogynousBand, width, pitchToY, 145f, 175f);
            SetLivePitchBand(LiveFeminineBand, width, pitchToY, 180f, maxPitch);

            GeometryGroup geometry = new GeometryGroup();
            for (int i = 0; i <= 4; i++)
            {
                double y = chartHeight * i / 4d;
                geometry.Children.Add(new LineGeometry(new Point(0, y), new Point(width, y)));
            }

            for (int i = 0; i <= 6; i++)
            {
                double x = width * i / 6d;
                geometry.Children.Add(new LineGeometry(new Point(x, 0), new Point(x, chartHeight)));
            }

            GridLinesPath.Data = geometry;
        }

        private static void SetLivePitchBand(FrameworkElement band, double width, Func<float, double> pitchToY, float lowPitch, float highPitch)
        {
            double yTop = pitchToY(highPitch);
            double yBottom = pitchToY(lowPitch);
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, yTop);
            band.Width = width;
            band.Height = Math.Max(0, yBottom - yTop);
        }

        private void DrawLivePitchContour()
        {
            double width = WaveCanvas.ActualWidth;
            double chartHeight = WaveCanvas.ActualHeight;

            if (width <= 0 || chartHeight <= 0) return;

            float[] values = _liveVolumeHistory.ToArray();
            int offset = MaxLiveVolumePoints - values.Length;

            double xStep = width / (MaxLiveVolumePoints - 1d);
            PathGeometry path = new PathGeometry();
            PathFigure? currentFigure = null;

            for (int i = 0; i < values.Length; i++)
            {
                double x = (i + offset) * xStep;
                // Scale RMS volume (0.0 to 1.0) so typical speech (0.01 to 0.3) is bouncy and clear
                double volumeRatio = Math.Clamp(values[i] * 4.0, 0.0, 1.0);
                double y = chartHeight - (volumeRatio * chartHeight * 0.8) - (chartHeight * 0.1);
                Point point = new Point(x, y);

                if (currentFigure == null)
                {
                    currentFigure = new PathFigure { StartPoint = point, IsClosed = false, IsFilled = false };
                    path.Figures.Add(currentFigure);
                }
                else
                {
                    currentFigure.Segments.Add(new LineSegment(point, true));
                }
            }

            LivePitchPath.Data = path;
        }

        private void AudioEngine_VolumeCaptured(float rmsVolume)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _liveVolumeHistory.Enqueue(rmsVolume);
                if (_liveVolumeHistory.Count > MaxLiveVolumePoints)
                {
                    _liveVolumeHistory.Dequeue();
                }
                DrawLivePitchContour();

                if (rmsVolume > 0.015f)
                {
                    LivePitchText.Text = "Capturing Voice...";
                    LivePitchText.Foreground = (SolidColorBrush)Application.Current.Resources["FemaleBrush"];
                }
                else
                {
                    LivePitchText.Text = "Listening...";
                    LivePitchText.Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
                }
            }));
        }

        private void AudioEngine_RecordingFinished(VoiceAnalysisSession session)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _recordingTimer?.Stop();
                _lastSession = session;
                _lastWavPath = _audioEngine?.TempWavPath;

                // Update UI state
                RecordBtnText.Text = "●";
                RecordBtnText.Foreground = Brushes.White;
                RecordBtn.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // red

                if (!string.IsNullOrEmpty(session.AnalysisError))
                {
                    VisualizerOverlayText.Text = "Praat Error";
                    VisualizerOverlaySubtext.Text = "Praat is required for analysis.";
                    StatusTitle.Text = "Praat Missing";
                    StatusSubtitle.Text = "Please run the installer script to configure Praat.";

                    PlayButton.Visibility = Visibility.Collapsed;
                    AnalyzeBtn.IsEnabled = false;

                    MessageBox.Show(
                        session.AnalysisError, 
                        "Praat Required", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                }
                else
                {
                    VisualizerOverlayText.Text = "Analysis Ready";
                    VisualizerOverlaySubtext.Text = "Recording successfully captured. Click Analyze to view details.";
                    StatusTitle.Text = "Vocal capture complete";
                    StatusSubtitle.Text = "You can listen back to your recording or proceed to full analysis.";

                    PlayButton.Visibility = Visibility.Visible;
                    AnalyzeBtn.IsEnabled = true;
                }

                DrawLivePitchContour();
            }));
        }

        private void RecordingTimer_Tick(object? sender, EventArgs e)
        {
            TimeSpan elapsed = DateTime.Now - _recordingStartTime;
            LiveTimerText.Text = $"{elapsed.TotalSeconds:F1}s";
        }

        private void RecordBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_audioEngine == null) return;

            if (!_audioEngine.IsRecording)
            {
                // Start Recording
                try
                {
                    _audioEngine.StartRecording();
                    
                    _recordingStartTime = DateTime.Now;
                    LiveTimerText.Text = "0.0s";
                    LivePitchText.Text = "Listening...";
                    LivePitchText.Foreground = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
                    _liveVolumeHistory.Clear();
                    ResetVisualizer();
                    _recordingTimer?.Start();

                    // Update UI state
                    RecordBtnText.Text = "■";
                    RecordBtnText.Foreground = Brushes.White;
                    RecordBtn.Background = Brushes.Black;

                    VisualizerOverlay.Visibility = Visibility.Collapsed;
                    StatusTitle.Text = "Recording...";
                    StatusSubtitle.Text = "Speak into your microphone. Say a sentence like 'The rainbow is a division of white light into beautiful colors.'";
                    
                    PlayButton.Visibility = Visibility.Collapsed;
                    AnalyzeBtn.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not access the microphone: {ex.Message}", "Audio Capture Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Stop Recording
                _audioEngine.StopRecording();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioEngine == null || string.IsNullOrEmpty(_lastWavPath)) return;

            if (_isPlayingBack)
            {
                _audioEngine.StopPlayback();
                return;
            }

            _isPlayingBack = true;
            SetButtonText(PlayButton, "Stop");
            SetPlayButtonIcon("&#xE71A;");
            
            // Visual feedback on visualizer overlay during playback
            VisualizerOverlayText.Text = "Playing audio sample...";
            VisualizerOverlaySubtext.Text = "Listen to your recorded sample.";

            _audioEngine.PlaybackRecording(_lastWavPath, null, () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isPlayingBack = false;
                    SetButtonText(PlayButton, "Playback");
                    SetPlayButtonIcon("&#xE768;");
                    VisualizerOverlayText.Text = "Analysis Ready";
                    VisualizerOverlaySubtext.Text = "Recording successfully captured. Click Analyze to view details.";
                }));
            });
        }

        private void SetPlayButtonIcon(string glyph)
        {
            if (PlayButton.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.FontFamily.Source == "Segoe MDL2 Assets")
                    {
                        tb.Text = glyph == "&#xE768;" ? "\uE768" : "\uE71A";
                        break;
                    }
                }
            }
        }

        private void AnalyzeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_lastSession != null && !string.IsNullOrEmpty(_lastWavPath))
            {
                // Save session in history log
                var record = SessionHistory.SaveSession(_lastSession, _lastWavPath);
                
                // Trigger event to main window
                AnalysisRequested?.Invoke(_lastSession, _lastWavPath);
            }
        }

        private void PromptCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PromptCategoryCombo.SelectedItem is string category)
            {
                _currentCategory = category;
                _currentPromptIndex = 0;
                UpdateSelectedPrompt();
            }
        }

        private void PrevPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentCategory) && ReadingPrompts.Categories.TryGetValue(_currentCategory, out var prompts))
            {
                if (prompts.Count > 0)
                {
                    _currentPromptIndex--;
                    if (_currentPromptIndex < 0) _currentPromptIndex = prompts.Count - 1;
                    UpdateSelectedPrompt();
                }
            }
        }

        private void NextPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentCategory) && ReadingPrompts.Categories.TryGetValue(_currentCategory, out var prompts))
            {
                if (prompts.Count > 0)
                {
                    _currentPromptIndex++;
                    if (_currentPromptIndex >= prompts.Count) _currentPromptIndex = 0;
                    UpdateSelectedPrompt();
                }
            }
        }

        private void UpdateSelectedPrompt()
        {
            if (!string.IsNullOrEmpty(_currentCategory) && ReadingPrompts.Categories.TryGetValue(_currentCategory, out var prompts))
            {
                if (_currentPromptIndex >= 0 && _currentPromptIndex < prompts.Count)
                {
                    var prompt = prompts[_currentPromptIndex];
                    SelectedPromptText.Text = prompt.Text;
                    PromptNoteText.Text = $"Note: {prompt.Note} (Prompt {_currentPromptIndex + 1} of {prompts.Count})";
                }
            }
        }

        private void SetButtonText(Button button, string text)
        {
            if (button.Content is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is TextBlock tb && tb.FontFamily.Source != "Segoe MDL2 Assets")
                    {
                        tb.Text = text;
                        break;
                    }
                }
            }
        }

        private void OpenRecordingsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string recordingsDir = System.IO.Path.Combine(appData, "VoiceTester", "Recordings");
                if (!System.IO.Directory.Exists(recordingsDir))
                {
                    System.IO.Directory.CreateDirectory(recordingsDir);
                }
                
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{recordingsDir}\"",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open recordings directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
