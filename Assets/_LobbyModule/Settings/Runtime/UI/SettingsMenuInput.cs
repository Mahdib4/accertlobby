using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Accertone.Settings
{
    /// <summary>
    /// Keyboard and controller input for the settings screen (Input System). Commands: up, down, left, right,
    /// confirm, back, prevCat, nextCat, apply, reset, clear, cat:N. Held directions repeat. Tracks whether the
    /// player is on a keyboard or a controller so prompts can show the right glyphs.
    /// </summary>
    public class SettingsMenuInput : MonoBehaviour
    {
        const float RepeatDelay = 0.38f;
        const float RepeatRate = 0.075f;

        public event Action<string> Command;
        public event Action DeviceChanged;

        public bool UsingGamepad { get; private set; }
        public bool BigStep { get { var k = Keyboard.current; return k != null && k.shiftKey.isPressed; } }

        InputActionMap m_Map;
        InputAction m_Navigate;
        readonly List<InputAction> m_Buttons = new List<InputAction>();
        Vector2Int m_Held;
        float m_NextRepeat;
        float m_SuspendedUntil;
        bool m_Suspended;

        void Awake()
        {
            m_Map = new InputActionMap("SettingsMenu");
            m_Navigate = m_Map.AddAction("Navigate", InputActionType.PassThrough);
            m_Navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            m_Navigate.AddBinding("<Gamepad>/dpad");
            m_Navigate.AddBinding("<Gamepad>/leftStick");
            m_Navigate.performed += ctx => NoteDevice(ctx.control);

            Button("confirm", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
            Button("back", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            Button("prevCat", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            Button("nextCat", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            Button("apply", "<Keyboard>/space", "<Gamepad>/buttonNorth");
            Button("reset", "<Keyboard>/r", "<Gamepad>/buttonWest");
            Button("clear", "<Keyboard>/backspace", "<Keyboard>/delete");
            for (int i = 1; i <= 7; i++) Button("cat:" + (i - 1), "<Keyboard>/" + i, "<Keyboard>/numpad" + i);
        }

        void Button(string command, params string[] paths)
        {
            var a = m_Map.AddAction(command, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            a.performed += ctx =>
            {
                NoteDevice(ctx.control);
                if (m_Suspended || Time.unscaledTime < m_SuspendedUntil) return;
                if (TextFieldFocused()) return;
                if (Command != null) Command(command);
            };
            m_Buttons.Add(a);
        }

        static bool TextFieldFocused()
        {
            var es = EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            return go != null && go.GetComponent<TMPro.TMP_InputField>() != null;
        }

        void NoteDevice(InputControl control)
        {
            if (control == null) return;
            bool pad = control.device is Gamepad;
            if (pad == UsingGamepad) return;
            UsingGamepad = pad;
            if (DeviceChanged != null) DeviceChanged();
        }

        void OnEnable() { m_Map.Enable(); m_Held = Vector2Int.zero; }
        void OnDisable() { m_Map.Disable(); }
        void OnDestroy() { m_Map.Dispose(); }

        /// <summary>Stops all commands (while rebinding). Resume waits a moment so the bound key doesn't also act on the menu.</summary>
        public void Suspend() { m_Suspended = true; m_Held = Vector2Int.zero; }
        public void Resume(float delay = 0.2f) { m_Suspended = false; m_SuspendedUntil = Time.unscaledTime + delay; m_Held = Vector2Int.zero; }

        void Update()
        {
            if (m_Suspended || Time.unscaledTime < m_SuspendedUntil) return;
            Vector2 v = m_Navigate.ReadValue<Vector2>();
            Vector2Int dir = Vector2Int.zero;
            if (Mathf.Abs(v.y) >= 0.6f && Mathf.Abs(v.y) >= Mathf.Abs(v.x)) dir = new Vector2Int(0, v.y > 0 ? 1 : -1);
            else if (Mathf.Abs(v.x) >= 0.6f) dir = new Vector2Int(v.x > 0 ? 1 : -1, 0);

            if (dir != m_Held)
            {
                m_Held = dir;
                if (dir != Vector2Int.zero) { Fire(dir); m_NextRepeat = Time.unscaledTime + RepeatDelay; }
            }
            else if (dir != Vector2Int.zero && Time.unscaledTime >= m_NextRepeat)
            {
                Fire(dir);
                m_NextRepeat = Time.unscaledTime + RepeatRate;
            }
        }

        void Fire(Vector2Int dir)
        {
            if (TextFieldFocused() || Command == null) return;
            if (dir.y > 0) Command("up");
            else if (dir.y < 0) Command("down");
            else if (dir.x > 0) Command("right");
            else Command("left");
        }
    }
}
