using System;
using NAudio.Dsp;

namespace Voice
{
    public struct FrameMetrics
    {
        public float Pitch;             // Hz (0 if unvoiced)
        public float PitchConfidence;   // 0-1 confidence from the F0 estimator
        public float TimeSeconds;       // Start time of the analysis frame
        public float ResonanceCentroid; // Hz
        public float VocalWeightDb;     // dB (low vs mid-high spectral energy)
        public float Rms;               // RMS amplitude
        public bool IsVoiced;           // True when F0 passed the confidence gate
    }

    public static class DspProcessor
    {
        public const int AnalysisFrameSize = 2048;
        public const int AnalysisHopSize = 1024;

        private const float MinimumPitchHz = 65f;
        private const float MaximumPitchHz = 500f;
        private const float YinThreshold = 0.15f;
        private const float MinimumPitchConfidence = 0.70f;
        private static readonly float[] HannWindowCache;

        static DspProcessor()
        {
            HannWindowCache = new float[AnalysisFrameSize];
            for (int i = 0; i < AnalysisFrameSize; i++)
            {
                HannWindowCache[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (AnalysisFrameSize - 1)));
            }
        }

        /// <summary>
        /// Processes one mono analysis frame. Pitch is estimated with YIN's cumulative
        /// mean normalized difference function, which is less prone to harmonic peaks
        /// and octave errors than selecting the largest autocorrelation peak.
        /// </summary>
        public static FrameMetrics ProcessFrame(float[] samples, int sampleRate = 44100)
        {
            if (samples.Length < AnalysisFrameSize)
            {
                return new FrameMetrics();
            }

            float sumSq = 0;
            for (int i = 0; i < AnalysisFrameSize; i++)
            {
                sumSq += samples[i] * samples[i];
            }

            float rms = (float)Math.Sqrt(sumSq / AnalysisFrameSize);
            if (rms < 0.008f)
            {
                return new FrameMetrics { Rms = rms };
            }

            PitchEstimate pitchEstimate = DetectPitchYin(samples, sampleRate);
            bool isVoiced = pitchEstimate.Confidence >= MinimumPitchConfidence
                && pitchEstimate.Hz >= MinimumPitchHz
                && pitchEstimate.Hz <= MaximumPitchHz;

            Complex[] fftBuffer = new Complex[AnalysisFrameSize];
            for (int i = 0; i < AnalysisFrameSize; i++)
            {
                fftBuffer[i].X = samples[i] * HannWindowCache[i];
                fftBuffer[i].Y = 0.0f;
            }

            int m = (int)Math.Log2(AnalysisFrameSize);
            FastFourierTransform.FFT(true, m, fftBuffer);

            int numBins = AnalysisFrameSize / 2;
            float binWidth = (float)sampleRate / AnalysisFrameSize;
            float[] magnitudes = new float[numBins];
            for (int i = 0; i < numBins; i++)
            {
                float real = fftBuffer[i].X;
                float imag = fftBuffer[i].Y;
                magnitudes[i] = (float)Math.Sqrt(real * real + imag * imag);
            }

            float centroid = CalculateSpectralCentroid(magnitudes, binWidth, 300f, 3000f);
            float weightDb = CalculateSpectralBalance(magnitudes, binWidth);

            return new FrameMetrics
            {
                Pitch = isVoiced ? pitchEstimate.Hz : 0,
                PitchConfidence = pitchEstimate.Confidence,
                ResonanceCentroid = centroid,
                VocalWeightDb = weightDb,
                Rms = rms,
                IsVoiced = isVoiced
            };
        }

        private static float CalculateSpectralCentroid(float[] magnitudes, float binWidth, float minFrequency, float maxFrequency)
        {
            int minBin = Math.Clamp((int)(minFrequency / binWidth), 0, magnitudes.Length - 1);
            int maxBin = Math.Clamp((int)(maxFrequency / binWidth), 0, magnitudes.Length - 1);
            float sumFreqMag = 0;
            float sumMag = 0;

            for (int i = minBin; i <= maxBin; i++)
            {
                float magnitude = magnitudes[i];
                sumFreqMag += i * binWidth * magnitude;
                sumMag += magnitude;
            }

            return sumMag > 1e-6f ? sumFreqMag / sumMag : 0;
        }

