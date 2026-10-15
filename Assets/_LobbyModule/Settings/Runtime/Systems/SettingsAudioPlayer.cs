using System.Collections.Generic;
using UnityEngine;

namespace Accertone.Settings
{
    public enum UiSound { Move, Change, Category, Confirm, Back, Error }

    /// <summary>
    /// Interface sounds and per-channel test sounds, synthesised at startup (no audio assets needed) and routed
    /// through the mixer groups, so every volume slider can be heard. Also reports the live level of each channel
    /// for the settings screen's meters.
    /// </summary>
    public class SettingsAudioPlayer : MonoBehaviour
    {
        const int Voices = 3;

        AudioSettingsSystem m_Audio;
        int m_Rate;
        readonly Dictionary<UiSound, AudioClip> m_Ui = new Dictionary<UiSound, AudioClip>();
        readonly Dictionary<string, AudioClip> m_Tests = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioSource[]> m_Sources = new Dictionary<string, AudioSource[]>();
        readonly float[] m_Buffer = new float[256];
        float m_LastUi;

        public void Initialise(AudioSettingsSystem audio)
        {
            m_Audio = audio;
            m_Rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            foreach (var ch in AudioSettingsSystem.Channels)
            {
                var arr = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    var src = gameObject.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 0f;
                    src.ignoreListenerPause = ch == "Interface";
                    src.outputAudioMixerGroup = ch == "Master" ? audio.Group("Music") : audio.Group(ch);
                    arr[i] = src;
                }
                m_Sources[ch] = arr;
            }
            BuildClips();
        }

        /// <summary>Short interface feedback on the Interface channel.</summary>
        public void Play(UiSound sound)
        {
            if (sound == UiSound.Move && Time.unscaledTime - m_LastUi < 0.03f) return;
            m_LastUi = Time.unscaledTime;
            AudioClip c;
            if (m_Ui.TryGetValue(sound, out c)) PlayOn("Interface", c, sound == UiSound.Move ? 0.55f : 0.8f);
        }

        /// <summary>A recognisable sound on one channel (Master plays music and engine together).</summary>
        public void PlayTest(string channel)
        {
            if (channel == "Master") { PlayTest("Music"); PlayTest("Engine"); return; }
            AudioClip c;
            if (m_Tests.TryGetValue(channel, out c)) PlayOn(channel, c, 1f);
        }

        /// <summary>Current output level (0..1) of the sounds playing on a channel.</summary>
        public float Level(string channel)
        {
            if (channel == "Master")
            {
                float m = 0f;
                foreach (var ch in AudioSettingsSystem.Channels) if (ch != "Master") m = Mathf.Max(m, Level(ch));
                return m;
            }
            AudioSource[] arr;
            if (!m_Sources.TryGetValue(channel, out arr)) return 0f;
            float peak = 0f;
            foreach (var s in arr)
            {
                if (!s.isPlaying) continue;
                s.GetOutputData(m_Buffer, 0);
                float sum = 0f;
                for (int i = 0; i < m_Buffer.Length; i++) sum += m_Buffer[i] * m_Buffer[i];
                peak = Mathf.Max(peak, Mathf.Sqrt(sum / m_Buffer.Length));
            }
            return Mathf.Clamp01(peak * 3.2f * m_Audio.ChannelLevel("Master") * m_Audio.ChannelLevel(channel));
        }

        void PlayOn(string channel, AudioClip clip, float volume)
        {
            AudioSource[] arr;
            if (!m_Sources.TryGetValue(channel, out arr)) return;
            AudioSource pick = arr[0];
            foreach (var s in arr) if (!s.isPlaying) { pick = s; break; }
            pick.clip = clip;
            pick.volume = volume;
            pick.Play();
        }

        // ------------------------------------------------------------------ synthesis
        void BuildClips()
        {
            m_Ui[UiSound.Move] = Tone("ui_move", 0.045f, 1500f, 0.18f);
            m_Ui[UiSound.Change] = Tone("ui_change", 0.05f, 2100f, 0.22f);
            m_Ui[UiSound.Category] = Tone("ui_category", 0.08f, 980f, 0.25f);
            m_Ui[UiSound.Confirm] = Sweep("ui_confirm", 0.16f, 900f, 1600f, 0.25f);
            m_Ui[UiSound.Back] = Sweep("ui_back", 0.14f, 1300f, 700f, 0.22f);
            m_Ui[UiSound.Error] = Buzz("ui_error", 0.18f, 180f, 0.2f);

            m_Tests["Music"] = Chord("test_music", 1.8f);
            m_Tests["Engine"] = Engine("test_engine", 1.7f);
            m_Tests["Effects"] = Noise("test_effects", 0.4f, 0.12f, 0.6f);
            m_Tests["Environment"] = Noise("test_environment", 2.2f, 0.92f, 0.35f);
            m_Tests["Interface"] = Ticks("test_interface");
            m_Tests["Voice"] = Voice("test_voice", 1.3f);
        }

