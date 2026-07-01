# Voice Lab 🎙️

Voice Lab is a native Windows WPF desktop application designed for real-time vocal analysis, diagnostics, and target training. 

> [!IMPORTANT]
> **AI Development Notice**
> This entire codebase, including user interface styling, audio engineering, and digital signal processing, was created, compiled, and verified **entirely by AI (Antigravity)**. The code has **never been manually written, read, or modified by a human developer**. 

---

## Core Features

1. **Voice Capture & Live Feedback**:
   - Captures microphone input at 44.1 kHz, 16-bit Mono.
   - Renders a real-time visualizer canvas featuring background grid lines and a SoundCloud-style filled gradient waveform.
   - Displays live fundamental frequency (F0) estimations in real time.

2. **Reading Prompt Assistant**:
   - Hosts a scrollable database of vocal prompts categorized into Speech Pathology classics (e.g., *The Rainbow Passage*, *The Grandfather Passage*), phonetically balanced *Harvard Sentences*, and targeted voice exercises.

3. **Vocal Diagnostics Console**:
   - Parses the recorded audio sample and rates the voice on a unified scale from `0` (Masculine) to `50` (Androgynous) to `100` (Feminine) across 5 metrics:
     - **Pitch**: The frequency of vocal fold vibration.
     - **Resonance**: Sound placement (dark/chest resonance vs bright/nasal head resonance).
     - **Weight**: Vocal thickness (chesty/buzzy thickness vs light/breathy thinness).
     - **Intonation**: Melody and expression (flat monotone vs sweeping melodic variation).
     - **Speech Patterns**: Phrase pacing, breathing pauses, and end-of-sentence pitch curves (e.g. rising inflections).
   - Displays custom monospace grid block progress bars (e.g. `█ █ █ █ █ █ █ █ ░ ░`) alongside scientific scale spectrum sliders.
   - Provides concrete, science-based training exercises tailored to each metric.

4. **Vocal Trainer (Live Practice)**:
   - Select a vocal property (Pitch or Resonance) and a target range (Masculine, Androgynous, or Feminine).
   - Practice vocalizing against a green target band. The app plots a live scrolling line of your voice, giving immediate visual feedback and coaching tips.

5. **History Log & Persistence**:
   - Saves vocal sessions locally in `%APPDATA%/VoiceTester/history.json`.
   - Copies audio recordings permanently to the user folder, allowing you to load past reports or replay recordings to track progress.

---

## Signal Processing & Metrics Breakdown

| Metric | Scientific Basis | Target Ranges |
| :--- | :--- | :--- |
| **Pitch (F0)** | Time-domain **Autocorrelation** with Sondhi center-clipping (to filter out formants) and parabolic peak interpolation. | **Masculine**: 85 - 155 Hz<br>**Androgynous**: 155 - 185 Hz<br>**Female**: 185 - 255+ Hz |
| **Resonance** | **Spectral Centroid** calculated from positive FFT magnitude coefficients within the speech vocal tract band (300 Hz - 3000 Hz). | **Masculine (Dark)**: Lower frequencies<br>**Androgynous**: Moderate frequencies<br>**Feminine (Bright)**: Higher frequencies |
| **Vocal Weight** | **Spectral Tilt** calculated as the decibel energy ratio of the low fundamental band (80 - 250 Hz) vs the mid-high harmonic band (250 - 3000 Hz). | **Masculine (Heavy)**: Strong lower harmonics (+8 dB)<br>**Androgynous**: Balanced harmonics (0 dB)<br>**Feminine (Light)**: High-frequency tilt / breathy (-8 dB) |
| **Intonation** | Pitch contour variance converted to perception-aligned **Semitones** ($S = 12 \log_2(F_0 / 55)$). | **Masculine**: Monotonic (SD < 1.0 semitones)<br>**Androgynous**: Balanced<br>**Feminine**: Melodic/Sweeping (SD > 3.0 semitones) |
| **Speech Patterns** | Pause ratio (energy gate), articulation rate (short-time RMS envelope peak tracking), and ending pitch contour regression slope. | **Masculine**: Direct, staccato, falling inflections<br>**Androgynous**: Steady rhythm<br>**Feminine**: Breathy phrasing, rising sweeps |

---

## How to Build and Run

### Prerequisites
- **.NET SDK 10.0** (or compatible newer version) installed.
- Windows Operating System (required for WPF native controls).

### Run Diagnostic Validation
We built an automated test runner into the entry point to verify DSP accuracy on synthetic waves. Run:
```powershell
dotnet run -- --test-dsp-headless
```
This processes mathematical sine waves and writes verification results to `dsp_test_results.log`. (All diagnostics should output `PASSED`).

### Build & Run the GUI Application
1. **Build**:
   ```powershell
   dotnet build -c Release
   ```
2. **Run**:
   ```powershell
   dotnet run
   ```
   *(Or launch the executable directly from `bin/Release/net10.0-windows/Voice.exe`)*
