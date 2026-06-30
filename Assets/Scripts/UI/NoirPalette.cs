using Interrogation.Llm;
using UnityEngine;

namespace Interrogation.UI
{
    /// <summary>A noir UI színpalettája: mély feketék, meleg borostyán, hideg kékesszürke.</summary>
    public static class NoirPalette
    {
        // Alap felületek
        public static readonly Color Background = new Color32(0x0D, 0x0E, 0x11, 0xFF); // mély fekete
        public static readonly Color Panel = new Color32(0x16, 0x18, 0x1D, 0xE6);      // sötét, áttetsző panel
        public static readonly Color PanelSoft = new Color32(0x1E, 0x21, 0x27, 0xCC);

        // Szövegek
        public static readonly Color TextPrimary = new Color32(0xEC, 0xE6, 0xDA, 0xFF); // meleg törtfehér
        public static readonly Color TextMuted = new Color32(0x9A, 0x93, 0x88, 0xFF);

        // Akcentusok
        public static readonly Color Amber = new Color32(0xE0, 0xA8, 0x67, 0xFF);   // meleg kulcsfény
        public static readonly Color ColdBlue = new Color32(0x6E, 0x8A, 0x9E, 0xFF); // hideg árnyék

        // Akta (főmenü, szünet, lezárt akta): manilapapír és írógép-tinta
        public static readonly Color Manila = new Color32(0xC8, 0xB3, 0x86, 0xFF);
        public static readonly Color ManilaDark = new Color32(0xA8, 0x92, 0x66, 0xFF);
        public static readonly Color Paper = new Color32(0xE4, 0xDA, 0xC2, 0xFF);
        public static readonly Color Ink = new Color32(0x26, 0x21, 0x1C, 0xFF);
        public static readonly Color InkMuted = new Color32(0x5E, 0x54, 0x46, 0xFF);
        public static readonly Color InkRed = new Color32(0x9C, 0x2B, 0x22, 0xFF);

        // Beszélő-felületek (transzkript stílus)
        public static readonly Color DetectiveBg = new Color32(0x16, 0x20, 0x28, 0xE6); // hideg
        public static readonly Color SuspectBg = new Color32(0x22, 0x1C, 0x18, 0xE6);   // meleg

        // Stressz-mérő gradiens
        public static readonly Color StressLow = new Color32(0x6E, 0x8A, 0x9E, 0xFF);
        public static readonly Color StressMid = new Color32(0xE0, 0xA8, 0x67, 0xFF);
        public static readonly Color StressHigh = new Color32(0xC0, 0x4A, 0x3A, 0xFF);

        public static Color Emotion(SuspectEmotion emotion)
        {
            switch (emotion)
            {
                case SuspectEmotion.Calm: return new Color32(0x8F, 0xB0, 0xC0, 0xFF);
                case SuspectEmotion.Nervous: return new Color32(0xE0, 0xC0, 0x67, 0xFF);
                case SuspectEmotion.Defensive: return new Color32(0xC0, 0xA0, 0x60, 0xFF);
                case SuspectEmotion.Hostile: return new Color32(0xD0, 0x68, 0x50, 0xFF);
                case SuspectEmotion.Breaking: return new Color32(0xB0, 0x60, 0xA0, 0xFF);
                default: return TextMuted;
            }
        }

        public static Color Stress(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? Color.Lerp(StressLow, StressMid, t * 2f)
                : Color.Lerp(StressMid, StressHigh, (t - 0.5f) * 2f);
        }
    }
}
