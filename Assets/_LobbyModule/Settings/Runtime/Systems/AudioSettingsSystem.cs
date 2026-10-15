using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace Accertone.Settings
{
    /// <summary>
    /// Audio: drives the Accert one Audio Mixer (Resources/AccertoneAudioMixer). Each channel is a mixer group with
    /// an exposed volume ("Vol_Master", "Vol_Music", ...); any AudioSource routed to a group follows its slider and
    /// mute. Also speaker mode and muting while the game is in the background.
    /// Route a game sound with <c>source.outputAudioMixerGroup = AccertoneSettings.Instance.Audio.Group("Engine")</c>.
    /// </summary>
    public class AudioSettingsSystem : ISettingsSystem
    {
        public const string MixerResource = "AccertoneAudioMixer";
        public static readonly string[] Channels = { "Master", "Music", "Effects", "Engine", "Environment", "Interface", "Voice" };
        static readonly string[] ChannelKeys =
        {
            SettingKeys.MasterVolume, SettingKeys.MusicVolume, SettingKeys.EffectsVolume, SettingKeys.EngineVolume,
            SettingKeys.EnvironmentVolume, SettingKeys.InterfaceVolume, SettingKeys.VoiceVolume
        };
        static readonly AudioSpeakerMode[] SpeakerModes = { AudioSpeakerMode.Stereo, AudioSpeakerMode.Quad, AudioSpeakerMode.Mode5point1, AudioSpeakerMode.Mode7point1 };

        AccertoneSettings m_S;
        readonly Dictionary<string, AudioMixerGroup> m_Groups = new Dictionary<string, AudioMixerGroup>();
        bool m_Focused = true;

        public AudioMixer Mixer { get; private set; }
        public SettingsAudioPlayer Player { get; private set; }

        public void Initialise(AccertoneSettings settings)
        {
            m_S = settings;
            Mixer = Resources.Load<AudioMixer>(MixerResource);
            if (Mixer != null)
                foreach (var g in Mixer.FindMatchingGroups(string.Empty)) m_Groups[g.name] = g;
            else
                Debug.LogWarning("[Accert one] Audio mixer missing (Resources/" + MixerResource + "); only the master volume will work. Run Accert one > Settings > Build.");
            Player = settings.gameObject.AddComponent<SettingsAudioPlayer>();
            Player.Initialise(this);
        }

        public AudioMixerGroup Group(string channel)
        {
            AudioMixerGroup g;
            return m_Groups.TryGetValue(channel, out g) ? g : null;
        }

        public static string KeyForChannel(string channel)
        {
            int i = Array.IndexOf(Channels, channel);
            return i >= 0 ? ChannelKeys[i] : null;
        }

        /// <summary>0..1 volume of a channel including its mute (not the master).</summary>
        public float ChannelLevel(string channel)
        {
            string key = KeyForChannel(channel);
            if (key == null) return 0f;
            return m_S.Bool(key + SettingKeys.MuteSuffix) ? 0f : m_S.Float(key) / 100f;
        }

        public void OnSettingChanged(string key)
        {
            string baseKey = AccertoneSettings.BaseKey(key);
            int i = Array.IndexOf(ChannelKeys, baseKey);
            if (i >= 0) { ApplyVolume(i); return; }
            if (key == SettingKeys.SpeakerMode) ApplySpeakerMode();
            if (key == SettingKeys.MuteInBackground) ApplyFocus();
        }

        public void ApplyAll()
        {
            for (int i = 0; i < Channels.Length; i++) ApplyVolume(i);
            ApplySpeakerMode();
            ApplyFocus();
        }

        public void OnSceneLoaded(Scene scene) { }

        public void Shutdown() { AudioListener.pause = false; }

        public void OnFocusChanged(bool focused)
        {
            m_Focused = focused;
            ApplyFocus();
        }

        void ApplyFocus()
        {
            AudioListener.pause = !m_Focused && m_S.Bool(SettingKeys.MuteInBackground);
        }

        void ApplyVolume(int i)
        {
            float level = ChannelLevel(Channels[i]);
            if (Mixer == null)
            {
                if (i == 0) AudioListener.volume = level;
                return;
            }
            float db = level <= 0.0001f ? -80f : Mathf.Max(-80f, 20f * Mathf.Log10(level));
            Mixer.SetFloat("Vol_" + Channels[i], db);
        }

        void ApplySpeakerMode()
        {
            // Disruptive (restarts audio), so it follows the applied value only.
            var mode = SpeakerModes[Mathf.Clamp((int)Math.Round(m_S.Store.GetApplied(SettingKeys.SpeakerMode)), 0, SpeakerModes.Length - 1)];
            var cfg = AudioSettings.GetConfiguration();
            if (cfg.speakerMode == mode) return;
            cfg.speakerMode = mode;
            if (!AudioSettings.Reset(cfg)) Debug.LogWarning("[Accert one] Speaker mode " + mode + " is not supported by this audio device.");
            for (int i = 0; i < Channels.Length; i++) ApplyVolume(i);
        }
    }
}
