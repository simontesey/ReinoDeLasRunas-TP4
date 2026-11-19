using System.Collections.Generic;
using UnityEngine;

namespace ReinoRunas
{
    public enum SfxId
    {
        None,
        UiMove, UiConfirm, UiCancel, UiError, Ready, BattleStart,
        Slash, HeavySlash, ShieldBash, Hit, Crit, Death, Defend, Taunt,
        Fireball, Explosion, Thunder, Poison, Burn, Paralyze,
        Heal, Buff, Debuff, Song, Haste, Slow, Potion, Cure,
        MeteorCharge, MeteorFall, Victory, Defeat, Step
    }

    /// <summary>Sonidos SINTETIZADOS por código (no hace falta importar audio).</summary>
    public static class Sfx
    {
        const int SR = 44100;
        public static float MasterVolume = 0.8f;
        static readonly Dictionary<SfxId, AudioClip> cache = new Dictionary<SfxId, AudioClip>();
        static readonly System.Random rng = new System.Random(77);

        enum Wave { Sine, Square, Saw, Voice, Tri }

        public static void Play(SfxId id, float volume = 1f, float pitchVariation = 0.05f, float pitch = 1f)
        {
            var clip = Get(id);
            if (clip == null) return;
            var go = new GameObject("SFX_" + id);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume * MasterVolume;
            src.pitch = pitch + Random.Range(-pitchVariation, pitchVariation);
            src.spatialBlend = 0f;
            src.ignoreListenerPause = true;
            src.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
        }

        public static AudioClip Get(SfxId id)
        {
            if (id == SfxId.None) return null;
            if (cache.TryGetValue(id, out var c) && c != null) return c;
            c = Build(id);
            cache[id] = c;
            return c;
        }

