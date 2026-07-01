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

            bool buildIcon = false;
            foreach (var arg in e.Args)
            {
                if (arg.Equals("--build-icon", StringComparison.OrdinalIgnoreCase))
                {
                    buildIcon = true;
                    break;
                }
            }

            if (buildIcon)
            {
                BuildAppIcon();
                Shutdown();
                return;
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

        private void BuildAppIcon()
        {
            try
            {
                string pngPath = @"d:\Coding\Voice\assets\app_icon.png";
                string outPngPath = @"d:\Coding\Voice\assets\app_icon_256.png";
                string icoPath = @"d:\Coding\Voice\assets\app_icon.ico";

                if (!System.IO.File.Exists(pngPath))
                {
                    Console.WriteLine("PNG file not found: " + pngPath);
                    return;
                }

                // Load PNG using WPF tools with OnLoad caching to prevent file locking
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(pngPath);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                // Resize to 256x256
                var scale = new System.Windows.Media.ScaleTransform(256.0 / bitmap.PixelWidth, 256.0 / bitmap.PixelHeight);
                var resized = new System.Windows.Media.Imaging.TransformedBitmap(bitmap, scale);

                // Save resized PNG
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(resized));
                using (var fs = System.IO.File.OpenWrite(outPngPath))
                {
                    encoder.Save(fs);
                }

                // Read bytes of resized PNG
                byte[] pngBytes = System.IO.File.ReadAllBytes(outPngPath);
                int pngSize = pngBytes.Length;

                // Write ICO file
                using (var fs = System.IO.File.Create(icoPath))
                {
                    // ICO Header
                    fs.Write(new byte[] { 0, 0, 1, 0, 1, 0 }, 0, 6);
                    // Directory Entry
                    fs.Write(new byte[] { 0, 0, 0, 0, 1, 0, 32, 0 }, 0, 8); // Width=0, Height=0, Colors=0, Reserved=0, Planes=1, BPP=32
                    fs.Write(BitConverter.GetBytes((uint)pngSize), 0, 4);   // Size
                    fs.Write(BitConverter.GetBytes((uint)22), 0, 4);        // Offset (22)
                    // PNG Data
                    fs.Write(pngBytes, 0, pngSize);
                }

                // Overwrite original png with the 256x256 one
                System.IO.File.Delete(pngPath);
                System.IO.File.Move(outPngPath, pngPath);
                Console.WriteLine("Icon generated successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error building icon: " + ex.Message);
            }
        }
    }
}
