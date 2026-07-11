# analyze_voice.praat
# Phonetic voice analysis script for Voice Lab (Feminization Training)
# Usage: praat.exe --run analyze_voice.praat "input_file.wav"

form Voice Analysis
    sentence input_file ""
endform

# Validate input
if not fileReadable (input_file$)
    appendInfoLine: "Error: File not readable: ", input_file$
    exit
endif

# Load sound
sound = Read from file: input_file$
total_duration = Get total duration

# 1. Pitch Analysis
pitch = To Pitch: 0.0, 50, 500
average_pitch = Get mean: 0, 0, "Hertz"
median_pitch = Get quantile: 0, 0, 0.50, "Hertz"
p10_pitch = Get quantile: 0, 0, 0.10, "Hertz"
p90_pitch = Get quantile: 0, 0, 0.90, "Hertz"

# Calculate standard deviation in semitones
selectObject: pitch
stddev_pitch_semitones = Get standard deviation: 0, 0, "semitones"

# Create PointProcess for Jitter/Shimmer
selectObject: sound
plusObject: pitch
pointProcess = To PointProcess (cc)

# Clean up pitch check
if average_pitch = undefined
    average_pitch = 0
endif
if median_pitch = undefined
    median_pitch = 0
endif
if p10_pitch = undefined
    p10_pitch = 0
endif
if p90_pitch = undefined
    p90_pitch = 0
endif
if stddev_pitch_semitones = undefined
    stddev_pitch_semitones = 0
endif

# 2. Formant Analysis (F1-F4)
selectObject: sound
formant = To Formant (burg): 0.0, 5.0, 5500.0, 0.025, 50.0
f1_mean = Get mean: 1, 0, 0, "Hertz"
f2_mean = Get mean: 2, 0, 0, "Hertz"
f3_mean = Get mean: 3, 0, 0, "Hertz"
f4_mean = Get mean: 4, 0, 0, "Hertz"

# Handle undefined formants
if f1_mean = undefined
    f1_mean = 0
endif
if f2_mean = undefined
    f2_mean = 0
endif
if f3_mean = undefined
    f3_mean = 0
endif
if f4_mean = undefined
    f4_mean = 0
endif

f2_f1_ratio_mean = 0
if f1_mean > 0
    f2_f1_ratio_mean = f2_mean / f1_mean
endif

formant_dispersion = 0
if f4_mean > 0 and f1_mean > 0
    formant_dispersion = (f4_mean - f1_mean) / 3.0
endif

# 3. Resonance (Spectral Centroid)
selectObject: sound
To Spectrum: "yes"
spectrum = selected("Spectrum")
selectObject: spectrum
centroid_mean = Get centre of gravity: 2.0
Remove

if centroid_mean = undefined
    centroid_mean = 0
endif

# 4. Vocal Weight (Spectral Tilt)
selectObject: sound
Filter (pass Hann band): 80, 250, 50
sound_low = selected("Sound")
intensity_low = To Intensity: 100, 0.0, "yes"

selectObject: sound
Filter (pass Hann band): 250, 3000, 100
sound_high = selected("Sound")
intensity_high = To Intensity: 100, 0.0, "yes"

# Calculate average vocal weight in decibels
selectObject: intensity_low
int_low_mean = Get mean: 0, 0, "energy"
selectObject: intensity_high
int_high_mean = Get mean: 0, 0, "energy"

weight_db_mean = 0
if int_low_mean != undefined and int_high_mean != undefined and int_low_mean > 0 and int_high_mean > 0
    # Convert average energy to decibels and apply 12.09 dB bandwidth normalization offset
    db_low = 10 * log10(int_low_mean)
    db_high = 10 * log10(int_high_mean)
    weight_db_mean = db_low - db_high + 12.09
endif

# 5. Silence & Speech Pacing
selectObject: sound
intensity = To Intensity: 100, 0.0, "yes"
min_int = Get minimum: 0, 0, "Parabolic"
max_int = Get maximum: 0, 0, "Parabolic"

silence_threshold = -25.0

textgrid = To TextGrid (silences): silence_threshold, 0.1, 0.1, "silent", "sounding"

selectObject: textgrid
num_intervals = Get number of intervals: 1
silent_duration = 0
sounding_duration = 0

for i from 1 to num_intervals
    label$ = Get label of interval: 1, i
    t1 = Get start time of interval: 1, i
    t2 = Get end time of interval: 1, i
    dur = t2 - t1
    if label$ == "silent"
        silent_duration = silent_duration + dur
    else
        sounding_duration = sounding_duration + dur
    endif
