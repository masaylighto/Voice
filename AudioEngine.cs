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
        private readonly VoiceAnalysisSession _currentSession = new VoiceAnalysisSession();
        private bool _isRecording;
        private WaveOutEvent? _activeOutputDevice;
        private AudioFileReader? _activeWaveReader;

        // Callbacks
        public event Action<float>? VolumeCaptured;
        public event Action<VoiceAnalysisSession>? RecordingFinished;

        public bool IsRecording => _isRecording;
        public string? TempWavPath => _tempWavPath;

        public void StartRecording()
        {
            if (_isRecording) return;

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

            // Calculate RMS volume of the current capture buffer
            float sum = 0f;
            foreach (var s in newSamples)
            {
                sum += s * s;
            }
            float rms = (float)Math.Sqrt(sum / newSamples.Length);

            // Trigger live UI callback with the RMS volume (for the waveform visualizer)
            VolumeCaptured?.Invoke(rms);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            CleanupRecording();

            // Calculate aggregated session results (passing WAV path for Praat analysis)
            _currentSession.CalculateResults(_tempWavPath);
            
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
                // Stop any current playback first
                StopPlayback();

                _activeWaveReader = new AudioFileReader(wavPath);
                _activeOutputDevice = new WaveOutEvent();

                var readerRef = _activeWaveReader;
                var deviceRef = _activeOutputDevice;

                deviceRef.Init(readerRef);

                deviceRef.PlaybackStopped += (s, e) =>
                {
                    deviceRef.Dispose();
                    readerRef.Dispose();

                    if (_activeOutputDevice == deviceRef) _activeOutputDevice = null;
                    if (_activeWaveReader == readerRef) _activeWaveReader = null;

                    playbackFinishedCallback?.Invoke();
                };

                deviceRef.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing back WAV: {ex.Message}");
            }
        }

        public void StopPlayback()
        {
            try
            {
                if (_activeOutputDevice != null)
                {
                    _activeOutputDevice.Stop();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping playback: {ex.Message}");
            }
        }

        public void Dispose()
        {
            CleanupRecording();
        }
    }
}
