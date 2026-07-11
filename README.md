# Voice Lab

Voice Lab is a native Windows WPF application for recording speech, tracking fundamental frequency (F0), and reviewing voice measurements over time.

## Core Features

1. Voice capture and live feedback
   - Captures microphone input at 44.1 kHz, 16-bit mono.
   - Shows a live F0 contour with time on the horizontal axis and pitch in Hz on the vertical axis.
   - Preserves the complete pitch contour for the final report, including gaps for unvoiced audio.

2. Voice measurements
   - Reports median F0, trimmed mean F0, central 80% F0 range, tracking coverage, and recording duration.
   - Shows a full-recording pitch contour instead of a single peak.
   - Reports spectral brightness, spectral balance, pitch variation, and timing as separate descriptive measurements.
   - Uses an acoustic pitch-presentation reference based on F0 only; it does not infer gender identity.

3. Live trainer
   - Offers pitch and spectral-brightness practice with a target band and scrolling line.

4. History
   - Saves voice sessions in `%APPDATA%/VoiceTester/history.json`.
   - Stores the pitch contour for newly created history entries.

## Signal Processing

| Measurement | Method | Interpretation |
| :--- | :--- | :--- |
| Pitch (F0) | YIN cumulative mean normalized difference function with sub-sample interpolation, a 65-500 Hz search range, and confidence gating. | Pitch presentation reference: masculine-leaning below 130 Hz, androgynous 145-175 Hz, feminine-leaning above 180 Hz. F0 alone does not identify a person's gender. |
| Spectral Brightness | Spectral centroid from positive FFT magnitudes in the 300-3000 Hz speech band. | A relative brightness measure, not a direct formant or vocal-tract-size estimate. |
| Spectral Balance | dB ratio of average power in 80-250 Hz and 250-3000 Hz bands. | Best for comparing recordings made with the same microphone and distance; it is not a direct vocal-fold-closure measurement. |
| Pitch Variation | Standard deviation of tracked F0 in semitones: `12 * log2(F0 / 55)`. | Lower values are steadier; higher values show a wider pitch contour in the recording. |
| Speech Timing | Pause ratio, amplitude-envelope pacing estimate, and final voiced-pitch direction. | Describes the recording and is not used for pitch-presentation labeling. |

## Build and Run

Prerequisites:

- .NET SDK 10.0 or compatible newer version.
- Windows, required for WPF audio capture and UI.

Run the DSP validation:

```powershell
dotnet run -- --test-dsp-headless
```

The validation writes `dsp_test_results.log` and exits nonzero on a failed diagnostic.

Build the application:

```powershell
dotnet build -c Release
```

Run the application:

```powershell
dotnet run
```