        AudioClip Make(string name, float seconds, System.Func<int, float, float> sample)
        {
            int n = Mathf.Max(1, (int)(seconds * m_Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = sample(i, i / (float)m_Rate);
            var clip = AudioClip.Create(name, n, 1, m_Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Env(float t, float attack, float length)
        {
            if (t < attack) return t / attack;
            return Mathf.Exp(-5f * (t - attack) / Mathf.Max(0.001f, length - attack));
        }

        AudioClip Tone(string n, float len, float f, float amp)
        {
            return Make(n, len, (i, t) => Mathf.Sin(2f * Mathf.PI * f * t) * Env(t, 0.002f, len) * amp);
        }

        AudioClip Sweep(string n, float len, float f0, float f1, float amp)
        {
            return Make(n, len, (i, t) =>
            {
                float k = t / len;
                float phase = 2f * Mathf.PI * (f0 * t + (f1 - f0) * 0.5f * t * k);
                return Mathf.Sin(phase) * Env(t, 0.004f, len) * amp;
            });
        }

        AudioClip Buzz(string n, float len, float f, float amp)
        {
            return Make(n, len, (i, t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.5f * Env(t, 0.004f, len) * amp);
        }

        AudioClip Chord(string n, float len)
        {
            float[] notes = { 261.6f, 329.6f, 392f, 523.3f };
            return Make(n, len, (i, t) =>
            {
                float s = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float tk = t - k * 0.06f;
                    if (tk < 0f) continue;
                    float tri = 2f * Mathf.Abs(2f * Mathf.Repeat(notes[k] * tk, 1f) - 1f) - 1f;
                    s += tri * Env(tk, 0.03f, len) * 0.12f;
                }
                return s;
            });
        }

        AudioClip Engine(string n, float len)
        {
            float phase = 0f, lp = 0f;
            return Make(n, len, (i, t) =>
            {
                float k = t / len;
                float f = k < 0.65f ? Mathf.Lerp(70f, 240f, k / 0.65f) : Mathf.Lerp(240f, 120f, (k - 0.65f) / 0.35f);
                phase = Mathf.Repeat(phase + f / m_Rate, 1f);
                float saw = phase * 2f - 1f;
                lp += (saw - lp) * 0.12f;
                return lp * Env(t, 0.08f, len * 1.2f) * 0.5f;
            });
        }

        AudioClip Noise(string n, float len, float smooth, float amp)
        {
            var rng = new System.Random(7);
            float lp = 0f;
            return Make(n, len, (i, t) =>
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * (1f - smooth);
                float attack = len > 1f ? 0.5f : 0.004f;
                return lp * Env(t, attack, len) * amp * (smooth > 0.5f ? 3f : 1f);
            });
        }

        AudioClip Ticks(string n)
        {
            return Make(n, 0.3f, (i, t) =>
            {
                float a = t < 0.06f ? Mathf.Sin(2f * Mathf.PI * 1900f * t) * Env(t, 0.002f, 0.06f) : 0f;
                float t2 = t - 0.12f;
                float b = t2 > 0f && t2 < 0.06f ? Mathf.Sin(2f * Mathf.PI * 2300f * t2) * Env(t2, 0.002f, 0.06f) : 0f;
                return (a + b) * 0.3f;
            });
        }

        AudioClip Voice(string n, float len)
        {
            float bp1 = 0f, bp2 = 0f;
            return Make(n, len, (i, t) =>
            {
                float pulse = Mathf.Repeat(140f * t, 1f) < 0.12f ? 1f : -0.14f;
                float formant = 800f + 300f * Mathf.Sin(2f * Mathf.PI * 6f * t);
                float c = Mathf.Clamp01(2f * Mathf.PI * formant / m_Rate);
                bp1 += (pulse - bp1) * c;
                bp2 += (bp1 - bp2) * c;
                return (bp1 - bp2) * Env(t, 0.05f, len) * 0.9f;
            });
        }
    }
}