endfor

pause_ratio = 0
if total_duration > 0
    pause_ratio = silent_duration / total_duration
endif

# Articulation peak counter (robust syllable nuclei detection)
selectObject: sound
Filter (pass Hann band): 300, 3000, 100
sound_filt = selected("Sound")
intensity_filt = To Intensity: 50, 0.01, "yes"

selectObject: intensity_filt
num_frames = Get number of frames
syllables_count = 0
last_peak_time = -1.0
min_peak_distance = 0.15 ; # Min 150ms between vowels (~6.6 syllables/sec max)

# Dynamic threshold based on maximum intensity
max_intensity = Get maximum: 0, 0, "Parabolic"
threshold = max_intensity - 18.0
if threshold < 40.0
    threshold = 40.0
endif

for f from 3 to num_frames - 2
    t = Get time from frame number: f
    val = Get value in frame: f
    val_prev = Get value in frame: f - 1
    val_prev2 = Get value in frame: f - 2
    val_next = Get value in frame: f + 1
    val_next2 = Get value in frame: f + 2
    
    if val != undefined and val_prev != undefined and val_next != undefined
        if val > val_prev and val > val_next and val > val_prev2 and val > val_next2
            if val > threshold
                # Check minimum separation between syllable peaks
                if t - last_peak_time > min_peak_distance
                    # Validate that it has corresponding voicing (pitch > 0)
                    selectObject: pitch
                    p_val = Get value at time: t, "Hertz", "Linear"
                    selectObject: intensity_filt
                    
                    if p_val != undefined and p_val > 0
                        syllables_count = syllables_count + 1
                        last_peak_time = t
                    endif
                endif
            endif
        endif
    endif
endfor

# Clean up temp objects
selectObject: intensity_filt
Remove
selectObject: sound_filt
Remove

articulation_rate = 0
speaking_rate = 0
if sounding_duration > 0.3
    articulation_rate = syllables_count / sounding_duration
endif
if total_duration > 0.3
    speaking_rate = syllables_count / total_duration
endif

# 6. Vocal Stability: Jitter & Shimmer (Overall Averages)
selectObject: pointProcess
jitter_overall = Get jitter (local): 0, 0, 0.0001, 0.02, 1.3
if jitter_overall = undefined
    jitter_overall = 0
endif
jitter_overall = jitter_overall * 100.0

selectObject: sound
plusObject: pointProcess
shimmer_overall = Get shimmer (local): 0, 0, 0.0001, 0.02, 1.3, 1.6
if shimmer_overall = undefined
    shimmer_overall = 0
endif
shimmer_overall = shimmer_overall * 100.0

# 7. Ending Pitch Sweep Direction
selectObject: pitch
num_pitch_frames = Get number of frames
last_valid_time = 0
f = num_pitch_frames
while f >= 1
    p_val = Get value in frame: f, "Hertz"
    if p_val != undefined
        last_valid_time = Get time from frame: f
        goto FOUND_LAST
    endif
    f = f - 1
endwhile

label FOUND_LAST

ending_direction$ = "Not enough data"
if last_valid_time > 0.5
    t_start = last_valid_time - 0.50
    t_end = last_valid_time
    p_start = Get value at time: t_start, "Hertz", "Linear"
    p_end = Get value at time: t_end, "Hertz", "Linear"
    
    if p_start != undefined and p_end != undefined
        st_start = 12 * log2(p_start / 55.0)
        st_end = 12 * log2(p_end / 55.0)
        diff = st_end - st_start
        if diff >= 1.0
            ending_direction$ = "Rising"
        elif diff <= -1.0
            ending_direction$ = "Falling"
        else
            ending_direction$ = "Level"
        endif
    endif
endif

# Print summary metrics to stdout
writeInfoLine: "AveragePitchHz: ", average_pitch
appendInfoLine: "MedianPitchHz: ", median_pitch
appendInfoLine: "PitchP10Hz: ", p10_pitch
appendInfoLine: "PitchP90Hz: ", p90_pitch
appendInfoLine: "PitchStdDevSemitones: ", stddev_pitch_semitones
appendInfoLine: "AverageF1Hz: ", f1_mean
appendInfoLine: "AverageF2Hz: ", f2_mean
appendInfoLine: "AverageF3Hz: ", f3_mean
appendInfoLine: "AverageF4Hz: ", f4_mean
appendInfoLine: "F2F1Ratio: ", f2_f1_ratio_mean
appendInfoLine: "FormantDispersion: ", formant_dispersion
appendInfoLine: "AverageResonanceHz: ", centroid_mean
appendInfoLine: "AverageWeightDb: ", weight_db_mean
appendInfoLine: "PauseRatio: ", pause_ratio
appendInfoLine: "ArticulationRate: ", articulation_rate
appendInfoLine: "SpeakingRate: ", speaking_rate
appendInfoLine: "EndingPitchDirection: ", ending_direction$
appendInfoLine: "JitterLocalPct: ", jitter_overall
appendInfoLine: "ShimmerLocalPct: ", shimmer_overall

