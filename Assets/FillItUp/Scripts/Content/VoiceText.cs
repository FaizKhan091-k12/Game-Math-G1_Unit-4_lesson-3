using System.Collections.Generic;

namespace FillItUp
{
    /// <summary>Subtitle text for each of Splashy's voice lines (same wording as the ElevenLabs recordings).</summary>
    public static class VoiceText
    {
        static readonly Dictionary<string, string> Lines = new Dictionary<string, string>
        {
            { "welcome", "Hi! I'm Splashy! Let's find out which one holds more water!" },
            { "pick_level", "Pick a level to play!" },
            { "l1_intro", "Level one! Look carefully. Which one holds more? Which one holds less?" },
            { "l2_intro", "Level two! Now there are three. Find the one that holds the most, or the least!" },
            { "l3_intro", "Level three! Magic water! I will pour the water. Will it fit, or will it spill?" },
            { "q_more", "Which holds more?" },
            { "q_less", "Which holds less?" },
            { "q_most", "Which holds the most?" },
            { "q_least", "Which holds the least?" },
            { "q_pour", "Will the water fit, or will it spill over?" },
            { "right_1", "Yes! You got it!" },
            { "right_2", "Amazing!" },
            { "right_3", "Splash-tastic!" },
            { "right_4", "Super job!" },
            { "right_5", "Wow! You're so smart!" },
            { "right_6", "That's right!" },
            { "wrong_1", "Oops! Try again!" },
            { "wrong_2", "Hmm... look again!" },
            { "wrong_3", "Not quite! You can do it!" },
            { "reveal", "Look! It's this one!" },
            { "why_more", "It is bigger, so it holds more water!" },
            { "why_less", "It is smaller, so it holds less water!" },
            { "why_most", "This one is the biggest. It holds the most!" },
            { "why_least", "This one is the smallest. It holds the least!" },
            { "fits", "It all fits! The water stays inside." },
            { "spills", "Whoa! It spilled over! Too much water!" },
            { "streak_3", "Three in a row! Super splash!" },
            { "streak_5", "Five in a row! You're on fire!" },
            { "level_done", "Level complete! Look at your stars!" },
            { "perfect", "Perfect! You got them all!" },
            { "good_try", "Good try! Let's play again and get more stars!" },
            { "unlocked", "Yay! A new level is open!" },
            { "champion", "You did it! You are a Capacity Champion!" },
            { "progress", "Look how much you have learned!" },
            { "tap_one", "Tap the one you think!" },
            { "sleepy", "Yawn... let's play!" },
            { "ready", "Ready? Let's go!" },
        };

        public static string Get(string key) => Lines.TryGetValue(key, out var t) ? t : null;
    }
}
