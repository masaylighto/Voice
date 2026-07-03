using System;
using System.Windows;
using System.Windows.Controls;

namespace Voice
{
    public partial class AnalysisView : UserControl
    {
        private VoiceAnalysisSession? _currentSession;
        private VoiceSessionRecord? _currentRecord;

        public AnalysisView()
        {
            InitializeComponent();
        }

        public void DisplaySession(VoiceAnalysisSession session)
        {
            _currentSession = session;
            _currentRecord = null;

            // Set Classification
            ClassificationText.Text = session.VoiceClassification;

            // Set raw numbers
            PitchRawText.Text = session.AveragePitchHz > 50 ? $"{Math.Round(session.AveragePitchHz)} Hz" : "No pitch detected";
            ResRawText.Text = $"{Math.Round(session.AverageResonanceHz)} Hz";
            WeightRawText.Text = $"{session.AverageWeightDb:F1} dB";
            IntRawText.Text = $"{session.PitchStdDevSemitones:F1} semitones";
            PatternRawText.Text = $"{session.ArticulationRate:F1} syl/s ({(session.EndingIsRising ? "rising sweep" : "falling sweep")})";

            // Set visual Gauges (0 to 100)
            PitchGauge.Value = session.PitchScore;
            ResGauge.Value = session.ResonanceScore;
            WeightGauge.Value = session.WeightScore;
            IntGauge.Value = session.IntonationScore;
            PatternGauge.Value = session.SpeechPatternsScore;
        }

        public void DisplayRecord(VoiceSessionRecord record)
        {
            _currentRecord = record;
            _currentSession = null;

            // Support displaying from saved history records
            ClassificationText.Text = record.VoiceClassification;

            PitchRawText.Text = record.AveragePitchHz > 50 ? $"{Math.Round(record.AveragePitchHz)} Hz" : "No pitch detected";
            ResRawText.Text = $"{Math.Round(record.AverageResonanceHz)} Hz";
            WeightRawText.Text = $"{record.AverageWeightDb:F1} dB";
            IntRawText.Text = $"{record.PitchStdDevSemitones:F1} semitones";
            PatternRawText.Text = $"{record.ArticulationRate:F1} syl/s ({(record.AverageWeightDb > 0 ? "falling sweep" : "rising sweep")})";

            PitchGauge.Value = record.PitchScore;
            ResGauge.Value = record.ResonanceScore;
            WeightGauge.Value = record.WeightScore;
            IntGauge.Value = record.IntonationScore;
            PatternGauge.Value = record.SpeechPatternsScore;
        }

        private void RawMetricsBtn_Click(object sender, RoutedEventArgs e)
        {
            string rawReport = "";

            if (_currentSession != null)
            {
                rawReport = ConstructRawReport(
                    _currentSession.AveragePitchHz, 
                    _currentSession.AverageResonanceHz, 
                    _currentSession.AverageWeightDb, 
                    _currentSession.PitchStdDevSemitones, 
                    _currentSession.ArticulationRate, 
                    _currentSession.PauseRatio, 
                    _currentSession.VoiceClassification,
                    _currentSession.EndingIsRising
                );
            }
            else if (_currentRecord != null)
            {
                rawReport = ConstructRawReport(
                    _currentRecord.AveragePitchHz, 
                    _currentRecord.AverageResonanceHz, 
                    _currentRecord.AverageWeightDb, 
                    _currentRecord.PitchStdDevSemitones, 
                    _currentRecord.ArticulationRate, 
                    _currentRecord.PauseRatio, 
                    _currentRecord.VoiceClassification,
                    null // Ending inflection is not archived in the simplified history record
                );
            }
            else
            {
                MessageBox.Show("No active voice analysis session is loaded. Please record a voice sample first.", 
                                "Raw Vocal Diagnostics", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(rawReport, "Raw Vocal Data (Unprocessed)", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string ConstructRawReport(float pitch, float resonance, float weight, float intonation, float rate, float pause, string classification, bool? ending = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== RAW VOCAL DIAGNOSTICS (UNPROCESSED) ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Voice Category: {classification}");
            sb.AppendLine();
            sb.AppendLine("1. Fundamental Frequency (F0):");
            sb.AppendLine($"   Average Pitch:   {(pitch > 50 ? $"{pitch:F1} Hz" : "No speech detected")}");
            sb.AppendLine();
            sb.AppendLine("2. Vocal Resonance:");
            sb.AppendLine($"   Centroid Freq:   {resonance:F1} Hz (Vocal tract filter placement)");
            sb.AppendLine();
            sb.AppendLine("3. Vocal Weight:");
            sb.AppendLine($"   Spectral Tilt:   {weight:F2} dB (PSD low vs mid-high energy)");
            sb.AppendLine();
            sb.AppendLine("4. Intonation Dynamics:");
            sb.AppendLine($"   Pitch Std Dev:   {intonation:F2} semitones (Contour melody range)");
            sb.AppendLine();
            sb.AppendLine("5. Speech Rhythms:");
            sb.AppendLine($"   Pacing Rate:     {rate:F2} syllables per active second");
            sb.AppendLine($"   Pause Ratio:     {pause * 100.0:F1}% (Silence threshold: < 0.01 RMS)");
            
            if (ending.HasValue)
            {
                sb.AppendLine($"   End Inflection:  {(ending.Value ? "Rising (Uptalk Sweep)" : "Falling (Statement Sweep)")}");
            }
            sb.AppendLine();
            sb.AppendLine("===========================================");
            return sb.ToString();
        }
    }
}
