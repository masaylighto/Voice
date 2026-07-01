using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Voice
{
    public partial class TrainerView : UserControl
    {
        private AudioEngine? _audioEngine;
        private readonly Queue<float> _historyPoints = new Queue<float>();
        private const int MaxPoints = 150; // Max points visible on the chart

        // Ranges for scaling the chart
        private float _minChartVal = 50f;
        private float _maxChartVal = 350f;
        
        // Target range bounds
        private float _targetMin = 155f;
        private float _targetMax = 185f;

        private bool _isPracticing;

        public TrainerView()
        {
            InitializeComponent();
            this.SizeChanged += (s, e) => UpdateChartLayout();
        }

        public void SetAudioEngine(AudioEngine audioEngine)
        {
            _audioEngine = audioEngine;
            _audioEngine.LiveFrameProcessed += AudioEngine_LiveFrameProcessed;
        }

        private void AudioEngine_LiveFrameProcessed(FrameMetrics metrics)
        {
            if (!_isPracticing) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool isPitchPractice = MetricSelector.SelectedIndex == 0;
                float currentVal = 0;
                bool isValid = false;

                if (isPitchPractice)
                {
                    if (metrics.IsVoiced)
                    {
                        currentVal = metrics.Pitch;
                        isValid = true;
                        LiveValueText.Text = $"{Math.Round(currentVal)} Hz";
                    }
                    else
                    {
                        LiveValueText.Text = "--- Hz";
                    }
                }
                else // Resonance practice
                {
                    if (metrics.Rms > 0.008f)
                    {
                        currentVal = metrics.ResonanceCentroid;
                        isValid = true;
                        LiveValueText.Text = $"{Math.Round(currentVal)} Hz";
                    }
                    else
                    {
                        LiveValueText.Text = "--- Hz";
                    }
                }

                if (isValid)
                {
                    // Add to rolling history
                    _historyPoints.Enqueue(currentVal);
                    if (_historyPoints.Count > MaxPoints)
                    {
                        _historyPoints.Dequeue();
                    }

                    // Update live coaching tip
                    UpdateCoachingFeedback(currentVal);

                    // Redraw the line
                    RedrawLine();
                }
            }));
        }

        private void RedrawLine()
        {
            if (ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0 || _historyPoints.Count == 0) return;

            double width = ChartCanvas.ActualWidth;
            double height = ChartCanvas.ActualHeight;

            TrainerPolyline.Points.Clear();

            double xStep = width / (MaxPoints - 1);
            float[] vals = _historyPoints.ToArray();

            // Offset to draw line scrolling from right to left
            int offset = MaxPoints - vals.Length;

            for (int i = 0; i < vals.Length; i++)
            {
                double x = (i + offset) * xStep;
                
                // Map Y value (higher values are higher on screen, so Y-coord is subtracted from Height)
                float normValue = (vals[i] - _minChartVal) / (_maxChartVal - _minChartVal);
                double y = height - (normValue * height);
                y = Math.Clamp(y, 2, height - 2);

                TrainerPolyline.Points.Add(new Point(x, y));
            }
        }

        private void UpdateChartLayout()
        {
            if (ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0) return;

            double height = ChartCanvas.ActualHeight;

            // Configure scaling variables based on selector
            bool isPitch = MetricSelector.SelectedIndex == 0;
            if (isPitch)
            {
                _minChartVal = 50f;
                _maxChartVal = 350f;
                MaxYLabel.Text = "350 Hz";
                MidYLabel.Text = "200 Hz";
                MinYLabel.Text = "50 Hz";
            }
            else
            {
                _minChartVal = 500f;
                _maxChartVal = 2500f;
                MaxYLabel.Text = "2500 Hz";
                MidYLabel.Text = "1500 Hz";
                MinYLabel.Text = "500 Hz";
            }

            // Determine Target range
            int targetZone = TargetSelector.SelectedIndex; // 0=Masculine, 1=Androgynous, 2=Feminine
            if (isPitch)
            {
                if (targetZone == 0) { _targetMin = 85f; _targetMax = 130f; }
                else if (targetZone == 1) { _targetMin = 155f; _targetMax = 185f; }
                else { _targetMin = 190f; _targetMax = 240f; }
            }
            else // Resonance
            {
                if (targetZone == 0) { _targetMin = 800f; _targetMax = 1150f; }
                else if (targetZone == 1) { _targetMin = 1300f; _targetMax = 1600f; }
                else { _targetMin = 1750f; _targetMax = 2100f; }
            }

            // Reposition Shaded Target Band
            float normMax = (_targetMax - _minChartVal) / (_maxChartVal - _minChartVal);
            float normMin = (_targetMin - _minChartVal) / (_maxChartVal - _minChartVal);

            double yTop = height - (normMax * height);
            double yBottom = height - (normMin * height);

            Canvas.SetTop(TargetBand, yTop);
            TargetBand.Height = Math.Max(5, yBottom - yTop);

            // Re-render points on resize
            RedrawLine();
        }

        private void UpdateCoachingFeedback(float currentVal)
        {
            if (currentVal >= _targetMin && currentVal <= _targetMax)
            {
                CoachingTipText.Text = "Excellent! You are in the target zone. Maintain this posture.";
                CoachingTipText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // emerald
            }
            else if (currentVal < _targetMin)
            {
                CoachingTipText.Text = "Try raising your tone. Focus sound in your mouth/head.";
                CoachingTipText.Foreground = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
            }
            else
            {
                CoachingTipText.Text = "Try relaxing your throat and dropping your larynx to lower the tone.";
                CoachingTipText.Foreground = (SolidColorBrush)Application.Current.Resources["FemaleBrush"];
            }
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_audioEngine == null) return;

            if (!_isPracticing)
            {
                try
                {
                    _audioEngine.StartRecording(); // Captures audio stream
                    _isPracticing = true;
                    _historyPoints.Clear();
                    TrainerPolyline.Points.Clear();
                    
                    StartBtn.Content = "Stop Practice";
                    StartBtn.Background = Brushes.Black;
                    
                    CoachingTipText.Text = "Vocalize and try to stay inside the green target band!";
                    CoachingTipText.Foreground = (SolidColorBrush)Application.Current.Resources["TextPrimaryBrush"];
                    
                    MetricSelector.IsEnabled = false;
                    TargetSelector.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not start audio stream: {ex.Message}", "Microphone Error");
                }
            }
            else
            {
                _audioEngine.StopRecording();
                _isPracticing = false;
                StartBtn.Content = "Start Live Practice";
                StartBtn.Background = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
                
                LiveValueText.Text = "---";
                CoachingTipText.Text = "Practice stopped. Choose settings and click Start.";
                CoachingTipText.Foreground = (SolidColorBrush)Application.Current.Resources["TextSecondaryBrush"];
                
                MetricSelector.IsEnabled = true;
                TargetSelector.IsEnabled = true;
                
                TrainerPolyline.Points.Clear();
            }
        }

        private void MetricSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) UpdateChartLayout();
        }

        private void TargetSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) UpdateChartLayout();
        }
    }
}
