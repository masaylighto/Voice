using System;
using System.Collections.Generic;

namespace Voice
{
    public class ReadingPrompt
    {
        public string Text { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public static class ReadingPrompts
    {
        public static Dictionary<string, List<ReadingPrompt>> Categories { get; } = new Dictionary<string, List<ReadingPrompt>>();

        static ReadingPrompts()
        {
            // 1. Phonetically Balanced Texts (Speech Science Classics)
            Categories["Standard Passages"] = new List<ReadingPrompt>
            {
                new ReadingPrompt
                {
                    Text = "When the sunlight strikes raindrops in the air, they act as a prism and form a rainbow. The rainbow is a division of white light into many beautiful colors. These take the shape of a long round arch, with its path high above, and its two ends apparently beyond the horizon.",
                    Note = "The Rainbow Passage (Part 1): Standard speech science passage containing all phonemes."
                },
                new ReadingPrompt
                {
                    Text = "There is, according to legend, a boiling pot of gold at one end. People look, but no one ever finds it. When a man looks for something beyond his reach, his friends say he is looking for the pot of gold at the end of the rainbow.",
                    Note = "The Rainbow Passage (Part 2): Ideal for natural speech flow and inflection analysis."
                },
                new ReadingPrompt
                {
                    Text = "You wished to know all about my grandfather. Well, he is nearly ninety-three years old; he plays grandfather clock games, and still thinks as swiftly as ever. He dresses in an ancient black frock coat, usually minorly stained with grease.",
                    Note = "The Grandfather Passage: Another classic speech evaluation passage."
                }
            };

            // 2. Harvard Sentences (Phonetically Balanced testing sentences)
            Categories["Harvard Sentences"] = new List<ReadingPrompt>
            {
                new ReadingPrompt { Text = "The birch canoe slid on the smooth planks.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "Glue the sheet to the dark blue background.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "It's easy to tell the depth of a well.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "These days a leg of chicken is a rare dish.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "Rice is often served in round bowls.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The juice of lemons makes fine punch.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The box was thrown beside the park gate.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The ship was torn on the sharp reef.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "A Joyful shout welcomed the new king.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "A saw is a useful tool in making boards.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The source of the huge river is in the cold hills.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "Kick the ball over the fence and run.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The hog oriented itself by sniffing the damp dirt.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "Hear the quiet chirp of the birds in the dark trees.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "Add the sum and write the total on the sheet.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "A salty breeze blew off the open ocean wave.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The houses are built of yellow brick and mortar.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "A Dash of salt makes the soup taste better.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The clock struck the hour of midnight with a chime.", Note = "Phonetic Balance" },
                new ReadingPrompt { Text = "The cold air chilled us to the bone.", Note = "Phonetic Balance" }
            };

            // 3. Resonance Training Prompts (M/N/NG Focus for forward placement, O/U for throat)
            Categories["Resonance Exercises"] = new List<ReadingPrompt>
            {
                new ReadingPrompt
                {
                    Text = "Many mumble muffins Monday morning. Norman named nine new neighbors. My mother makes massive melon milkshakes.",
                    Note = "Forward Placement: Focus on feeling vibrations on the lips and nose (nasal resonance)."
                },
                new ReadingPrompt
                {
                    Text = "Singing songs along the winding ring. Bringing bells to hang among the ceiling rings.",
                    Note = "Nasality Focus: Emphasize the '-ng' sounds to open the soft palate and shift resonance forward."
                },
                new ReadingPrompt
                {
                    Text = "Who threw the blue balloon through the cool room? Only old oak boats float on the open ocean.",
                    Note = "Vocal Tract Shape: Open back vowels. Good for practicing deep, warm throat/chest resonance."
                },
                new ReadingPrompt
                {
                    Text = "Meet me at the green street tree. Keep the neat keys near the tea cup.",
                    Note = "Bright Formants: High front vowels. Practice keeping the larynx high and tone bright."
                }
            };

            // 4. Vocal Weight & Intensity Prompts (Breathy vs. Pressed onset)
            Categories["Vocal Weight Exercises"] = new List<ReadingPrompt>
            {
                new ReadingPrompt
                {
                    Text = "Harry holds his heavy hat. Hello, how has Helen been? Who has heard of Hugh's home?",
                    Note = "Airy / Light Weight: Emphasize the soft breathy 'H' sounds. Good for thin vocal weight practice."
                },
                new ReadingPrompt
                {
                    Text = "Eight apples ate Andy. Ask Abby about actual answers. Open the orange box immediately.",
                    Note = "Firm / Heavy Weight: Practice clean glottal onsets. Good for building chest projection and fold thickness."
                },
                new ReadingPrompt
                {
                    Text = "Softly, the summer wind sighs through the silver trees. Whispering breezes bring quiet dreams.",
                    Note = "Soft/Thin Tone: Focus on a gentle, low-effort speech style to reduce vocal fold compression."
                }
            };

            // 5. Intonation & Rhythm Prompts (Melodic contours vs Monotonic statements)
            Categories["Intonation Exercises"] = new List<ReadingPrompt>
            {
                new ReadingPrompt
                {
                    Text = "Really? That is absolutely incredible! Are you coming to the party tonight? I can't wait to see you!",
                    Note = "Melodic Sweeps: Exaggerate the rising endings and wide pitch bounds. (Feminine-style intonation)."
                },
                new ReadingPrompt
                {
                    Text = "This is a fact. The project will succeed on time. We need to focus. Do not deviate from the plan.",
                    Note = "Monotonic statements: Deliver flat tones, dropping the pitch at the end of each sentence. (Masculine-style)."
                },
                new ReadingPrompt
                {
                    Text = "One, two, three, four, five. January, February, March, April, May.",
                    Note = "Steady cadence: Good for analyzing monotonic rhythm and pacing consistency."
                }
            };
        }
    }
}