        static AudioClip Build(SfxId id)
        {
            float[] b;
            switch (id)
            {
                case SfxId.UiMove: b = Buf(0.05f); Sweep(b, 0f, 0.04f, 1200f, 1300f, 0.25f, 40f, Wave.Square); break;
                case SfxId.UiConfirm: b = Buf(0.16f); Notes(b, 0f, 0.06f, 0.25f, Wave.Square, 880f, 1320f); break;
                case SfxId.UiCancel: b = Buf(0.16f); Notes(b, 0f, 0.06f, 0.25f, Wave.Square, 700f, 500f); break;
                case SfxId.UiError: b = Buf(0.22f); Notes(b, 0f, 0.1f, 0.3f, Wave.Square, 180f, 150f); break;
                case SfxId.Ready: b = Buf(0.25f); Notes(b, 0f, 0.07f, 0.22f, Wave.Tri, 660f, 990f, 1320f); break;
                case SfxId.BattleStart:
                    b = Buf(1.2f);
                    Notes(b, 0f, 0.12f, 0.3f, Wave.Square, 392f, 392f, 523f);
                    Sweep(b, 0.36f, 0.8f, 659f, 659f, 0.3f, 2f, Wave.Square, 6f, 6f);
                    Sweep(b, 0.36f, 0.8f, 330f, 330f, 0.2f, 2f, Wave.Saw);
                    break;
                case SfxId.Slash:
                    b = Buf(0.25f);
                    Noise(b, 0f, 0.2f, 0.6f, 14f, 0.6f, 0.03f);
                    Sweep(b, 0f, 0.15f, 2400f, 900f, 0.2f, 20f);
                    break;
                case SfxId.HeavySlash:
                    b = Buf(0.5f);
                    Noise(b, 0f, 0.35f, 0.8f, 7f, 0.4f, 0.08f);
                    Sweep(b, 0.05f, 0.4f, 180f, 50f, 0.8f, 6f);
                    break;
                case SfxId.ShieldBash:
                    b = Buf(0.4f);
                    Sweep(b, 0f, 0.35f, 420f, 380f, 0.5f, 10f, Wave.Square);
                    Sweep(b, 0f, 0.35f, 637f, 600f, 0.3f, 10f);
                    Noise(b, 0f, 0.06f, 0.6f, 60f, 0.8f);
                    break;
                case SfxId.Hit:
                    b = Buf(0.2f);
                    Sweep(b, 0f, 0.18f, 200f, 70f, 0.9f, 20f);
                    Noise(b, 0f, 0.08f, 0.5f, 50f, 0.4f);
                    break;
                case SfxId.Crit:
                    b = Buf(0.35f);
                    Sweep(b, 0f, 0.25f, 160f, 50f, 1f, 10f);
                    Noise(b, 0f, 0.12f, 0.7f, 30f, 0.6f);
                    Sweep(b, 0.02f, 0.25f, 1800f, 2600f, 0.25f, 10f);
                    break;
                case SfxId.Death:
                    b = Buf(0.9f);
                    Sweep(b, 0f, 0.9f, 300f, 60f, 0.7f, 3f, Wave.Voice, 5f, 15f);
                    Noise(b, 0.3f, 0.5f, 0.3f, 6f, 0.1f);
                    break;
                case SfxId.Defend:
                    b = Buf(0.3f);
                    Sweep(b, 0f, 0.3f, 520f, 500f, 0.4f, 9f, Wave.Tri);
                    Sweep(b, 0f, 0.3f, 780f, 760f, 0.25f, 9f);
                    break;
                case SfxId.Taunt:
                    b = Buf(0.6f);
                    Sweep(b, 0f, 0.5f, 140f, 220f, 0.7f, 3f, Wave.Voice, 8f, 20f);
                    Noise(b, 0f, 0.5f, 0.2f, 4f, 0.2f);
                    break;
                case SfxId.Fireball:
                    b = Buf(0.7f);
                    Noise(b, 0f, 0.7f, 0.7f, 3f, 0.15f, 0.15f);
                    Sweep(b, 0f, 0.5f, 300f, 600f, 0.2f, 3f, Wave.Saw);
                    break;
                case SfxId.Explosion:
                    b = Buf(1.3f);
                    Noise(b, 0f, 1.3f, 1f, 3.5f, 0.05f);
                    Sweep(b, 0f, 0.9f, 90f, 30f, 0.8f, 4f);
                    Noise(b, 0f, 0.12f, 0.5f, 25f, 0.6f);
                    break;
                case SfxId.Thunder:
                    b = Buf(1f);
                    Noise(b, 0f, 0.15f, 0.9f, 15f, 0.95f);
                    Noise(b, 0.05f, 0.9f, 0.9f, 3f, 0.08f);
                    Sweep(b, 0f, 0.2f, 3000f, 400f, 0.2f, 10f, Wave.Square);
                    break;
                case SfxId.Poison:
                    b = Buf(0.6f);
                    for (int i = 0; i < 5; i++) Sweep(b, i * 0.1f, 0.08f, 300f + i * 60f, 600f + i * 80f, 0.3f, 20f);
                    break;
                case SfxId.Burn:
                    b = Buf(0.5f);
                    Noise(b, 0f, 0.5f, 0.6f, 5f, 0.3f, 0.05f);
                    Noise(b, 0.1f, 0.03f, 0.5f, 80f, 0.9f);
                    Noise(b, 0.25f, 0.03f, 0.5f, 80f, 0.9f);
                    break;
                case SfxId.Paralyze:
                    b = Buf(0.5f);
                    for (int i = 0; i < 6; i++) Noise(b, i * 0.07f, 0.04f, 0.7f, 40f, 0.95f);
                    Sweep(b, 0f, 0.45f, 120f, 120f, 0.3f, 4f, Wave.Square);
                    break;
                case SfxId.Heal:
                    b = Buf(0.9f);
                    Notes(b, 0f, 0.1f, 0.22f, Wave.Sine, 523f, 659f, 784f, 1046f, 1318f);
                    Sweep(b, 0f, 0.9f, 2093f, 2093f, 0.05f, 3f, Wave.Sine, 7f, 20f);
                    break;
                case SfxId.Buff:
                    b = Buf(0.6f);
                    Sweep(b, 0f, 0.5f, 300f, 900f, 0.3f, 3f, Wave.Tri);
                    Sweep(b, 0f, 0.5f, 450f, 1350f, 0.2f, 3f);
                    break;
                case SfxId.Debuff:
                    b = Buf(0.6f);
                    Sweep(b, 0f, 0.5f, 700f, 200f, 0.3f, 3f, Wave.Tri);
                    Sweep(b, 0f, 0.5f, 740f, 210f, 0.2f, 3f, Wave.Saw);
                    break;
                case SfxId.Song:
                    b = Buf(1.0f);
                    Notes(b, 0f, 0.16f, 0.2f, Wave.Tri, 587f, 740f, 880f, 740f, 988f, 880f);
                    break;
                case SfxId.Haste:
                    b = Buf(0.5f);
                    for (int i = 0; i < 8; i++) Sweep(b, i * 0.05f, 0.05f, 800f + i * 150f, 900f + i * 150f, 0.2f, 20f, Wave.Square);
                    break;
                case SfxId.Slow:
                    b = Buf(0.8f);
                    Sweep(b, 0f, 0.8f, 600f, 150f, 0.3f, 2f, Wave.Tri, 3f, 30f);
                    break;
                case SfxId.Potion:
                    b = Buf(0.6f);
                    for (int i = 0; i < 4; i++) Sweep(b, i * 0.08f, 0.07f, 500f + rng.Next(0, 300), 900f + rng.Next(0, 300), 0.25f, 25f);
                    Notes(b, 0.32f, 0.08f, 0.2f, Wave.Sine, 880f, 1318f);
                    break;
                case SfxId.Cure:
                    b = Buf(0.6f);
                    Notes(b, 0f, 0.09f, 0.2f, Wave.Sine, 1046f, 1318f, 1568f, 2093f);
                    break;
                case SfxId.MeteorCharge:
                    b = Buf(2.2f);
                    Sweep(b, 0f, 2.2f, 80f, 400f, 0.4f, 0f, Wave.Saw, 6f, 10f);
                    Noise(b, 0f, 2.2f, 0.3f, 0f, 0.1f, 1.5f);
                    Sweep(b, 0f, 2.2f, 220f, 880f, 0.15f, 0f, Wave.Sine, 9f, 30f);
                    break;
                case SfxId.MeteorFall:
                    b = Buf(0.9f);
                    Noise(b, 0f, 0.9f, 0.6f, 1f, 0.2f, 0.6f);
                    Sweep(b, 0f, 0.9f, 1600f, 300f, 0.15f, 1f);
                    break;
                case SfxId.Victory:
                    b = Buf(2f);
                    Notes(b, 0f, 0.13f, 0.28f, Wave.Square, 523f, 523f, 523f);
                    Sweep(b, 0.39f, 0.35f, 523f, 523f, 0.28f, 2f, Wave.Square);
                    Notes(b, 0.75f, 0.13f, 0.28f, Wave.Square, 415f, 466f);
                    Sweep(b, 1.01f, 0.9f, 523f, 523f, 0.3f, 1.5f, Wave.Square, 6f, 6f);
                    Sweep(b, 1.01f, 0.9f, 262f, 262f, 0.2f, 1.5f, Wave.Tri);
                    break;
                case SfxId.Defeat:
                    b = Buf(2f);
                    Notes(b, 0f, 0.35f, 0.28f, Wave.Tri, 392f, 349f, 311f);
                    Sweep(b, 1.05f, 0.9f, 262f, 250f, 0.3f, 1.5f, Wave.Tri, 4f, 4f);
                    break;
                case SfxId.Step:
                    b = Buf(0.08f);
                    Noise(b, 0f, 0.06f, 0.4f, 60f, 0.3f);
                    break;
                default: b = Buf(0.05f); break;
            }
            return ToClip(id.ToString(), b);
        }