# Print time-series header
appendInfoLine: "---TIME_SERIES---"
appendInfoLine: "Time(s),Pitch(Hz),F1(Hz),F2(Hz),F3(Hz),F4(Hz),F2F1Ratio,Resonance(Hz),VocalWeight(dB),Intensity(dB),Jitter(%),Shimmer(%)"

time_step = 0.02
t = time_step
while t <= total_duration
    # Pitch
    selectObject: pitch
    p_val = Get value at time: t, "Hertz", "Linear"
    if p_val = undefined
        p_val = 0
    endif
    
    # Formants
    selectObject: formant
    f1_val = Get value at time: 1, t, "Hertz", "Linear"
    f2_val = Get value at time: 2, t, "Hertz", "Linear"
    f3_val = Get value at time: 3, t, "Hertz", "Linear"
    f4_val = Get value at time: 4, t, "Hertz", "Linear"
    
    if f1_val = undefined
        f1_val = 0
    endif
    if f2_val = undefined
        f2_val = 0
    endif
    if f3_val = undefined
        f3_val = 0
    endif
    if f4_val = undefined
        f4_val = 0
    endif
    
    # Formant ratio
    ratio_val = 0
    if f1_val > 0
        ratio_val = f2_val / f1_val
    endif
    
    # Vocal Weight (Spectral Tilt)
    selectObject: intensity_low
    int_low = Get value at time: t, "Cubic"
    selectObject: intensity_high
    int_high = Get value at time: t, "Cubic"
    
    weight_val = 0
    if int_low != undefined and int_high != undefined
        weight_val = int_low - int_high + 12.09
    endif
    
    # Intensity
    selectObject: intensity
    intensity_val = Get value at time: t, "Cubic"
    if intensity_val = undefined
        intensity_val = 0
    endif
    
    # Local Resonance / Spectral Centroid (extracted via a 50ms sound slice)
    selectObject: sound
    t_start_slice = t - 0.025
    t_end_slice = t + 0.025
    if t_start_slice < 0
        t_start_slice = 0
    endif
    if t_end_slice > total_duration
        t_end_slice = total_duration
    endif
    
    sound_slice = Extract part: t_start_slice, t_end_slice, "rectangular", 1, "no"
    spectrum_slice = To Spectrum: "yes"
    centroid_val = Get centre of gravity: 2.0
    
    if centroid_val = undefined
        centroid_val = 0
    endif
    
    selectObject: sound_slice
    plusObject: spectrum_slice
    Remove
    
    # Stability (Jitter and Shimmer in a 200 ms neighborhood)
    t_min = t - 0.10
    t_max = t + 0.10
    if t_min < 0
        t_min = 0
    endif
    if t_max > total_duration
        t_max = total_duration
    endif
    
    selectObject: pointProcess
    jit_val = Get jitter (local): t_min, t_max, 0.0001, 0.02, 1.3
    if jit_val = undefined
        jit_val = 0
    endif
    jit_val = jit_val * 100.0
    
    selectObject: sound
    plusObject: pointProcess
    shim_val = Get shimmer (local): t_min, t_max, 0.0001, 0.02, 1.3, 1.6
    if shim_val = undefined
        shim_val = 0
    endif
    shim_val = shim_val * 100.0
    
    # Print row
    appendInfoLine: fixed$ (t, 2), ",", fixed$ (p_val, 1), ",", fixed$ (f1_val, 1), ",", fixed$ (f2_val, 1), ",", fixed$ (f3_val, 1), ",", fixed$ (f4_val, 1), ",", fixed$ (ratio_val, 3), ",", fixed$ (centroid_val, 1), ",", fixed$ (weight_val, 2), ",", fixed$ (intensity_val, 1), ",", fixed$ (jit_val, 3), ",", fixed$ (shim_val, 3)
    t = t + time_step
endwhile

# Clean up Praat objects
selectObject: sound
plusObject: pitch
plusObject: pointProcess
plusObject: formant
plusObject: sound_low
plusObject: intensity_low
plusObject: sound_high
plusObject: intensity_high
plusObject: intensity
plusObject: textgrid
Remove
