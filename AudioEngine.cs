using System;
using System.IO;
using System.Collections.Generic;
using NAudio.Wave;

namespace Voice
{
    public class AudioEngine : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _waveWriter;
        private string? _tempWavPath;
        private readonly List<float> _sampleBuffer = new List<float>();
        private readonly VoiceAnalysisSession _currentSession = new VoiceAnalysisSession();
        private bool _isRecording;

        // Callbacks
        public event Action<FrameMetrics>? LiveFrameProcessed;
        public event Action<VoiceAnalysisSession>? RecordingFinished;
        public event Action<float[]>? RawSamplesCaptured; // For oscilloscope/waveform drawing

        public bool IsRecording => _isRecording;
        public string? TempWavPath => _tempWavPath;

        public void StartRecording()
        {
            if (_isRecording) return;

            _sampleBuffer.Clear();
            _currentSession.Frames.Clear();
            
            // Set up temp WAV file path
            _tempWavPath = Path.Combine(Path.GetTempPath(), $"voice_test_{Guid.NewGuid()}.wav");
            
            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(44100, 16, 1), // 44.1kHz, 16-bit, Mono
                    BufferMilliseconds = 40 // ~40ms buffer raises events quickly for low latency UI
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _waveWriter = new WaveFileWriter(_tempWavPath, _waveIn.WaveFormat);
                
                _waveIn.StartRecording();
                _isRecording = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting audio recording: {ex.Message}");
                CleanupRecording();
                throw;
            }
        }

        public void StopRecording()
        {
            if (!_isRecording || _waveIn == null) return;

            try
            {
                _waveIn.StopRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping audio recording: {ex.Message}");
                CleanupRecording();
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_waveWriter == null || e.BytesRecorded == 0) return;

            // 1. Write raw bytes to WAV file for playback
            _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);

            // 2. Convert 16-bit PCM bytes to float samples (-1.0 to 1.0)
            int sampleCount = e.BytesRecorded / 2;
            float[] newSamples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)((e.Buffer[i * 2 + 1] << 8) | e.Buffer[i * 2]);
                newSamples[i] = sample / 32768.0f;
            }

            // Raise raw samples event for the live waveform visualizer
            RawSamplesCaptured?.Invoke(newSamples);

            // Append to sliding buffer for DSP analysis
            _sampleBuffer.AddRange(newSamples);

            // 3. Process buffer in 2048-sample frames with 50% overlap (1024 sample slide)
            int frameSize = 2048;
            int hopSize = 1024;

            while (_sampleBuffer.Count >= frameSize)
            {
                float[] frame = new float[frameSize];
                _sampleBuffer.CopyTo(0, frame, 0, frameSize);

                // Run DSP processing on the frame
                FrameMetrics metrics = DspProcessor.ProcessFrame(frame, 44100);

                // Accumulate in current session
                _currentSession.AddFrame(metrics);

                // Trigger live UI callback
                LiveFrameProcessed?.Invoke(metrics);

                // Remove hopSize samples from buffer to slide the window
                _sampleBuffer.RemoveRange(0, hopSize);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            CleanupRecording();

            // Calculate aggregated session results
            _currentSession.CalculateResults();
            
            // Notify subscribers
            RecordingFinished?.Invoke(_currentSession);
        }

        private void CleanupRecording()
        {
            _isRecording = false;

            if (_waveWriter != null)
            {
                try
                {
                    _waveWriter.Flush();
                    _waveWriter.Dispose();
                }
                catch { }
                _waveWriter = null;
            }

            if (_waveIn != null)
            {
                try
                {
                    _waveIn.DataAvailable -= OnDataAvailable;
                    _waveIn.RecordingStopped -= OnRecordingStopped;
                    _waveIn.Dispose();
                }
                catch { }
                _waveIn = null;
            }
        }

        public void PlaybackRecording(string wavPath, Action<float[]>? waveCapturedCallback, Action? playbackFinishedCallback)
        {
            if (!File.Exists(wavPath)) return;

            try
            {
                var waveReader = new AudioFileReader(wavPath);
                var outputDevice = new WaveOutEvent();
                outputDevice.Init(waveReader);

                // Setup timer or event to push samples for visualization during playback
                var sampleProvider = waveReader.ToSampleProvider();
                
                // Let's wrap standard playback but we want simple playback for history.
                // A simple WaveOut playback is enough:
                outputDevice.PlaybackStopped += (s, e) =>
                {
                    outputDevice.Dispose();
                    waveReader.Dispose();
                    playbackFinishedCallback?.Invoke();
                };

                outputDevice.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing back WAV: {ex.Message}");
            }
        }

        public void Dispose()
        {
            CleanupRecording();
        }
    }
}
