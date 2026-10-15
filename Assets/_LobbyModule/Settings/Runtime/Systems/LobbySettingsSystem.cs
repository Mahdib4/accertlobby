using Accertone.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Accertone.Settings
{
    /// <summary>
    /// Lobby-facing preferences that take effect right away: interface animation speed (every reveal and transition),
    /// showroom camera motion, and speed units (labels tagged with <see cref="SpeedUnitsLabel"/>).
    /// </summary>
    public class LobbySettingsSystem : ISettingsSystem
    {
        static readonly float[] AnimationScale = { 1f, 0.45f, 0f };

        AccertoneSettings m_S;

        public bool Imperial { get { return m_S != null && m_S.Int(SettingKeys.SpeedUnits) == 1; } }
        public bool NotificationsOn { get { return m_S == null || m_S.Bool(SettingKeys.Notifications); } }

        public void Initialise(AccertoneSettings settings) { m_S = settings; }

        public void OnSettingChanged(string key)
        {
            if (key == SettingKeys.InterfaceAnimations || key == SettingKeys.ShowroomMotion || key == SettingKeys.SpeedUnits) ApplyAll();
        }

        public void ApplyAll()
        {
            AccertoneMotion.Scale = AnimationScale[Mathf.Clamp(m_S.Int(SettingKeys.InterfaceAnimations), 0, 2)];
            AccertoneMotion.CameraMotion = m_S.Bool(SettingKeys.ShowroomMotion);
            SpeedUnitsLabel.ApplyAll(Imperial);
        }

        public void OnSceneLoaded(Scene scene) { SpeedUnitsLabel.ApplyAll(Imperial); }

        public void Shutdown()
        {
            AccertoneMotion.Scale = 1f;
            AccertoneMotion.CameraMotion = true;
        }
    }
}
