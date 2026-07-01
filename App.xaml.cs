using System;
using System.Text;
using System.Windows;

namespace Voice
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Parse arguments
            bool runDspTests = false;
            bool headless = false;
            
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--test-dsp-headless", StringComparison.OrdinalIgnoreCase))
                {
                    runDspTests = true;
                    headless = true;
                    break;
                }
                else if (arg.Equals("--test-dsp", StringComparison.OrdinalIgnoreCase))
                {
                    runDspTests = true;
                    headless = false;
                    break;
                }
            }

            if (runDspTests)
            {
                RunDiagnosticTests(headless);
                Shutdown(); // Gracefully exit after tests
                return;
            }

            base.OnStartup(e);
        }

        private void RunDiagnosticTests(bool headless)
        {
            var resultsText = new StringBuilder();
            resultsText.AppendLine("=== Voice Lab DSP Diagnostic Engine ===");
            resultsText.AppendLine($"Timestamp: {DateTime.Now}");
            resultsText.AppendLine(".NET Runtime: " + Environment.Version);
            resultsText.AppendLine();

            bool allTestsPassed = true;

            // TEST 1: Male Pitch (120 Hz Sine Wave)
            try
            {
                float testFreq = 120f;
                float[] samples = GenerateSineWave(testFreq, 2048, 44100);
                var metrics = DspProcessor.ProcessFrame(samples, 44100);

                bool passed = metrics.IsVoiced && Math.Abs(metrics.Pitch - testFreq) < 2.0f;
                resultsText.AppendLine($"[Test 1] Male Pitch (Target: {testFreq}Hz): " + (passed ? "PASSED" : "FAILED"));
                resultsText.AppendLine($"  - Detected Pitch: {metrics.Pitch:F1} Hz");
                resultsText.AppendLine($"  - Voiced Flag: {metrics.IsVoiced}");
                if (!passed) allTestsPassed = false;
            }
            catch (Exception ex)
            {
                resultsText.AppendLine($"[Test 1] Male Pitch: EXCEPTION - {ex.Message}");
                allTestsPassed = false;
            }
            resultsText.AppendLine();

            // TEST 2: Female Pitch (220 Hz Sine Wave)
            try
            {
                float testFreq = 220f;
                float[] samples = GenerateSineWave(testFreq, 2048, 44100);
                var metrics = DspProcessor.ProcessFrame(samples, 44100);

                bool passed = metrics.IsVoiced && Math.Abs(metrics.Pitch - testFreq) < 2.0f;
                resultsText.AppendLine($"[Test 2] Female Pitch (Target: {testFreq}Hz): " + (passed ? "PASSED" : "FAILED"));
                resultsText.AppendLine($"  - Detected Pitch: {metrics.Pitch:F1} Hz");
                resultsText.AppendLine($"  - Voiced Flag: {metrics.IsVoiced}");
                if (!passed) allTestsPassed = false;
            }
            catch (Exception ex)
            {
                resultsText.AppendLine($"[Test 2] Female Pitch: EXCEPTION - {ex.Message}");
                allTestsPassed = false;
            }
            resultsText.AppendLine();

            // TEST 3: Resonance Centroid Tracking (1200 Hz Sine Wave)
            try
            {
                float testFreq = 1200f;
                float[] samples = GenerateSineWave(testFreq, 2048, 44100);
                var metrics = DspProcessor.ProcessFrame(samples, 44100);

                // Spectral centroid of a pure 1200Hz sine should be very close to 1200Hz
                bool passed = Math.Abs(metrics.ResonanceCentroid - testFreq) < 50.0f;
                resultsText.AppendLine($"[Test 3] Resonance Centroid (Target: {testFreq}Hz): " + (passed ? "PASSED" : "FAILED"));
                resultsText.AppendLine($"  - Detected Centroid: {metrics.ResonanceCentroid:F1} Hz");
                if (!passed) allTestsPassed = false;
            }
            catch (Exception ex)
            {
                resultsText.AppendLine($"[Test 3] Resonance Centroid: EXCEPTION - {ex.Message}");
                allTestsPassed = false;
            }
            resultsText.AppendLine();

            // TEST 4: Silence Noise Gate
            try
            {
                float[] samples = new float[2048]; // Silence (all zeroes)
                var metrics = DspProcessor.ProcessFrame(samples, 44100);

                bool passed = !metrics.IsVoiced && metrics.Rms < 0.001f;
                resultsText.AppendLine($"[Test 4] Noise Gate (Silence): " + (passed ? "PASSED" : "FAILED"));
                resultsText.AppendLine($"  - Voiced Flag: {metrics.IsVoiced}");
                resultsText.AppendLine($"  - RMS Amplitude: {metrics.Rms:F6}");
                if (!passed) allTestsPassed = false;
            }
            catch (Exception ex)
            {
                resultsText.AppendLine($"[Test 4] Noise Gate: EXCEPTION - {ex.Message}");
                allTestsPassed = false;
            }
            resultsText.AppendLine();

            resultsText.AppendLine("=======================================");
            resultsText.AppendLine(allTestsPassed ? "OVERALL RESULT: ALL TESTS PASSED" : "OVERALL RESULT: SOME TESTS FAILED");

            // Save to log file
            try
            {
                System.IO.File.WriteAllText("dsp_test_results.log", resultsText.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not write log file: {ex.Message}");
            }

            if (!headless)
            {
                MessageBox.Show(resultsText.ToString(), "DSP Diagnostic Results", MessageBoxButton.OK, 
                    allTestsPassed ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }

        private float[] GenerateSineWave(float frequency, int length, int sampleRate)
        {
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                samples[i] = 0.5f * (float)Math.Sin(2.0 * Math.PI * frequency * i / sampleRate);
            }
            return samples;
        }
    }
}
