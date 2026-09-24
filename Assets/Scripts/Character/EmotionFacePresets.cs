using System.Collections.Generic;
using System.Linq;
using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.Character
{
    /// <summary>Érzelmenkénti és reakciónkénti arckifejezések ARKit-blendshape-ekből (súly 0..1).</summary>
    public static class EmotionFacePresets
    {
        public const string BrowInnerUp = "A01_Brow_Inner_Up";
        public const string BlinkLeft = "A14_Eye_Blink_Left";
        public const string BlinkRight = "A15_Eye_Blink_Right";
        public const string MouthPressLeft = "A48_Mouth_Press_Left";
        public const string MouthPressRight = "A49_Mouth_Press_Right";
        public const string LipBite = "Mouth_Bottom_Lip_Bite";
        public const string SquintLeft = "A16_Eye_Squint_Left";
        public const string SquintRight = "A17_Eye_Squint_Right";
        public const string LowerLipDownLeft = "A46_Mouth_Lower_Down_Left";
        public const string LowerLipDownRight = "A47_Mouth_Lower_Down_Right";

        private static readonly Dictionary<SuspectEmotion, (string shape, float weight)[]> Presets =
            new Dictionary<SuspectEmotion, (string, float)[]>
            {
                [SuspectEmotion.Calm] = new[]
                {
                    (SquintLeft, 0.1f), (SquintRight, 0.1f),
                    (BlinkLeft, 0.15f), (BlinkRight, 0.15f), // kissé leengedett felső szemhéj: nyugodt, nem „meglepett"
                    ("A38_Mouth_Smile_Left", 0.04f), ("A39_Mouth_Smile_Right", 0.04f)
                },
                [SuspectEmotion.Nervous] = new[]
                {
                    (BrowInnerUp, 0.5f), ("A18_Eye_Wide_Left", 0.2f), ("A19_Eye_Wide_Right", 0.2f),
                    (MouthPressLeft, 0.35f), (MouthPressRight, 0.35f),
                    ("A40_Mouth_Frown_Left", 0.15f), ("A41_Mouth_Frown_Right", 0.15f),
                    ("A34_Mouth_Roll_Lower", 0.15f)
                },
                [SuspectEmotion.Defensive] = new[]
                {
                    ("A02_Brow_Down_Left", 0.55f), ("A03_Brow_Down_Right", 0.55f),
                    (SquintLeft, 0.4f), (SquintRight, 0.4f),
                    (MouthPressLeft, 0.6f), (MouthPressRight, 0.6f),
                    ("A36_Mouth_Shrug_Lower", 0.45f)
                },
                [SuspectEmotion.Hostile] = new[]
                {
                    ("A02_Brow_Down_Left", 1f), ("A03_Brow_Down_Right", 1f),
                    ("A23_Nose_Sneer_Left", 0.65f), ("A24_Nose_Sneer_Right", 0.65f),
                    ("A44_Mouth_Upper_Up_Left", 0.45f), ("A45_Mouth_Upper_Up_Right", 0.45f),
                    (SquintLeft, 0.55f), (SquintRight, 0.55f),
                    ("A50_Mouth_Stretch_Left", 0.2f), ("A51_Mouth_Stretch_Right", 0.2f),
                    ("A26_Jaw_Forward", 0.2f)
                },
                [SuspectEmotion.Breaking] = new[]
                {
                    (BrowInnerUp, 0.8f),
                    ("A40_Mouth_Frown_Left", 0.6f), ("A41_Mouth_Frown_Right", 0.6f),
                    (SquintLeft, 0.25f), (SquintRight, 0.25f),
                    (BlinkLeft, 0.3f), (BlinkRight, 0.3f),
                    ("A34_Mouth_Roll_Lower", 0.2f),
                    (LowerLipDownLeft, 0.15f), (LowerLipDownRight, 0.15f)
                }
            };

        // Reakció alatt ezek váltják az érzelem-presetet; az állkapcsot a ReactionJaw nyitja.
        private static readonly Dictionary<SuspectReaction, (string shape, float weight)[]> ReactionPresets =
            new Dictionary<SuspectReaction, (string, float)[]>
            {
                [SuspectReaction.Caught] = new[]
                {
                    (BrowInnerUp, 1f), ("A04_Brow_Outer_Up_Left", 0.75f), ("A05_Brow_Outer_Up_Right", 0.75f),
                    ("A18_Eye_Wide_Left", 0.8f), ("A19_Eye_Wide_Right", 0.8f),
                    (LowerLipDownLeft, 0.35f), (LowerLipDownRight, 0.35f),
                    ("A50_Mouth_Stretch_Left", 0.3f), ("A51_Mouth_Stretch_Right", 0.3f)
                },
                [SuspectReaction.Outburst] = new[]
                {
                    ("A02_Brow_Down_Left", 1f), ("A03_Brow_Down_Right", 1f),
                    ("A23_Nose_Sneer_Left", 0.85f), ("A24_Nose_Sneer_Right", 0.85f),
                    ("A44_Mouth_Upper_Up_Left", 0.7f), ("A45_Mouth_Upper_Up_Right", 0.7f),
                    (SquintLeft, 0.6f), (SquintRight, 0.6f),
                    ("A50_Mouth_Stretch_Left", 0.45f), ("A51_Mouth_Stretch_Right", 0.45f),
                    (LowerLipDownLeft, 0.4f), (LowerLipDownRight, 0.4f)
                },
                [SuspectReaction.SmugLaugh] = new[]
                {
                    ("A38_Mouth_Smile_Left", 1f), ("A39_Mouth_Smile_Right", 1f),
                    ("A21_Cheek_Squint_Left", 0.85f), ("A22_Cheek_Squint_Right", 0.85f),
                    (SquintLeft, 0.6f), (SquintRight, 0.6f),
                    ("A42_Mouth_Dimple_Left", 0.45f), ("A43_Mouth_Dimple_Right", 0.45f),
                    ("A04_Brow_Outer_Up_Left", 0.3f), ("A05_Brow_Outer_Up_Right", 0.3f),
                    ("A44_Mouth_Upper_Up_Left", 0.3f), ("A45_Mouth_Upper_Up_Right", 0.3f)
                }
            };

        // (alapnyitás, lüktetés amplitúdója, Hz)
        private static readonly Dictionary<SuspectReaction, (float open, float pulse, float hz)> ReactionJawData =
            new Dictionary<SuspectReaction, (float, float, float)>
            {
                [SuspectReaction.Caught] = (0.4f, 0f, 0f),
                [SuspectReaction.Outburst] = (0.45f, 0.2f, 2.2f),
                [SuspectReaction.SmugLaugh] = (0.35f, 0.3f, 4.8f)
            };

        public static IReadOnlyList<(string shape, float weight)> For(SuspectEmotion emotion) =>
            Presets.TryGetValue(emotion, out var preset) ? preset : Presets[SuspectEmotion.Calm];

        public static IReadOnlyList<(string shape, float weight)> For(SuspectReaction reaction) => ReactionPresets[reaction];

        public static float ReactionJaw(SuspectReaction reaction, float seconds)
        {
            var (open, pulse, hz) = ReactionJawData[reaction];
            if (pulse <= 0f) return open;
            // Egyenirányított szinusz: rövid nyitások és zárások („ha-ha").
            return Mathf.Clamp01(open - pulse * 0.5f + pulse * Mathf.Abs(Mathf.Sin(seconds * hz * Mathf.PI)));
        }

        public static IEnumerable<string> AllShapes()
        {
            var seen = new HashSet<string>();
            foreach (var preset in Presets.Values.Concat(ReactionPresets.Values))
                foreach (var (shape, _) in preset)
                    if (seen.Add(shape)) yield return shape;
        }
    }
}