        static float[] Buf(float s) => new float[Mathf.Max(1, (int)(s * SR))];

        static void Sweep(float[] b, float start, float dur, float f0, float f1, float amp, float decay,
            Wave wave = Wave.Sine, float vibHz = 0f, float vibDepth = 0f, float attack = 0.005f)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= b.Length) break;
                float t = i / (float)SR, k = i / (float)n;
                float f = Mathf.Lerp(f0, f1, k);
                if (vibHz > 0f) f += Mathf.Sin(2f * Mathf.PI * vibHz * t) * vibDepth;
                phase += f / SR;
                float p = (float)(phase - System.Math.Floor(phase));
                float v;
                switch (wave)
                {
                    case Wave.Square: v = (p < 0.5f ? 1f : -1f) * 0.55f; break;
                    case Wave.Saw: v = (2f * p - 1f) * 0.6f; break;
                    case Wave.Tri: v = (1f - 4f * Mathf.Abs(p - 0.5f)) * 0.9f; break;
                    case Wave.Voice:
                        float a = 2f * Mathf.PI * p;
                        v = (Mathf.Sin(a) + 0.45f * Mathf.Sin(2f * a) + 0.25f * Mathf.Sin(3f * a)) * 0.6f;
                        break;
                    default: v = Mathf.Sin(2f * Mathf.PI * p); break;
                }
                float env = decay > 0f ? Mathf.Exp(-decay * t) : 1f;
                env *= Mathf.Clamp01(t / Mathf.Max(attack, 0.0001f));
                env *= Mathf.Clamp01((n - i) / (SR * 0.01f));
                b[idx] += v * amp * env;
            }
        }

        static void Noise(float[] b, float start, float dur, float amp, float decay, float lowpass, float attack = 0.002f)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            float y = 0f, alpha = Mathf.Clamp(lowpass, 0.01f, 1f);
            float comp = Mathf.Min(4f, 1f / Mathf.Sqrt(alpha));
            for (int i = 0; i < n; i++)
            {
                int idx = s0 + i;
                if (idx >= b.Length) break;
                float t = i / (float)SR;
                y += alpha * ((float)(rng.NextDouble() * 2.0 - 1.0) - y);
                float env = decay > 0f ? Mathf.Exp(-decay * t) : 1f;
                env *= Mathf.Clamp01(t / Mathf.Max(attack, 0.0001f));
                env *= Mathf.Clamp01((n - i) / (SR * 0.01f));
                b[idx] += y * comp * amp * env;
            }
        }

        static void Notes(float[] b, float start, float noteDur, float amp, Wave wave, params float[] freqs)
        {
            for (int i = 0; i < freqs.Length; i++)
                Sweep(b, start + i * noteDur, noteDur * 1.1f, freqs[i], freqs[i], amp, 6f, wave);
        }

        static AudioClip ToClip(string name, float[] b)
        {
            float peak = 0f;
            for (int i = 0; i < b.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(b[i]));
            if (peak > 0.95f)
                for (int i = 0; i < b.Length; i++) b[i] *= 0.95f / peak;
            var clip = AudioClip.Create(name, b.Length, 1, SR, false);
            clip.SetData(b, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }
    }
}