        private static float CalculateSpectralBalance(float[] magnitudes, float binWidth)
        {
            float lowEnergy = CalculateAverageBandPower(magnitudes, binWidth, 80f, 250f);
            float midHighEnergy = CalculateAverageBandPower(magnitudes, binWidth, 250f, 3000f);

            if (lowEnergy <= 1e-10f || midHighEnergy <= 1e-10f)
            {
                return 0;
            }

            return 10f * (float)Math.Log10(lowEnergy / midHighEnergy);
        }

        private static float CalculateAverageBandPower(float[] magnitudes, float binWidth, float minFrequency, float maxFrequency)
        {
            int minBin = Math.Clamp((int)(minFrequency / binWidth), 0, magnitudes.Length - 1);
            int maxBin = Math.Clamp((int)(maxFrequency / binWidth), 0, magnitudes.Length - 1);
            float energy = 0;
            int count = 0;

            for (int i = minBin; i <= maxBin; i++)
            {
                energy += magnitudes[i] * magnitudes[i];
                count++;
            }

            return count == 0 ? 0 : energy / count;
        }

        private static PitchEstimate DetectPitchYin(float[] samples, int sampleRate)
        {
            int minLag = Math.Max(2, (int)Math.Floor(sampleRate / MaximumPitchHz));
            int maxLag = Math.Min(AnalysisFrameSize - 2, (int)Math.Ceiling(sampleRate / MinimumPitchHz));
            if (maxLag <= minLag)
            {
                return default;
            }

            // YIN is sensitive to DC offset, so center the frame before calculating differences.
            float mean = 0;
            for (int i = 0; i < AnalysisFrameSize; i++)
            {
                mean += samples[i];
            }
            mean /= AnalysisFrameSize;

            float[] centered = new float[AnalysisFrameSize];
            for (int i = 0; i < AnalysisFrameSize; i++)
            {
                centered[i] = samples[i] - mean;
            }

            double[] cmndf = new double[maxLag + 1];
            double runningDifferenceSum = 0;

            for (int lag = 1; lag <= maxLag; lag++)
            {
                double difference = 0;
                int sampleCount = AnalysisFrameSize - lag;
                for (int i = 0; i < sampleCount; i++)
                {
                    double delta = centered[i] - centered[i + lag];
                    difference += delta * delta;
                }

                runningDifferenceSum += difference;
                cmndf[lag] = runningDifferenceSum > 1e-12
                    ? difference * lag / runningDifferenceSum
                    : 1.0;
            }

            int bestLag = -1;
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                if (cmndf[lag] < YinThreshold)
                {
                    while (lag + 1 <= maxLag && cmndf[lag + 1] < cmndf[lag])
                    {
                        lag++;
                    }

                    bestLag = lag;
                    break;
                }
            }

            if (bestLag < 0)
            {
                double lowestValue = double.MaxValue;
                for (int lag = minLag + 1; lag < maxLag; lag++)
                {
                    if (cmndf[lag] <= cmndf[lag - 1]
                        && cmndf[lag] < cmndf[lag + 1]
                        && cmndf[lag] < lowestValue)
                    {
                        bestLag = lag;
                        lowestValue = cmndf[lag];
                    }
                }

                if (bestLag < 0 || cmndf[bestLag] > 0.30)
                {
                    return default;
                }
            }

            double refinedLag = bestLag;
            if (bestLag > minLag && bestLag < maxLag)
            {
                double before = cmndf[bestLag - 1];
                double center = cmndf[bestLag];
                double after = cmndf[bestLag + 1];
                double denominator = before - (2.0 * center) + after;
                if (Math.Abs(denominator) > 1e-12)
                {
                    double offset = 0.5 * (before - after) / denominator;
                    refinedLag += Math.Clamp(offset, -0.5, 0.5);
                }
            }

            float confidence = Math.Clamp(1f - (float)cmndf[bestLag], 0f, 1f);
            float hz = (float)(sampleRate / refinedLag);
            return new PitchEstimate(hz, confidence);
        }

        private readonly struct PitchEstimate
        {
            public PitchEstimate(float hz, float confidence)
            {
                Hz = hz;
                Confidence = confidence;
            }

            public float Hz { get; }
            public float Confidence { get; }
        }
    }
}
