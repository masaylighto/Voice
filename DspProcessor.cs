using System;
using NAudio.Dsp;

namespace Voice
{
    public struct FrameMetrics
    {
        public float Pitch;             // Hz (0 if unvoiced)
        public float ResonanceCentroid; // Hz
        public float VocalWeightDb;     // dB (low band vs mid-high band energy)
        public float Rms;               // RMS amplitude
        public bool IsVoiced;           // True if pitch was successfully detected and signal is voiced
    }

    public static class DspProcessor
    {
        private const int FftLength = 2048;
        private static readonly float[] HannWindowCache;

        static DspProcessor()
        {
            // Cache Hanning window coefficients
            HannWindowCache = new float[FftLength];
            for (int i = 0; i < FftLength; i++)
            {
                HannWindowCache[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (FftLength - 1)));
            }
        }

        /// <summary>
        /// Processes a 2048-sample frame of 16-bit mono 44.1kHz audio (passed as floats in range -1.0 to 1.0).
        /// </summary>
        public static FrameMetrics ProcessFrame(float[] samples, int sampleRate = 44100)
        {
            if (samples.Length < FftLength)
            {
                return new FrameMetrics { Pitch = 0, ResonanceCentroid = 0, VocalWeightDb = 0, Rms = 0, IsVoiced = false };
            }

            // 1. Calculate RMS Amplitude
            float sumSq = 0;
            for (int i = 0; i < FftLength; i++)
            {
                sumSq += samples[i] * samples[i];
            }
            float rms = (float)Math.Sqrt(sumSq / FftLength);

            // Silence threshold: ignore frames that are too quiet (e.g. RMS < 0.01)
            if (rms < 0.008f)
            {
                return new FrameMetrics { Pitch = 0, ResonanceCentroid = 0, VocalWeightDb = 0, Rms = rms, IsVoiced = false };
            }

            // 2. Pitch Detection using Center-Clipped Autocorrelation
            float pitch = DetectPitchAutocorrelation(samples, sampleRate);
            bool isVoiced = pitch > 50.0f && pitch < 500.0f;

            // 3. Spectral Analysis (FFT) for Resonance and Weight
            Complex[] fftBuffer = new Complex[FftLength];
            for (int i = 0; i < FftLength; i++)
            {
                fftBuffer[i].X = samples[i] * HannWindowCache[i];
                fftBuffer[i].Y = 0.0f;
            }

            int m = (int)Math.Log2(FftLength);
            FastFourierTransform.FFT(true, m, fftBuffer);

            // Calculate magnitudes of positive frequencies
            int numBins = FftLength / 2;
            float binWidth = (float)sampleRate / FftLength;
            float[] magnitudes = new float[numBins];
            
            for (int i = 0; i < numBins; i++)
            {
                float real = fftBuffer[i].X;
                float imag = fftBuffer[i].Y;
                magnitudes[i] = (float)Math.Sqrt(real * real + imag * imag);
            }

            // 4. Calculate Resonance (Spectral Centroid in speech band: 300Hz - 3000Hz)
            float sumFreqMag = 0;
            float sumMag = 0;
            
            int minCentroidBin = (int)(300f / binWidth);
            int maxCentroidBin = (int)(3000f / binWidth);
            
            minCentroidBin = Math.Clamp(minCentroidBin, 0, numBins - 1);
            maxCentroidBin = Math.Clamp(maxCentroidBin, 0, numBins - 1);

            for (int i = minCentroidBin; i <= maxCentroidBin; i++)
            {
                float freq = i * binWidth;
                sumFreqMag += freq * magnitudes[i];
                sumMag += magnitudes[i];
            }

            float centroid = sumMag > 1e-6f ? (sumFreqMag / sumMag) : 1200f; // default to neutral if empty

            // 5. Calculate Vocal Weight (Energy in low band 80-250Hz vs mid-high band 250-3000Hz)
            float energyLow = 0;
            float energyMidHigh = 0;

            int lowBandMinBin = (int)(80f / binWidth);
            int lowBandMaxBin = (int)(250f / binWidth);
            int midHighBandMinBin = (int)(250f / binWidth);
            int midHighBandMaxBin = (int)(3000f / binWidth);

            lowBandMinBin = Math.Clamp(lowBandMinBin, 0, numBins - 1);
            lowBandMaxBin = Math.Clamp(lowBandMaxBin, 0, numBins - 1);
            midHighBandMinBin = Math.Clamp(midHighBandMinBin, 0, numBins - 1);
            midHighBandMaxBin = Math.Clamp(midHighBandMaxBin, 0, numBins - 1);

            for (int i = lowBandMinBin; i <= lowBandMaxBin; i++)
            {
                energyLow += magnitudes[i] * magnitudes[i];
            }

            for (int i = midHighBandMinBin; i <= midHighBandMaxBin; i++)
            {
                energyMidHigh += magnitudes[i] * magnitudes[i];
            }

            // Express as a ratio in decibels
            float weightDb = 0;
            if (energyLow > 1e-10f && energyMidHigh > 1e-10f)
            {
                weightDb = 10f * (float)Math.Log10(energyLow / energyMidHigh);
            }
            else if (energyLow > 1e-10f)
            {
                weightDb = 15f; // high weight
            }
            else
            {
                weightDb = -15f; // low weight
            }

            return new FrameMetrics
            {
                Pitch = isVoiced ? pitch : 0,
                ResonanceCentroid = centroid,
                VocalWeightDb = weightDb,
                Rms = rms,
                IsVoiced = isVoiced
            };
        }

