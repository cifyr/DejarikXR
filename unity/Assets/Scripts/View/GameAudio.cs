using UnityEngine;

namespace Dejarik.View
{
    // Procedural holographic SFX (no asset files): short generated clips for dice, strikes, deaths,
    // moves, and victory. Spatialized at the board so they read as coming from the hologram.
    public class GameAudio : MonoBehaviour
    {
        AudioSource _src;

        void Awake()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 1f;
            _src.minDistance = 0.4f;
            _src.maxDistance = 12f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.dopplerLevel = 0f;
        }

        public void PlayDice() => Play(Noise(0.5f, 0.25f, 1600f), 0.5f);
        public void PlayRoar() => Play(Roar(0.6f), 0.7f);
        public void PlayMove() => Play(Tone(0.12f, 420f, 0.3f), 0.4f);
        public void PlayStrike() => Play(Tone(0.18f, 180f, 0.02f, true), 0.8f);
        public void PlayDeath() => Play(Sweep(0.5f, 900f, 90f), 0.7f);
        public void PlayVictory() => Play(Chord(0.9f, new[] { 392f, 523f, 659f }), 0.6f);

        void Play(AudioClip clip, float vol) { if (clip != null) _src.PlayOneShot(clip, vol); }

        static AudioClip Tone(float dur, float freq, float decay, bool noisy = false)
        {
            int sr = 44100, n = (int)(sr * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Exp(-t / Mathf.Max(0.001f, decay));
                float s = Mathf.Sin(2f * Mathf.PI * freq * t);
                if (noisy) s = 0.6f * s + 0.4f * (Random.value * 2f - 1f);
                d[i] = s * env;
            }
            return Clip("tone", d, sr);
        }

        static AudioClip Sweep(float dur, float f0, float f1)
        {
            int sr = 44100, n = (int)(sr * dur);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t);
                phase += 2f * Mathf.PI * f / sr;
                d[i] = Mathf.Sin(phase) * (1f - t);
            }
            return Clip("sweep", d, sr);
        }

        static AudioClip Noise(float dur, float decay, float lp)
        {
            int sr = 44100, n = (int)(sr * dur);
            var d = new float[n];
            float s = 0f, a = lp / (lp + sr);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                s = Mathf.Lerp(s, Random.value * 2f - 1f, a);
                d[i] = s * Mathf.Exp(-t / decay);
            }
            return Clip("noise", d, sr);
        }

        // Low growl with vibrato + grit — a creature roar.
        static AudioClip Roar(float dur)
        {
            int sr = 44100, n = (int)(sr * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float vib = 1f + 0.06f * Mathf.Sin(2f * Mathf.PI * 18f * t);
                float f = 110f * vib;
                float saw = 2f * (t * f - Mathf.Floor(0.5f + t * f));
                float env = Mathf.Min(1f, t * 10f) * Mathf.Exp(-t / 0.4f);
                d[i] = (0.7f * saw + 0.3f * (Random.value * 2f - 1f)) * env;
            }
            return Clip("roar", d, sr);
        }

        static AudioClip Chord(float dur, float[] freqs)
        {
            int sr = 44100, n = (int)(sr * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                float env = Mathf.Min(1f, t * 8f) * Mathf.Exp(-t / 0.6f);
                float s = 0f;
                foreach (var f in freqs) s += Mathf.Sin(2f * Mathf.PI * f * t);
                d[i] = s / freqs.Length * env;
            }
            return Clip("chord", d, sr);
        }

        static AudioClip Clip(string name, float[] data, int sr)
        {
            var c = AudioClip.Create(name, data.Length, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
