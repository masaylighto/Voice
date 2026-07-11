using System;
using System.Collections.Generic;
using System.Linq;

namespace Voice
{
    public class PitchContourPoint
    {
        public float TimeSeconds { get; set; }
        public float PitchHz { get; set; }
        public float Confidence { get; set; }
    }

    public class VoiceAnalysisSession
    {
        private const float SampleRate = 44100f;
        private const int MinimumFramesForPitchProfile = 20;

        public List<FrameMetrics> Frames { get; } = new List<FrameMetrics>();
        public List<PitchContourPoint> PitchContour { get; } = new List<PitchContourPoint>();

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

        public string VoiceClassification { get; private set; } = "Calibrating";

        public void AddFrame(FrameMetrics frame)
        {
            Frames.Add(frame);
        }

        public void CalculateResults()
        {
            if (Frames.Count == 0)
            {
                ResetMetrics();
                return;
            }

            RecordingDurationSeconds = CalculateDurationSeconds();
            BuildPitchContour();

            List<float> validPitch = PitchContour
                .Where(point => point.PitchHz > 0)
                .Select(point => point.PitchHz)
                .ToList();

            PitchCoverageRatio = (float)validPitch.Count / Frames.Count;
            if (validPitch.Count >= 5)
            {
                MedianPitchHz = Percentile(validPitch, 0.50f);
                PitchP10Hz = Percentile(validPitch, 0.10f);
                PitchP90Hz = Percentile(validPitch, 0.90f);
                AveragePitchHz = TrimmedMean(validPitch, 0.10f);
                PitchScore = MapPitchScore(MedianPitchHz);

                List<double> semitones = validPitch
                    .Select(pitch => 12.0 * Math.Log2(pitch / 55.0))
                    .ToList();
                PitchStdDevSemitones = StandardDeviation(semitones);
                IntonationScore = Math.Clamp((PitchStdDevSemitones - 0.6f) / 3.4f * 100f, 0f, 100f);
            }
            else
            {
                AveragePitchHz = 0;
                MedianPitchHz = 0;
                PitchP10Hz = 0;
                PitchP90Hz = 0;
                PitchScore = 50f;
                PitchStdDevSemitones = 0;
                IntonationScore = 0;
            }

            List<FrameMetrics> activeFrames = Frames
                .Where(frame => frame.Rms >= 0.008f && frame.ResonanceCentroid > 0)
                .ToList();
            if (activeFrames.Count > 0)
            {
                AverageResonanceHz = activeFrames.Average(frame => frame.ResonanceCentroid);
                ResonanceScore = Math.Clamp((AverageResonanceHz - 900f) / 1000f * 100f, 0f, 100f);
            }
            else
            {
                AverageResonanceHz = 0;
                ResonanceScore = 50f;
            }

            List<FrameMetrics> voicedFrames = Frames
                .Where(frame => frame.IsVoiced && frame.PitchConfidence >= 0.70f)
                .ToList();
            if (voicedFrames.Count > 0)
            {
                AverageWeightDb = TrimmedMean(voicedFrames.Select(frame => frame.VocalWeightDb).ToList(), 0.10f);
                WeightScore = Math.Clamp((8f - AverageWeightDb) / 16f * 100f, 0f, 100f);
            }
            else
            {
                AverageWeightDb = 0;
                WeightScore = 50f;
            }

            int pauseFrames = Frames.Count(frame => frame.Rms < 0.008f);
            PauseRatio = (float)pauseFrames / Frames.Count;
            int syllableEstimate = CountEnvelopePeaks(Frames);
            float activeDurationSeconds = (Frames.Count - pauseFrames) * DspProcessor.AnalysisHopSize / SampleRate;
            ArticulationRate = activeDurationSeconds >= 0.5f
                ? syllableEstimate / activeDurationSeconds
                : 0f;

            // This is a descriptive pacing scale, not a gendered classification.
            float pauseComponent = Math.Clamp(PauseRatio / 0.40f * 100f, 0f, 100f);
            float rateComponent = Math.Clamp((ArticulationRate - 1f) / 5f * 100f, 0f, 100f);
            SpeechPatternsScore = (pauseComponent * 0.60f) + (rateComponent * 0.40f);

            EndingPitchDirection = DeterminePitchEndingDirection(PitchContour);
            EndingIsRising = EndingPitchDirection == "Rising";
            VoiceClassification = DetermineClassification(validPitch.Count);
        }

