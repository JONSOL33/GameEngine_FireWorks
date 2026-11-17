using System;
using UnityEngine;

namespace FireflyFestival
{
    public enum BloomShape { Chrysanthemum, Willow, Ring, DoubleRing, Fireflies }

    [Serializable]
    public struct FireworkCue
    {
        public float time;
        public Vector3 position;
        public BloomShape shape;
        public int paletteIndex;
        public float scale;
        public int seed;
    }

    [Serializable]
    public struct ShowChapter
    {
        public string title;
        public float start;
    }

    [CreateAssetMenu(menuName = "Firefly Festival/Fireworks Score")]
    public sealed class FireworksScore : ScriptableObject
    {
        public AudioClip music;
        public Color[] palette = { new Color(1f, .75f, .16f), new Color(.47f, 1f, .18f), new Color(1f, .25f, .19f) };
        public ShowChapter[] chapters;
        public FireworkCue[] cues;
        [Tooltip("Measured RMS envelope of the supplied music, sampled every 0.2 seconds.")]
        public float[] energy;
        public float envelopeStep = .2f;

        public string ChapterAt(float time)
        {
            if (chapters == null || chapters.Length == 0) return "FIREFLY NOCTURNE";
            int index = 0;
            for (int i = 1; i < chapters.Length && time >= chapters[i].start; i++) index = i;
            return chapters[index].title;
        }
    }
}
