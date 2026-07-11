using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

namespace Voice
{
    public class VoiceSessionRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string AudioPath { get; set; } = string.Empty;
        
        // 0-100 Scores
        public float PitchScore { get; set; }
        public float ResonanceScore { get; set; }
        public float WeightScore { get; set; }
        public float IntonationScore { get; set; }
        public float SpeechPatternsScore { get; set; }

        // Raw Metrics
        public float AveragePitchHz { get; set; }
        public float MedianPitchHz { get; set; }
        public float PitchP10Hz { get; set; }
        public float PitchP90Hz { get; set; }
        public float PitchCoverageRatio { get; set; }
        public float RecordingDurationSeconds { get; set; }
        public float AverageResonanceHz { get; set; }
        public float AverageWeightDb { get; set; }
        public float PitchStdDevSemitones { get; set; }
        public float PauseRatio { get; set; }
        public float ArticulationRate { get; set; }
        public string EndingPitchDirection { get; set; } = "Not enough data";
        public string VoiceClassification { get; set; } = "Unknown";
        public List<PitchContourPoint> PitchContour { get; set; } = new List<PitchContourPoint>();
    }

    public static class SessionHistory
    {
        private static readonly string AppFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "VoiceTester"
        );
        private static readonly string RecordingsFolder = Path.Combine(AppFolder, "Recordings");
        private static readonly string HistoryFile = Path.Combine(AppFolder, "history.json");

        static SessionHistory()
        {
            try
            {
                if (!Directory.Exists(AppFolder)) Directory.CreateDirectory(AppFolder);
                if (!Directory.Exists(RecordingsFolder)) Directory.CreateDirectory(RecordingsFolder);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create folders: {ex.Message}");
            }
        }

        public static List<VoiceSessionRecord> LoadHistory()
        {
            if (!File.Exists(HistoryFile))
            {
                return new List<VoiceSessionRecord>();
            }

            try
            {
                string json = File.ReadAllText(HistoryFile);
                return JsonSerializer.Deserialize<List<VoiceSessionRecord>>(json) ?? new List<VoiceSessionRecord>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load history: {ex.Message}");
                return new List<VoiceSessionRecord>();
            }
        }

        public static VoiceSessionRecord? SaveSession(VoiceAnalysisSession session, string tempWavPath)
        {
            if (session.Frames.Count == 0) return null;

            try
            {
                string permanentAudioPath = string.Empty;
                
                // Copy temporary WAV to app recordings directory
                if (File.Exists(tempWavPath))
                {
                    string filename = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.wav";
                    permanentAudioPath = Path.Combine(RecordingsFolder, filename);
                    File.Copy(tempWavPath, permanentAudioPath, true);
                }

                var record = new VoiceSessionRecord
                {
                    Timestamp = DateTime.Now,
                    AudioPath = permanentAudioPath,
                    PitchScore = session.PitchScore,
                    ResonanceScore = session.ResonanceScore,
                    WeightScore = session.WeightScore,
                    IntonationScore = session.IntonationScore,
                    SpeechPatternsScore = session.SpeechPatternsScore,
                    AveragePitchHz = session.AveragePitchHz,
                    MedianPitchHz = session.MedianPitchHz,
                    PitchP10Hz = session.PitchP10Hz,
                    PitchP90Hz = session.PitchP90Hz,
                    PitchCoverageRatio = session.PitchCoverageRatio,
                    RecordingDurationSeconds = session.RecordingDurationSeconds,
                    AverageResonanceHz = session.AverageResonanceHz,
                    AverageWeightDb = session.AverageWeightDb,
                    PitchStdDevSemitones = session.PitchStdDevSemitones,
                    PauseRatio = session.PauseRatio,
                    ArticulationRate = session.ArticulationRate,
                    EndingPitchDirection = session.EndingPitchDirection,
                    VoiceClassification = session.VoiceClassification,
                    PitchContour = session.PitchContour.Select(point => new PitchContourPoint
                    {
                        TimeSeconds = point.TimeSeconds,
                        PitchHz = point.PitchHz,
                        Confidence = point.Confidence
                    }).ToList()
                };

                var currentHistory = LoadHistory();
                currentHistory.Insert(0, record); // Keep newest on top

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(currentHistory, options);
                File.WriteAllText(HistoryFile, json);

                return record;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save session: {ex.Message}");
                return null;
            }
        }

        public static void DeleteRecord(VoiceSessionRecord record)
        {
            try
            {
                // Delete WAV file
                if (!string.IsNullOrEmpty(record.AudioPath) && File.Exists(record.AudioPath))
                {
                    File.Delete(record.AudioPath);
                }

                // Remove from json
                var currentHistory = LoadHistory();
                currentHistory.RemoveAll(r => r.Id == record.Id);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(currentHistory, options);
                File.WriteAllText(HistoryFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete record: {ex.Message}");
            }
        }
    }
}