        /// <summary>
        /// Detects pitch using autocorrelation with center clipping.
        /// Returns 0 if no clear pitch is found.
        /// </summary>
        private static float DetectPitchAutocorrelation(float[] samples, int sampleRate)
        {
            // Find max amplitude in the frame
            float maxVal = 0;
            for (int i = 0; i < FftLength; i++)
            {
                float absVal = Math.Abs(samples[i]);
                if (absVal > maxVal) maxVal = absVal;
            }

            if (maxVal < 1e-5f) return 0;

            // Apply center clipping (Sondhi's center clipping, threshold = 0.35 * max)
            float clipLevel = maxVal * 0.35f;
            float[] clipped = new float[FftLength];
            for (int i = 0; i < FftLength; i++)
            {
                float val = samples[i];
                if (val > clipLevel) clipped[i] = val - clipLevel;
                else if (val < -clipLevel) clipped[i] = val + clipLevel;
                else clipped[i] = 0.0f;
            }

            // Pitch range: 50 Hz to 500 Hz
            int minLag = sampleRate / 500; // ~88 samples at 44.1kHz
            int maxLag = sampleRate / 50;  // ~882 samples at 44.1kHz

            float[] r = new float[maxLag + 1];
            float r0 = 0; // Autocorrelation at lag 0

            for (int i = 0; i < FftLength; i++)
            {
                r0 += clipped[i] * clipped[i];
            }

            if (r0 < 1e-7f) return 0;

            // Compute autocorrelation for lags in target range
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float sum = 0;
                int maxIdx = FftLength - lag;
                for (int i = 0; i < maxIdx; i++)
                {
                    sum += clipped[i] * clipped[i + lag];
                }
                r[lag] = sum;
            }

            // Find the highest peak in the search range
            int peakLag = -1;
            float maxR = -1;

            // Standard peak detection: must be a local maximum and exceed the threshold
            for (int lag = minLag; lag <= maxLag; lag++)
            {
                if (r[lag] > maxR)
                {
                    // Check if it is a local maximum
                    if (lag > minLag && lag < maxLag)
                    {
                        if (r[lag] > r[lag - 1] && r[lag] > r[lag + 1])
                        {
                            maxR = r[lag];
                            peakLag = lag;
                        }
                    }
                    else
                    {
                        maxR = r[lag];
                        peakLag = lag;
                    }
                }
            }

            // Verify peak confidence (autocorrelation coefficient threshold, typically > 0.25)
            float threshold = 0.25f * r0;
            if (peakLag == -1 || maxR < threshold)
            {
                return 0; // Unvoiced / too weak
            }

            // Parabolic interpolation for sub-sample accuracy
            float exactLag = peakLag;
            if (peakLag > minLag && peakLag < maxLag)
            {
                float alpha = r[peakLag - 1];
                float beta = r[peakLag];
                float gamma = r[peakLag + 1];
                float denominator = alpha - 2 * beta + gamma;
                if (Math.Abs(denominator) > 1e-7f)
                {
                    float p = 0.5f * (alpha - gamma) / denominator;
                    exactLag += p;
                }
            }

            float pitch = (float)sampleRate / exactLag;
            if (pitch >= 50.0f && pitch <= 500.0f)
            {
                return pitch;
            }

            return 0;
        }
    }
}
