using System.Collections.Generic;
using UnityEngine;
using Dejarik;

namespace Dejarik.View
{
    // Plays the imported MP3 SFX pack, each from its 3D world location (a creature's roar comes from that
    // creature, the dice from the board, a click from the cell you pushed). Per-creature attack/hit/death
    // clips live in Resources/Audio. Move + click are short synth blips. Clips play at ~half volume.
    public class GameAudio : MonoBehaviour
    {
        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        AudioClip Load(string name)
        {
            if (_cache.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>($"Audio/{name}");
            _cache[name] = c;
            if (c == null) Debug.LogWarning($"[Audio] missing clip Audio/{name}");
            return c;
        }

        static string Key(PieceType t) => Pieces.IdOf(t);

        // Per-creature, at the creature's position.
        public void Attack(PieceType t, Vector3 pos, bool finisher) => PlayAt(Load($"{Key(t)}_attack"), pos, finisher ? 0.65f : 0.5f);
        public void Hit(PieceType t, Vector3 pos) => PlayAt(Load($"{Key(t)}_hit"), pos, 0.5f);
        public void Death(PieceType t, Vector3 pos) => PlayAt(Load($"{Key(t)}_death"), pos, 0.5f);

        // Shared, positioned at the board / event.
        public void Dice(Vector3 pos) => PlayAt(Load("dice"), pos, 0.5f);
        public void Victory(Vector3 pos) => PlayAt(Load("victory"), pos, 0.55f);

        // Synth blips (no MP3): a click where you selected, a soft blip where a piece moves.
        public void Click(Vector3 pos) => PlayAt(Tone(0.05f, 1300f, 0.02f), pos, 0.45f);
        public void Move(Vector3 pos) => PlayAt(Tone(0.12f, 520f, 0.22f), pos, 0.35f);

        // Spawn a temporary 3D audio source at the world position so the sound comes from there.
        void PlayAt(AudioClip clip, Vector3 pos, float vol)
        {
            if (clip == null) return;
            var go = new GameObject("sfx");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip;
            s.volume = vol;
            s.spatialBlend = 1f;          // fully 3D
            s.minDistance = 0.3f;
            s.maxDistance = 12f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = 0f;
            s.Play();
            Destroy(go, clip.length + 0.1f);
        }

        static AudioClip Tone(float dur, float freq, float decay)
        {
            int sr = 44100, n = (int)(sr * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                d[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t / Mathf.Max(0.001f, decay));
            }
            var c = AudioClip.Create("blip", n, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
