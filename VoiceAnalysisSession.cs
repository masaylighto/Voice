using System;
using System.Collections.Generic;
using System.Linq;

namespace Voice
{
    public class VoiceAnalysisSession
    {
        public List<FrameMetrics> Frames { get; } = new List<FrameMetrics>();

        // Aggregated Results (0-100 Scales, where 0 is Masculine, 50 is Androgynous, 100 is Feminine)
        public float PitchScore { get; private set; }
        public float ResonanceScore { get; private set; }
        public float WeightScore { get; private set; }
        public float IntonationScore { get; private set; }
        public float SpeechPatternsScore { get; private set; }

        // Raw averages
        public float AveragePitchHz { get; private set; }
        public float AverageResonanceHz { get; private set; }
        public float AverageWeightDb { get; private set; }
        public float PitchStdDevSemitones { get; private set; }
        public float PauseRatio { get; private set; }
        public float ArticulationRate { get; private set; }
        public bool EndingIsRising { get; private set; }

        public string VoiceClassification { get; private set; } = "Calibrating";

        public void AddFrame(FrameMetrics frame)
        {
            Frames.Add(frame);
        }

        public void CalculateResults()
        {
            var voicedFrames = Frames.Where(f => f.IsVoiced).ToList();
            var allFrames = Frames;

            if (allFrames.Count == 0)
            {
                ResetMetrics();
                return;
            }

            // 1. PITCH (Hz)
            if (voicedFrames.Count > 0)
            {
                AveragePitchHz = voicedFrames.Average(f => f.Pitch);
                // Map pitch: 85 Hz (Masculine) to 255 Hz (Feminine)
                PitchScore = Math.Clamp((AveragePitchHz - 85f) / (255f - 85f) * 100f, 0f, 100f);
            }
            else
            {
                AveragePitchHz = 0;
                PitchScore = 50f; // Default neutral if no voiced frames
            }

            // 2. RESONANCE (Spectral Centroid)
            // Resonance centroid applies to all frames with vocal energy (voiced or unvoiced whisper)
            var activeFrames = allFrames.Where(f => f.Rms > 0.005f).ToList();
            if (activeFrames.Count > 0)
            {
                AverageResonanceHz = activeFrames.Average(f => f.ResonanceCentroid);
                // Map resonance: 900 Hz (Masculine / Dark) to 1900 Hz (Feminine / Bright)
                ResonanceScore = Math.Clamp((AverageResonanceHz - 900f) / (1900f - 900f) * 100f, 0f, 100f);
            }
            else
            {
                AverageResonanceHz = 1400f;
                ResonanceScore = 50f;
            }

            // 3. WEIGHT (Spectral Tilt)
            if (voicedFrames.Count > 0)
            {
                AverageWeightDb = voicedFrames.Average(f => f.VocalWeightDb);
                // Map weight: +8 dB (Heavy / Masculine) to -8 dB (Light / Feminine)
                // Let +8dB -> 0, -8dB -> 100.
                WeightScore = Math.Clamp((8.0f - AverageWeightDb) / 16.0f * 100.0f, 0f, 100f);
            }
            else
            {
                AverageWeightDb = 0;
                WeightScore = 50f;
            }

            // 4. INTONATION (Pitch Standard Deviation in Semitones)
            if (voicedFrames.Count > 5)
            {
                // Convert pitch to semitones relative to 55Hz (A1)
                var semitones = voicedFrames.Select(f => 12.0 * Math.Log2(f.Pitch / 55.0)).ToList();
                double avgSemitone = semitones.Average();
                double variance = semitones.Sum(s => Math.Pow(s - avgSemitone, 2)) / semitones.Count;
                PitchStdDevSemitones = (float)Math.Sqrt(variance);

                // Map standard deviation: 1.0 semitone (Monotonic / Masculine) to 4.5 semitones (Melodic / Feminine)
                IntonationScore = Math.Clamp((PitchStdDevSemitones - 1.0f) / 3.5f * 100.0f, 0f, 100f);
            }
            else
            {
                PitchStdDevSemitones = 0;
                IntonationScore = 50f;
            }

            // 5. SPEECH PATTERNS
            // (a) Pause Ratio
            int pauseFrames = allFrames.Count(f => f.Rms < 0.01f);
            PauseRatio = (float)pauseFrames / allFrames.Count;

            // (b) Articulation rate estimation: count envelope peaks
            int syllables = CountEnvelopePeaks(allFrames);
            float totalDurationSeconds = allFrames.Count * 2048f / 44100f;
            float activeDurationSeconds = (allFrames.Count - pauseFrames) * 2048f / 44100f;
            
            ArticulationRate = activeDurationSeconds > 0.5f ? (syllables / activeDurationSeconds) : 0f;

            // (c) End-of-sentence pitch contour
            EndingIsRising = DeterminePitchEndingIsRising(voicedFrames);

            // Compute Speech Pattern Score: 0 is Masculine (flat endings, direct, faster), 
            // 50 is Androgynous, 100 is Feminine (breathier pauses, rising ending sweeps, expressive pacing)
            float patternScore = 50f;
            
            // Adjust based on pauses: average normal pause ratio is 0.2
            // Breathy/more pauses leans feminine (+15 points max)
            float pauseBonus = Math.Clamp((PauseRatio - 0.15f) * 100f, -20f, 20f);
            patternScore += pauseBonus;

            // Adjust based on ending inflection
            if (voicedFrames.Count > 5)
            {
                if (EndingIsRising)
                {
                    patternScore += 25f; // Rising inflection (uptalk) -> Feminine
                }
                else
                {
                    patternScore -= 20f; // Falling inflection -> Masculine
                }
            }

            // Articulation rate influence: variable/fluid pacing leans feminine, steady/driving leans masculine.
            // Let's use a mild scaling
            float rateModifier = Math.Clamp((ArticulationRate - 3.0f) * 10f, -10f, 10f);
            patternScore += rateModifier;

            SpeechPatternsScore = Math.Clamp(patternScore, 0f, 100f);

            // Final Voice Classification
            VoiceClassification = DetermineClassification();
        }

