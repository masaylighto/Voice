using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Voice
{
    public class PitchContourPoint
    {
        public float TimeSeconds { get; set; }
        public float PitchHz { get; set; }
        public float Confidence { get; set; }
    }

    public class VoiceContourPoint
    {
        public float TimeSeconds { get; set; }
        public float PitchHz { get; set; }
        public float F1Hz { get; set; }
        public float F2Hz { get; set; }
        public float F3Hz { get; set; }
        public float F4Hz { get; set; }
        public float F2F1Ratio { get; set; }
        public float ResonanceHz { get; set; }
        public float WeightDb { get; set; }
        public float IntensityDb { get; set; }
        public float JitterPct { get; set; }
        public float ShimmerPct { get; set; }
    }

    public class VoiceAnalysisSession
    {
        private const float SampleRate = 44100f;
        private const int MinimumFramesForPitchProfile = 20;

        public List<PitchContourPoint> PitchContour { get; } = new List<PitchContourPoint>();
        public List<VoiceContourPoint> VoiceContour { get; } = new List<VoiceContourPoint>();

        // Display scales. They describe acoustic position, not a person's identity.
        public float PitchScore { get; private set; }
        public float ResonanceScore { get; private set; }
        public float WeightScore { get; private set; }
        public float IntonationScore { get; private set; }
        public float SpeechPatternsScore { get; private set; }

        // Pitch summary. Median is the primary value because it is resistant to octave outliers.
        public float AveragePitchHz { get; private set; }
        public float MedianPitchHz { get; private set; }
        public float PitchP10Hz { get; private set; }
        public float PitchP90Hz { get; private set; }
        public float PitchCoverageRatio { get; private set; }
        public float RecordingDurationSeconds { get; private set; }

        // Other measured characteristics.
        public float AverageResonanceHz { get; private set; }
        public float AverageWeightDb { get; private set; }
        public float PitchStdDevSemitones { get; private set; }
        public float PauseRatio { get; private set; }
        public float ArticulationRate { get; private set; }
        public bool EndingIsRising { get; private set; }
        public string EndingPitchDirection { get; private set; } = "Not enough data";

        // Praat specific metrics
        public float AverageF1Hz { get; private set; }
        public float AverageF2Hz { get; private set; }
        public float AverageF3Hz { get; private set; }
        public float AverageF4Hz { get; private set; }
        public float F2F1Ratio { get; private set; }
        public float FormantDispersion { get; private set; }
        public float SpeakingRate { get; private set; }
        public float JitterLocalPct { get; private set; }
        public float ShimmerLocalPct { get; private set; }

        public bool IsPraatAnalysis { get; private set; }
        public string? AnalysisError { get; private set; }

        public string VoiceClassification { get; private set; } = "Calibrating";

        public void CalculateResults(string? wavPath = null)
        {
            IsPraatAnalysis = false;
            AnalysisError = null;

            if (string.IsNullOrEmpty(wavPath))
            {
                ResetMetrics();
                return;
            }

            if (TryCalculateResultsWithPraat(wavPath))
            {
                IsPraatAnalysis = true;
                return;
            }

            // Praat is missing or failed - clear metrics and abort analysis!
            ResetMetrics();
            VoiceClassification = "Praat Missing";
            AnalysisError = "Voice analysis failed because Praat was not found or could not execute. Please ensure Praat is installed in the application directory.";
        }


        private string DetermineClassification(int validPitchCount)
        {
            if (validPitchCount < MinimumFramesForPitchProfile)
            {
                return "Insufficient voiced speech";
            }

            // These are pitch-presentation bands used by speech-language sources. F0 alone
            // cannot determine gender, so resonance, weight, and cadence do not alter this label.
            if (MedianPitchHz < 130f)
            {
                return "Masculine-leaning pitch";
            }
            if (MedianPitchHz < 145f)
            {
                return "Lower androgynous pitch";
            }
            if (MedianPitchHz <= 175f)
            {
                return "Androgynous pitch";
            }
            if (MedianPitchHz < 180f)
            {
                return "Higher androgynous pitch";
            }

            return "Feminine-leaning pitch";
        }

        private static float MapPitchScore(float pitchHz)
        {
            if (pitchHz <= 130f)
            {
                return MapRange(pitchHz, 65f, 130f, 0f, 30f);
            }
            if (pitchHz <= 145f)
            {
                return MapRange(pitchHz, 130f, 145f, 30f, 45f);
            }
            if (pitchHz <= 175f)
            {
                return MapRange(pitchHz, 145f, 175f, 45f, 55f);
            }
            if (pitchHz <= 180f)
            {
                return MapRange(pitchHz, 175f, 180f, 55f, 60f);
            }

            return MapRange(pitchHz, 180f, 255f, 60f, 100f);
        }

        private static float MapRange(float value, float inputMin, float inputMax, float outputMin, float outputMax)
        {
            float normalized = Math.Clamp((value - inputMin) / (inputMax - inputMin), 0f, 1f);
            return outputMin + (normalized * (outputMax - outputMin));
        }



        private void ResetMetrics()
        {
            PitchContour.Clear();
            VoiceContour.Clear();
            PitchScore = 50f;
            ResonanceScore = 50f;
            WeightScore = 50f;
            IntonationScore = 0f;
            SpeechPatternsScore = 0f;
            AveragePitchHz = 0f;
            MedianPitchHz = 0f;
            PitchP10Hz = 0f;
            PitchP90Hz = 0f;
            PitchCoverageRatio = 0f;
            RecordingDurationSeconds = 0f;
            AverageResonanceHz = 0f;
            AverageWeightDb = 0f;
            PitchStdDevSemitones = 0f;
            PauseRatio = 0f;
            ArticulationRate = 0f;
            EndingIsRising = false;
            EndingPitchDirection = "Not enough data";
            VoiceClassification = "No speech detected";

            AverageF1Hz = 0f;
            AverageF2Hz = 0f;
            AverageF3Hz = 0f;
            AverageF4Hz = 0f;
            F2F1Ratio = 0f;
            FormantDispersion = 0f;
            SpeakingRate = 0f;
            JitterLocalPct = 0f;
            ShimmerLocalPct = 0f;

            IsPraatAnalysis = false;
            AnalysisError = null;
        }

        private bool TryCalculateResultsWithPraat(string wavPath)
        {
            try
            {
                // Praat paths
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string praatExe = Path.Combine(baseDir, "praat", "praat.exe");
                string praatScript = Path.Combine(baseDir, "praat", "analyze_voice.praat");

                // Fallback to current working dir if executing in debug/release directory but resources are not copied yet (or for design-time/dev-time)
                if (!File.Exists(praatExe))
                {
                    praatExe = Path.Combine(Directory.GetCurrentDirectory(), "praat", "praat.exe");
                    praatScript = Path.Combine(Directory.GetCurrentDirectory(), "praat", "analyze_voice.praat");
                }

                if (!File.Exists(praatExe) || !File.Exists(praatScript))
                {
                    System.Diagnostics.Debug.WriteLine($"Praat not found at: {praatExe}. Falling back to C# DSP.");
                    return false;
                }

                // Set up process
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = praatExe,
                    Arguments = $"--run \"{praatScript}\" \"{wavPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.Unicode,
                    StandardErrorEncoding = System.Text.Encoding.Unicode
                };

                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process == null) return false;

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    // Strip any stray null bytes (UTF-16 decoding artifacts)
                    stdout = stdout.Replace("\0", "");
                    stderr = stderr.Replace("\0", "");

                    if (process.ExitCode != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Praat failed with exit code {process.ExitCode}: {stderr}");
                        return false;
                    }

                    // Parse output
                    ParsePraatOutput(stdout);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error running Praat: {ex.Message}");
                return false;
            }
        }

        private void ParsePraatOutput(string stdout)
        {
            ResetMetrics();
            
            using (var reader = new StringReader(stdout))
            {
                string? line;
                bool parsingTimeSeries = false;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line == "---TIME_SERIES---")
                    {
                        parsingTimeSeries = true;
                        continue;
                    }

                    if (!parsingTimeSeries)
                    {
                        // Parse summary statistics: key: value
                        int colonIndex = line.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string key = line.Substring(0, colonIndex).Trim();
                            string valStr = line.Substring(colonIndex + 1).Trim();
                            
                            // Parse based on key
                            ParseSummaryField(key, valStr);
                        }
                    }
                    else
                    {
                        // Parse CSV row
                        // Skip header row: Time(s),Pitch(Hz),...
                        if (line.StartsWith("Time(s)", StringComparison.OrdinalIgnoreCase)) continue;

                        string[] parts = line.Split(',');
                        if (parts.Length >= 12)
                        {
                            try
                            {
                                var point = new VoiceContourPoint
                                {
                                    TimeSeconds = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                                    PitchHz = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                                    F1Hz = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                                    F2Hz = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                                    F3Hz = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture),
                                    F4Hz = float.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture),
                                    F2F1Ratio = float.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture),
                                    ResonanceHz = float.Parse(parts[7], System.Globalization.CultureInfo.InvariantCulture),
                                    WeightDb = float.Parse(parts[8], System.Globalization.CultureInfo.InvariantCulture),
                                    IntensityDb = float.Parse(parts[9], System.Globalization.CultureInfo.InvariantCulture),
                                    JitterPct = float.Parse(parts[10], System.Globalization.CultureInfo.InvariantCulture),
                                    ShimmerPct = float.Parse(parts[11], System.Globalization.CultureInfo.InvariantCulture)
                                };

                                VoiceContour.Add(point);
                            }
                            catch (FormatException)
                            {
                                // Skip malformed rows
                            }
                        }
                    }
                }
            }

            // Post-process scores and classifications
            PitchScore = MapPitchScore(MedianPitchHz);
            ResonanceScore = Math.Clamp((AverageResonanceHz - 900f) / 1000f * 100f, 0f, 100f);
            WeightScore = Math.Clamp((8f - AverageWeightDb) / 16f * 100f, 0f, 100f);
            IntonationScore = Math.Clamp((PitchStdDevSemitones - 0.6f) / 3.4f * 100f, 0f, 100f);

            float pauseComponent = Math.Clamp(PauseRatio / 0.40f * 100f, 0f, 100f);
            float rateComponent = Math.Clamp((ArticulationRate - 1f) / 5f * 100f, 0f, 100f);
            SpeechPatternsScore = (pauseComponent * 0.60f) + (rateComponent * 0.40f);

            int validPitchCount = VoiceContour.Count(p => p.PitchHz > 0);
            VoiceClassification = DetermineClassification(validPitchCount);
            
            // Sync EndingIsRising
            EndingIsRising = EndingPitchDirection == "Rising";

            // Sync PitchContour for WPF UI Chart rendering and database archiving
            PitchContour.Clear();
            foreach (var point in VoiceContour)
            {
                if (point.PitchHz > 0)
                {
                    PitchContour.Add(new PitchContourPoint
                    {
                        TimeSeconds = point.TimeSeconds,
                        PitchHz = point.PitchHz,
                        Confidence = 1.0f
                    });
                }
            }
        }

        private void ParseSummaryField(string key, string valStr)
        {
            float parseVal(string s) => float.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;

            switch (key)
            {
                case "AveragePitchHz":
                    AveragePitchHz = parseVal(valStr);
                    break;
                case "MedianPitchHz":
                    MedianPitchHz = parseVal(valStr);
                    break;
                case "PitchP10Hz":
                    PitchP10Hz = parseVal(valStr);
                    break;
                case "PitchP90Hz":
                    PitchP90Hz = parseVal(valStr);
                    break;
                case "PitchStdDevSemitones":
                    PitchStdDevSemitones = parseVal(valStr);
                    break;
                case "AverageF1Hz":
                    AverageF1Hz = parseVal(valStr);
                    break;
                case "AverageF2Hz":
                    AverageF2Hz = parseVal(valStr);
                    break;
                case "AverageF3Hz":
                    AverageF3Hz = parseVal(valStr);
                    break;
                case "AverageF4Hz":
                    AverageF4Hz = parseVal(valStr);
                    break;
                case "F2F1Ratio":
                    F2F1Ratio = parseVal(valStr);
                    break;
                case "FormantDispersion":
                    FormantDispersion = parseVal(valStr);
                    break;
                case "AverageResonanceHz":
                    AverageResonanceHz = parseVal(valStr);
                    break;
                case "AverageWeightDb":
                    AverageWeightDb = parseVal(valStr);
                    break;
                case "PauseRatio":
                    PauseRatio = parseVal(valStr);
                    break;
                case "ArticulationRate":
                    ArticulationRate = parseVal(valStr);
                    break;
                case "SpeakingRate":
                    SpeakingRate = parseVal(valStr);
                    break;
                case "EndingPitchDirection":
                    EndingPitchDirection = valStr;
                    break;
                case "JitterLocalPct":
                    JitterLocalPct = parseVal(valStr);
                    break;
                case "ShimmerLocalPct":
                    ShimmerLocalPct = parseVal(valStr);
                    break;
            }
        }
    }
}
