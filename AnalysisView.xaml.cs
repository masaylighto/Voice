using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Voice
{
    public partial class AnalysisView : UserControl
    {
        private VoiceAnalysisSession? _currentSession;
        private VoiceSessionRecord? _currentRecord;
        private readonly List<PitchContourPoint> _pitchContour = new List<PitchContourPoint>();
        private float _medianPitchHz;
        private float _pitchP10Hz;
        private float _pitchP90Hz;
        private float _pitchCoverageRatio;
        private float _recordingDurationSeconds;
        private bool _isHistoricalContour;

        public AnalysisView()
        {
            InitializeComponent();
            Loaded += (_, _) => QueuePitchChartRender();
        }

        public void DisplaySession(VoiceAnalysisSession session)
        {
            _currentSession = session;
            _currentRecord = null;

            ClassificationText.Text = session.VoiceClassification;
            PitchRawText.Text = FormatPitchSummary(session.MedianPitchHz, session.PitchP10Hz, session.PitchP90Hz);
            ResRawText.Text = session.AverageResonanceHz > 0 ? $"{Math.Round(session.AverageResonanceHz)} Hz" : "No active speech";
            WeightRawText.Text = $"{session.AverageWeightDb:F1} dB";
            IntRawText.Text = $"{session.PitchStdDevSemitones:F1} semitones";
            PatternRawText.Text = FormatPatternSummary(session.ArticulationRate, session.EndingPitchDirection);

            PitchGauge.Value = session.PitchScore;
            ResGauge.Value = session.ResonanceScore;
            WeightGauge.Value = session.WeightScore;
            IntGauge.Value = session.IntonationScore;
            PatternGauge.Value = session.SpeechPatternsScore;

            SetPitchChartData(
                session.PitchContour,
                session.MedianPitchHz,
                session.PitchP10Hz,
                session.PitchP90Hz,
                session.PitchCoverageRatio,
                session.RecordingDurationSeconds,
                false);
        }

        public void DisplayRecord(VoiceSessionRecord record)
        {
            _currentRecord = record;
            _currentSession = null;

            float medianPitch = record.MedianPitchHz > 0 ? record.MedianPitchHz : record.AveragePitchHz;
            ClassificationText.Text = record.VoiceClassification;
            PitchRawText.Text = FormatPitchSummary(medianPitch, record.PitchP10Hz, record.PitchP90Hz);
            ResRawText.Text = record.AverageResonanceHz > 0 ? $"{Math.Round(record.AverageResonanceHz)} Hz" : "No active speech";
            WeightRawText.Text = $"{record.AverageWeightDb:F1} dB";
            IntRawText.Text = $"{record.PitchStdDevSemitones:F1} semitones";
            PatternRawText.Text = FormatPatternSummary(record.ArticulationRate, record.EndingPitchDirection);

            PitchGauge.Value = record.PitchScore;
            ResGauge.Value = record.ResonanceScore;
            WeightGauge.Value = record.WeightScore;
            IntGauge.Value = record.IntonationScore;
            PatternGauge.Value = record.SpeechPatternsScore;

            SetPitchChartData(
                record.PitchContour ?? new List<PitchContourPoint>(),
                medianPitch,
                record.PitchP10Hz,
                record.PitchP90Hz,
                record.PitchCoverageRatio,
                record.RecordingDurationSeconds,
                true);
        }

        private void SetPitchChartData(
            IEnumerable<PitchContourPoint> contour,
            float medianPitch,
            float pitchP10,
            float pitchP90,
            float coverageRatio,
            float durationSeconds,
            bool isHistorical)
        {
            _pitchContour.Clear();
            _pitchContour.AddRange(contour.Select(point => new PitchContourPoint
            {
                TimeSeconds = point.TimeSeconds,
                PitchHz = point.PitchHz,
                Confidence = point.Confidence
            }));

            _medianPitchHz = medianPitch;
            _pitchP10Hz = pitchP10;
            _pitchP90Hz = pitchP90;
            _pitchCoverageRatio = coverageRatio;
            _recordingDurationSeconds = durationSeconds;
            _isHistoricalContour = isHistorical;

            if (_medianPitchHz > 0)
            {
                string range = _pitchP10Hz > 0 && _pitchP90Hz > 0
                    ? $"central 80% {_pitchP10Hz:0}-{_pitchP90Hz:0} Hz"
                    : "range unavailable";
                string coverage = _pitchCoverageRatio > 0
                    ? $"tracked {_pitchCoverageRatio * 100f:0}%"
                    : "coverage unavailable";
                PitchChartSummaryText.Text = $"Median {_medianPitchHz:0} Hz | {range} | {coverage}";
            }
            else
            {
                PitchChartSummaryText.Text = "No reliable F0 summary is available.";
            }

            QueuePitchChartRender();
        }

        private static string FormatPitchSummary(float medianPitch, float pitchP10, float pitchP90)
        {
            if (medianPitch <= 0)
            {
                return "No reliable pitch";
            }

            return pitchP10 > 0 && pitchP90 > 0
                ? $"{medianPitch:0} Hz median ({pitchP10:0}-{pitchP90:0})"
                : $"{medianPitch:0} Hz median";
        }

        private static string FormatPatternSummary(float articulationRate, string endingDirection)
        {
            if (articulationRate <= 0)
            {
                return endingDirection;
            }

            return $"{articulationRate:F1} syl/s, {endingDirection.ToLowerInvariant()}";
        }

        private void PitchChartHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderPitchChart();
        }

        private void QueuePitchChartRender()
        {
            Dispatcher.BeginInvoke(new Action(RenderPitchChart));
        }

        private void RenderPitchChart()
        {
            double width = PitchChartCanvas.ActualWidth;
            double height = PitchChartCanvas.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            const double bottomLabelInset = 16;
            double plotHeight = Math.Max(1, height - bottomLabelInset);
            List<PitchContourPoint> voicedPoints = _pitchContour
                .Where(point => point.PitchHz > 0)
                .OrderBy(point => point.TimeSeconds)
                .ToList();

            float minPitch = 65f;
            float maxPitch = 300f;
            if (voicedPoints.Count > 0)
            {
                minPitch = Math.Min(minPitch, (float)(Math.Floor((voicedPoints.Min(point => point.PitchHz) - 20f) / 10f) * 10f));
                maxPitch = Math.Max(maxPitch, (float)(Math.Ceiling((voicedPoints.Max(point => point.PitchHz) + 20f) / 10f) * 10f));
            }

            minPitch = Math.Max(50f, minPitch);
            maxPitch = Math.Max(minPitch + 220f, maxPitch);

            Func<float, double> pitchToY = pitch =>
                Math.Clamp((maxPitch - pitch) / (maxPitch - minPitch) * plotHeight, 0, plotHeight);

            DrawPitchBands(width, pitchToY, minPitch, maxPitch);
            DrawPitchGrid(width, plotHeight, minPitch, maxPitch);
            DrawPitchContour(width, plotHeight, pitchToY);

            PitchChartTopLabel.Text = $"{maxPitch:0} Hz";
            PitchChartMidLabel.Text = $"{((minPitch + maxPitch) / 2f):0} Hz";
            PitchChartBottomLabel.Text = $"{minPitch:0} Hz";

            float duration = Math.Max(_recordingDurationSeconds, _pitchContour.Count > 0 ? _pitchContour.Max(point => point.TimeSeconds) : 0f);
            duration = Math.Max(0.1f, duration);
            PitchChartStartTime.Text = "0.0 s";
            PitchChartMidTime.Text = $"{duration / 2f:0.0} s";
            PitchChartEndTime.Text = $"{duration:0.0} s";

            if (voicedPoints.Count == 0)
            {
                PitchChartStatusText.Text = _isHistoricalContour
                    ? "This saved record has summary data only. New recordings retain the full pitch contour."
                    : "No reliable voiced pitch was detected. Speak closer to the microphone and try again.";
                PitchChartStatusText.Visibility = Visibility.Visible;
            }
            else
            {
                PitchChartStatusText.Visibility = Visibility.Collapsed;
            }
        }

        private void DrawPitchBands(double width, Func<float, double> pitchToY, float minPitch, float maxPitch)
        {
            SetPitchBand(PitchMasculineBand, width, pitchToY, minPitch, Math.Min(130f, maxPitch));
            SetPitchBand(PitchAndrogynousBand, width, pitchToY, Math.Max(145f, minPitch), Math.Min(175f, maxPitch));
            SetPitchBand(PitchFeminineBand, width, pitchToY, Math.Max(180f, minPitch), maxPitch);
        }

        private static void SetPitchBand(FrameworkElement band, double width, Func<float, double> pitchToY, float lowPitch, float highPitch)
        {
            if (highPitch <= lowPitch)
            {
                band.Width = 0;
                band.Height = 0;
                return;
            }

            double yTop = pitchToY(highPitch);
            double yBottom = pitchToY(lowPitch);
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, yTop);
            band.Width = width;
            band.Height = Math.Max(0, yBottom - yTop);
        }

        private void DrawPitchGrid(double width, double plotHeight, float minPitch, float maxPitch)
        {
            GeometryGroup grid = new GeometryGroup();
            for (int i = 0; i <= 4; i++)
            {
                double y = plotHeight * i / 4d;
                grid.Children.Add(new LineGeometry(new Point(0, y), new Point(width, y)));
            }

            for (int i = 0; i <= 5; i++)
            {
                double x = width * i / 5d;
                grid.Children.Add(new LineGeometry(new Point(x, 0), new Point(x, plotHeight)));
            }

            PitchChartGridPath.Data = grid;
        }

        private void DrawPitchContour(double width, double plotHeight, Func<float, double> pitchToY)
        {
            PathGeometry path = new PathGeometry();
            if (_pitchContour.Count > 0)
            {
                float duration = Math.Max(_recordingDurationSeconds, _pitchContour.Max(point => point.TimeSeconds));
                duration = Math.Max(0.1f, duration);
                PathFigure? currentFigure = null;

                foreach (PitchContourPoint point in _pitchContour.OrderBy(point => point.TimeSeconds))
                {
                    if (point.PitchHz <= 0)
                    {
                        currentFigure = null;
                        continue;
                    }

                    double x = Math.Clamp(point.TimeSeconds / duration * width, 0, width);
                    Point chartPoint = new Point(x, Math.Clamp(pitchToY(point.PitchHz), 0, plotHeight));
                    if (currentFigure == null)
                    {
                        currentFigure = new PathFigure { StartPoint = chartPoint, IsClosed = false, IsFilled = false };
                        path.Figures.Add(currentFigure);
                    }
                    else
                    {
                        currentFigure.Segments.Add(new LineSegment(chartPoint, true));
                    }
                }
            }

            PitchContourPath.Data = path;
        }

        private void RawMetricsBtn_Click(object sender, RoutedEventArgs e)
        {
            string rawReport;
            if (_currentSession != null)
            {
                rawReport = ConstructRawReport(
                    _currentSession.AveragePitchHz,
                    _currentSession.MedianPitchHz,
                    _currentSession.PitchP10Hz,
                    _currentSession.PitchP90Hz,
                    _currentSession.PitchCoverageRatio,
                    _currentSession.RecordingDurationSeconds,
                    _currentSession.AverageResonanceHz,
                    _currentSession.AverageWeightDb,
                    _currentSession.PitchStdDevSemitones,
                    _currentSession.ArticulationRate,
                    _currentSession.PauseRatio,
                    _currentSession.VoiceClassification,
                    _currentSession.EndingPitchDirection);
            }
            else if (_currentRecord != null)
            {
                rawReport = ConstructRawReport(
                    _currentRecord.AveragePitchHz,
                    _currentRecord.MedianPitchHz,
                    _currentRecord.PitchP10Hz,
                    _currentRecord.PitchP90Hz,
                    _currentRecord.PitchCoverageRatio,
                    _currentRecord.RecordingDurationSeconds,
                    _currentRecord.AverageResonanceHz,
                    _currentRecord.AverageWeightDb,
                    _currentRecord.PitchStdDevSemitones,
                    _currentRecord.ArticulationRate,
                    _currentRecord.PauseRatio,
                    _currentRecord.VoiceClassification,
                    _currentRecord.EndingPitchDirection);
            }
            else
            {
                MessageBox.Show(
                    "No active voice analysis session is loaded. Please record a voice sample first.",
                    "Voice Measurement Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                Clipboard.SetText(rawReport);
                MessageBox.Show("Voice measurements copied to the clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to copy to the clipboard: {ex.Message}\n\n{rawReport}",
                    "Voice Measurement Report",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private static string ConstructRawReport(
            float meanPitch,
            float medianPitch,
            float pitchP10,
            float pitchP90,
            float pitchCoverage,
            float duration,
            float resonance,
            float weight,
            float intonation,
            float rate,
            float pause,
            string classification,
            string endingDirection)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== VOICE MEASUREMENT REPORT ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Pitch Profile: {classification}");
            sb.AppendLine();
            sb.AppendLine("1. Fundamental Frequency (F0)");
            sb.AppendLine($"   Median F0:       {FormatFrequency(medianPitch)}");
            sb.AppendLine($"   Trimmed Mean F0: {FormatFrequency(meanPitch)}");
            sb.AppendLine($"   Central 80%:     {FormatRange(pitchP10, pitchP90)}");
            sb.AppendLine($"   F0 Coverage:     {FormatPercentage(pitchCoverage)} of analysis frames");
            sb.AppendLine($"   Recording Span:  {FormatDuration(duration)}");
            sb.AppendLine("   Method: YIN F0 tracking, 65-500 Hz, confidence gate >= 0.70.");
            sb.AppendLine();
            sb.AppendLine("2. Spectral Characteristics");
            sb.AppendLine($"   Spectral Centroid: {FormatFrequency(resonance)}");
            sb.AppendLine($"   Spectral Balance:  {weight:F2} dB (80-250 Hz vs 250-3000 Hz band power)");
            sb.AppendLine();
            sb.AppendLine("3. Pitch Variation and Speech Timing");
            sb.AppendLine($"   F0 Standard Dev: {intonation:F2} semitones");
            sb.AppendLine($"   Pacing Estimate: {rate:F2} syllables per active second");
            sb.AppendLine($"   Pause Ratio:     {pause * 100f:F1}% (RMS < 0.008)");
            sb.AppendLine($"   Ending Contour:  {endingDirection}");
            sb.AppendLine();
            sb.AppendLine("Note: pitch presentation is an acoustic comparison. It does not determine gender identity.");
            sb.AppendLine("================================");
            return sb.ToString();
        }

        private static string FormatFrequency(float frequency)
        {
            return frequency > 0 ? $"{frequency:F1} Hz" : "Not available";
        }

        private static string FormatRange(float low, float high)
        {
            return low > 0 && high > 0 ? $"{low:F1}-{high:F1} Hz" : "Not available";
        }

        private static string FormatPercentage(float value)
        {
            return value > 0 ? $"{value * 100f:F1}%" : "Not available";
        }

        private static string FormatDuration(float seconds)
        {
            return seconds > 0 ? $"{seconds:F2} seconds" : "Not available";
        }
    }
}