        private void BuildPitchContour()
        {
            PitchContour.Clear();
            float[] filteredPitch = new float[Frames.Count];

            for (int i = 0; i < Frames.Count; i++)
            {
                FrameMetrics frame = Frames[i];
                if (frame.IsVoiced
                    && frame.PitchConfidence >= 0.70f
                    && frame.Pitch >= 65f
                    && frame.Pitch <= 500f)
                {
                    filteredPitch[i] = frame.Pitch;
                }
            }

            // Apply a short median filter inside each voiced phrase. This removes isolated
            // octave errors without smoothing across pauses or hiding intentional pitch motion.
            float[] smoothedPitch = (float[])filteredPitch.Clone();
            int segmentStart = 0;
            while (segmentStart < filteredPitch.Length)
            {
                while (segmentStart < filteredPitch.Length && filteredPitch[segmentStart] <= 0)
                {
                    segmentStart++;
                }

                int segmentEnd = segmentStart;
                while (segmentEnd < filteredPitch.Length && filteredPitch[segmentEnd] > 0)
                {
                    segmentEnd++;
                }

                for (int i = segmentStart; i < segmentEnd; i++)
                {
                    List<float> neighborhood = new List<float>(3);
                    for (int offset = -1; offset <= 1; offset++)
                    {
                        int neighbor = i + offset;
                        if (neighbor >= segmentStart && neighbor < segmentEnd)
                        {
                            neighborhood.Add(filteredPitch[neighbor]);
                        }
                    }

                    smoothedPitch[i] = Percentile(neighborhood, 0.50f);
                }
                segmentStart = segmentEnd + 1;
            }

            filteredPitch = smoothedPitch;
            segmentStart = 0;
            while (segmentStart < filteredPitch.Length)
            {
                while (segmentStart < filteredPitch.Length && filteredPitch[segmentStart] <= 0)
                {
                    segmentStart++;
                }

                int segmentEnd = segmentStart;
                while (segmentEnd < filteredPitch.Length && filteredPitch[segmentEnd] > 0)
                {
                    segmentEnd++;
                }

                // Reject only extreme single-frame jumps. A real speaking contour does not
                // move eight semitones and immediately return within one 23 ms hop.
                for (int i = segmentStart + 1; i < segmentEnd - 1; i++)
                {
                    float localMedian = Percentile(new List<float>
                    {
                        filteredPitch[i - 1], filteredPitch[i], filteredPitch[i + 1]
                    }, 0.50f);

                    if (Math.Abs(ToSemitones(filteredPitch[i]) - ToSemitones(localMedian)) > 8f)
                    {
                        filteredPitch[i] = 0;
                    }
                }

                segmentStart = segmentEnd + 1;
            }

            for (int i = 0; i < Frames.Count; i++)
            {
                float timeSeconds = Frames[i].TimeSeconds;
                if (i > 0 && timeSeconds <= 0)
                {
                    timeSeconds = i * DspProcessor.AnalysisHopSize / SampleRate;
                }

                PitchContour.Add(new PitchContourPoint
                {
                    TimeSeconds = timeSeconds,
                    PitchHz = filteredPitch[i],
                    Confidence = Frames[i].PitchConfidence
                });
            }
        }

        private float CalculateDurationSeconds()
        {
            if (Frames.Count == 0)
            {
                return 0;
            }

            float lastFrameTime = Frames[^1].TimeSeconds;
            if (lastFrameTime > 0)
            {
                return lastFrameTime + (DspProcessor.AnalysisFrameSize / SampleRate);
            }

            return ((Frames.Count - 1) * DspProcessor.AnalysisHopSize + DspProcessor.AnalysisFrameSize) / SampleRate;
        }

        private static int CountEnvelopePeaks(List<FrameMetrics> frames)
        {
            if (frames.Count < 6)
            {
                return 0;
            }

            const int smoothingWindow = 5;
            float[] smoothed = new float[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                float sum = 0;
                int count = 0;
                for (int offset = -smoothingWindow / 2; offset <= smoothingWindow / 2; offset++)
                {
                    int index = i + offset;
                    if (index >= 0 && index < frames.Count)
                    {
                        sum += frames[index].Rms;
                        count++;
                    }
                }
                smoothed[i] = sum / count;
            }

            float threshold = Math.Max(0.012f, smoothed.Where(value => value > 0).DefaultIfEmpty(0).Average() * 0.60f);
            int peakCount = 0;
            int lastPeak = -4;
            for (int i = 1; i < smoothed.Length - 1; i++)
            {
                if (i - lastPeak >= 4
                    && smoothed[i] >= threshold
                    && smoothed[i] >= smoothed[i - 1]
                    && smoothed[i] > smoothed[i + 1])
                {
                    peakCount++;
                    lastPeak = i;
                }
            }

            return peakCount;
        }

        private static string DeterminePitchEndingDirection(List<PitchContourPoint> contour)
        {
            int end = contour.FindLastIndex(point => point.PitchHz > 0);
            if (end < 0)
            {
                return "Not enough data";
            }

            int start = end;
            while (start > 0
                && contour[start - 1].PitchHz > 0
                && contour[start].TimeSeconds - contour[start - 1].TimeSeconds <= 0.05f)
            {
                start--;
            }

            float latestTime = contour[end].TimeSeconds;
            List<PitchContourPoint> phraseEnding = contour
                .Skip(start)
                .Take(end - start + 1)
                .Where(point => point.TimeSeconds >= latestTime - 0.50f)
                .ToList();

            if (phraseEnding.Count < 5)
            {
                return "Not enough data";
            }

            float changeSemitones = ToSemitones(phraseEnding[^1].PitchHz) - ToSemitones(phraseEnding[0].PitchHz);
            if (changeSemitones >= 1.0f)
            {
                return "Rising";
            }
            if (changeSemitones <= -1.0f)
            {
                return "Falling";
            }

            return "Level";
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

        private static float TrimmedMean(List<float> values, float trimRatio)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            List<float> sorted = values.OrderBy(value => value).ToList();
            int trim = sorted.Count >= 10 ? (int)Math.Floor(sorted.Count * trimRatio) : 0;
            return sorted.Skip(trim).Take(sorted.Count - (trim * 2)).Average();
        }

        private static float Percentile(List<float> values, float percentile)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            List<float> sorted = values.OrderBy(value => value).ToList();
            float index = (sorted.Count - 1) * Math.Clamp(percentile, 0f, 1f);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);
            if (lower == upper)
            {
                return sorted[lower];
            }

            float fraction = index - lower;
            return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
        }

        private static float StandardDeviation(List<double> values)
        {
            if (values.Count == 0)
            {
                return 0;
            }

            double mean = values.Average();
            double variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Count;
            return (float)Math.Sqrt(variance);
        }

        private static float ToSemitones(float pitchHz)
        {
            return 12f * (float)Math.Log2(pitchHz / 55f);
        }

        private void ResetMetrics()
        {
            PitchContour.Clear();
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
        }
    }
}
