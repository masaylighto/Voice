using System;
using System.Windows;
using System.Windows.Controls;

namespace Voice
{
    public partial class AnalysisView : UserControl
    {
        public AnalysisView()
        {
            InitializeComponent();
        }

        public void DisplaySession(VoiceAnalysisSession session)
        {
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

            // Set the ASCII progress bars exactly matching the requested look
            PitchAsciiBar.Text = GetAsciiBar(session.PitchScore);
            ResAsciiBar.Text = GetAsciiBar(session.ResonanceScore);
            WeightAsciiBar.Text = GetAsciiBar(session.WeightScore);
            IntAsciiBar.Text = GetAsciiBar(session.IntonationScore);
            PatternAsciiBar.Text = GetAsciiBar(session.SpeechPatternsScore);
        }

        public void DisplayRecord(VoiceSessionRecord record)
        {
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

            PitchAsciiBar.Text = GetAsciiBar(record.PitchScore);
            ResAsciiBar.Text = GetAsciiBar(record.ResonanceScore);
            WeightAsciiBar.Text = GetAsciiBar(record.WeightScore);
            IntAsciiBar.Text = GetAsciiBar(record.IntonationScore);
            PatternAsciiBar.Text = GetAsciiBar(record.SpeechPatternsScore);
        }

        private string GetAsciiBar(float score)
        {
            int filled = (int)Math.Round(score / 10.0f);
            filled = Math.Clamp(filled, 0, 10);
            
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 10; i++)
            {
                if (i < filled)
                    sb.Append("█ ");
                else
                    sb.Append("░ ");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
