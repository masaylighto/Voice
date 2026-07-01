using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Dsp;

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
        private float[]? _smoothMagnitudes;

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
            _audioEngine.RawSamplesCaptured += AudioEngine_RawSamplesCaptured;
            _audioEngine.LiveFrameProcessed += AudioEngine_LiveFrameProcessed;
            _audioEngine.RecordingFinished += AudioEngine_RecordingFinished;
        }

        private void ResetVisualizer()
        {
            if (WaveCanvas.ActualWidth > 0 && WaveCanvas.ActualHeight > 0)
            {
                WavePolyline.Points.Clear();
                WavePolygon.Points.Clear();
                
                // Draw a flat baseline when idle (at the bottom of the canvas)
                double baselineY = WaveCanvas.ActualHeight - 2;
                WavePolyline.Points.Add(new Point(0, baselineY));
                WavePolyline.Points.Add(new Point(WaveCanvas.ActualWidth, baselineY));
                
                // Draw background grid lines
                DrawGridLines();
            }
        }

        private void DrawGridLines()
        {
            if (WaveCanvas.ActualWidth <= 0 || WaveCanvas.ActualHeight <= 0) return;
            
            var geometry = new GeometryGroup();
            double w = WaveCanvas.ActualWidth;
            double h = WaveCanvas.ActualHeight;
            
            // Horizontal grid lines
            for (double y = 30; y < h; y += 30)
            {
                geometry.Children.Add(new LineGeometry(new Point(0, y), new Point(w, y)));
            }
            
            // Vertical grid lines
            for (double x = 45; x < w; x += 45)
            {
                geometry.Children.Add(new LineGeometry(new Point(x, 0), new Point(x, h)));
            }
            
            GridLinesPath.Data = geometry;
        }

        private void AudioEngine_RawSamplesCaptured(float[] samples)
        {
            // Thread-safe UI update
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (WaveCanvas.ActualWidth <= 0 || WaveCanvas.ActualHeight <= 0) return;

                double width = WaveCanvas.ActualWidth;
                double height = WaveCanvas.ActualHeight;

                WavePolyline.Points.Clear();
                WavePolygon.Points.Clear();

                // Compute FFT on the captured sample buffer
                int fftLen = 1024;
                if (samples.Length < fftLen) return;

                var complex = new Complex[fftLen];
                for (int i = 0; i < fftLen; i++)
                {
                    // Apply Hanning Window to prevent spectral leakage
                    float window = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (fftLen - 1)));
                    complex[i].X = samples[i] * window;
                    complex[i].Y = 0f;
                }

                int m = (int)Math.Log2(fftLen);
                FastFourierTransform.FFT(true, m, complex);

                // We display the vocal speech band (DC to ~3500 Hz)
                // At 44.1kHz sample rate, bin spacing is 44100 / 1024 = 43.07 Hz.
                // 3500 Hz corresponds to bin index: 3500 / 43.07 = 81.
                int startBin = 2; // skip DC and low sub-bass rumble
                int endBin = 85;  // covers up to ~3660 Hz
                int numBins = endBin - startBin;

                float[] magnitudes = new float[numBins];
                for (int i = 0; i < numBins; i++)
                {
                    var c = complex[i + startBin];
                    magnitudes[i] = (float)Math.Sqrt(c.X * c.X + c.Y * c.Y);
                }

                // Initialize smoothing buffer if needed
                if (_smoothMagnitudes == null || _smoothMagnitudes.Length != numBins)
                {
                    _smoothMagnitudes = new float[numBins];
                }

                // Apply exponential smoothing (rise fast, decay slow) to make the bars smooth and fluid
                for (int i = 0; i < numBins; i++)
                {
                    float target = magnitudes[i];
                    if (target > _smoothMagnitudes[i])
                        _smoothMagnitudes[i] = 0.4f * _smoothMagnitudes[i] + 0.6f * target;
                    else
                        _smoothMagnitudes[i] = 0.75f * _smoothMagnitudes[i] + 0.25f * target;
                }

                // Start polygon path at the bottom left corner
                WavePolygon.Points.Add(new Point(0, height));

                double xStep = width / (numBins - 1);
                double lastX = 0;

                for (int i = 0; i < numBins; i++)
                {
                    double x = i * xStep;
                    
                    // Convert magnitude to vertical pixel height
                    // We apply a square-root scaling for better visualization of quiet signals
                    double y = height - (Math.Sqrt(_smoothMagnitudes[i]) * height * 1.8);
                    y = Math.Clamp(y, 4, height - 2); // keep within visual range

                    var p = new Point(x, y);
                    WavePolyline.Points.Add(p);
                    WavePolygon.Points.Add(p);

                    lastX = x;
                }

                // Close polygon path at the bottom right corner
                WavePolygon.Points.Add(new Point(lastX, height));
                WavePolygon.Points.Add(new Point(0, height));
            }));
        }

        private void AudioEngine_LiveFrameProcessed(FrameMetrics metrics)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (metrics.IsVoiced)
                {
                    LivePitchText.Text = $"{Math.Round(metrics.Pitch)} Hz";
                    // Dynamic coloring depending on gender range
                    if (metrics.Pitch < 155f)
                        LivePitchText.Foreground = (SolidColorBrush)Application.Current.Resources["MaleBrush"];
                    else if (metrics.Pitch < 185f)
                        LivePitchText.Foreground = (SolidColorBrush)Application.Current.Resources["AndroBrush"];
                    else
                        LivePitchText.Foreground = (SolidColorBrush)Application.Current.Resources["FemaleBrush"];
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

                VisualizerOverlayText.Text = "Analysis Ready";
                VisualizerOverlaySubtext.Text = "Recording successfully captured. Click Analyze to view details.";
                VisualizerOverlay.Visibility = Visibility.Visible;

                StatusTitle.Text = "Vocal capture complete";
                StatusSubtitle.Text = "You can listen back to your recording or proceed to full analysis.";

                PlayButton.Visibility = Visibility.Visible;
                AnalyzeBtn.IsEnabled = true;

                // Reset visualizer to center flat line
                ResetVisualizer();
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
                    LivePitchText.Text = "--- Hz";
                    LivePitchText.Foreground = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
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
            if (_audioEngine == null || string.IsNullOrEmpty(_lastWavPath) || _isPlayingBack) return;

            _isPlayingBack = true;
            PlayButton.IsEnabled = false;
            SetButtonText(PlayButton, "Playing...");
            
            // Visual feedback on visualizer overlay during playback
            VisualizerOverlayText.Text = "Playing audio sample...";
            VisualizerOverlaySubtext.Text = "Listen to your recorded sample.";

            _audioEngine.PlaybackRecording(_lastWavPath, null, () =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _isPlayingBack = false;
                    PlayButton.IsEnabled = true;
                    SetButtonText(PlayButton, "Playback");
                    VisualizerOverlayText.Text = "Analysis Ready";
                    VisualizerOverlaySubtext.Text = "Recording successfully captured. Click Analyze to view details.";
                }));
            });
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
    }
}