        private void ResetMetrics()
        {
            PitchScore = 50;
            ResonanceScore = 50;
            WeightScore = 50;
            IntonationScore = 50;
            SpeechPatternsScore = 50;
            AveragePitchHz = 0;
            AverageResonanceHz = 0;
            AverageWeightDb = 0;
            PitchStdDevSemitones = 0;
            PauseRatio = 0;
            ArticulationRate = 0;
            EndingIsRising = false;
            VoiceClassification = "No Speech Detected";
        }

        private int CountEnvelopePeaks(List<FrameMetrics> frames)
        {
            if (frames.Count < 5) return 0;

            // Smooth the RMS envelope using a moving average
            float[] smoothedRms = new float[frames.Count];
            int window = 5; // ~230ms window at 46ms frames
            
            for (int i = 0; i < frames.Count; i++)
            {
                float sum = 0;
                int count = 0;
                for (int w = -window / 2; w <= window / 2; w++)
                {
                    int idx = i + w;
                    if (idx >= 0 && idx < frames.Count)
                    {
                        sum += frames[idx].Rms;
                        count++;
                    }
                }
                smoothedRms[i] = sum / count;
            }

            // Count peaks (syllables) that are local maxima and above threshold
            int peakCount = 0;
            float rmsThreshold = 0.02f; // Min amplitude to be considered a syllable
            
            for (int i = 1; i < frames.Count - 1; i++)
            {
                if (smoothedRms[i] > rmsThreshold && 
                    smoothedRms[i] > smoothedRms[i - 1] && 
                    smoothedRms[i] > smoothedRms[i + 1])
                {
                    peakCount++;
                    i += 2; // skip immediate neighbours to prevent double counting
                }
            }

            return peakCount;
        }

        private bool DeterminePitchEndingIsRising(List<FrameMetrics> voicedFrames)
        {
            if (voicedFrames.Count < 6) return false;

            // Look at the last 5 voiced frames in the session
            var lastFrames = voicedFrames.TakeLast(5).ToList();
            
            // Perform simple linear regression on pitch values
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            int n = lastFrames.Count;
            
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += lastFrames[i].Pitch;
                sumXY += i * lastFrames[i].Pitch;
                sumXX += i * i;
            }

            double slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            return slope > 3.0; // Positive slope of 3Hz/frame indicates a clear rising sweep
        }

        private string DetermineClassification()
        {
            // We use a weighted combination of Pitch and Resonance as the core indicators
            float coreGenderScore = (PitchScore * 0.6f) + (ResonanceScore * 0.4f);

            if (coreGenderScore < 30f)
            {
                if (WeightScore < 30f)
                    return "Deep Masculine";
                else
                    return "Light Masculine";
            }
            else if (coreGenderScore < 45f)
            {
                return "Masculine-leaning Androgynous";
            }
            else if (coreGenderScore < 55f)
            {
                return "Balanced Androgynous";
            }
            else if (coreGenderScore < 70f)
            {
                return "Feminine-leaning Androgynous";
            }
            else
            {
                if (WeightScore > 70f)
                    return "Bright Feminine";
                else
                    return "Full Feminine";
            }
        }
    }
}
