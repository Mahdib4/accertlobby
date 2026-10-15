using System;
using Accertone.Branding;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Accertone.LobbyModule
{
    /// <summary>
    /// Lobby-only character body control (isolated from any race driver / gameplay controller).
    ///
    /// In the lobby the driver stays in a small presentation spot: <see cref="LobbyCharacterPresentation"/>
    /// turns the body in place with <see cref="FaceTowards"/>, at a limited, eased angular speed, and the legs
    /// step around the turn (<see cref="ProceduralLocomotion"/>), so it never spins like a mannequin.
    /// Walking (<see cref="WalkTo"/>, or player input from the project's "Player/Move" action when
    /// <see cref="acceptPlayerInput"/> is on) is kept for other scenes and is off in the lobby presentation;
    /// when used, the body always moves along its facing, stays out of the car's clearance envelope and
    /// ignores input while the UI has focus or a branded transition is playing.
    ///
    /// Assign <see cref="animator"/> for a rigged humanoid with clips: it then receives the "Speed" parameter.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyCharacterController : MonoBehaviour
    {
        public LobbyWalkArea walkArea;
        [Tooltip("Movement is relative to this camera. Defaults to the main camera.")]
        public Transform viewCamera;
        [Tooltip("What the character pays attention to when idle (the display car).")]
        public Transform attentionTarget;

        [Header("Speed")]
        public float walkSpeed = 1.15f;
        public float briskSpeed = 1.55f;
        public float acceleration = 1.9f;
        public float deceleration = 2.8f;

        [Header("Turning")]
        public float turnSmoothTime = 0.25f;
        [Tooltip("Degrees per second.")]
        public float maxTurnSpeed = 200f;
        [Tooltip("Heading error (degrees) above which the character slows down to turn.")]
        public float slowTurnAngle = 40f;
        [Tooltip("Heading error at which forward speed reaches zero (turning on the spot with steps).")]
        public float stopTurnAngle = 140f;
        [Tooltip("Ease-in / ease-out time of a FaceTowards turn made with a custom turn speed (presentation turns).")]
        public float presentationTurnSmoothTime = 0.55f;

        [Header("Input (project Input System actions)")]
        public string moveAction = "Player/Move";
        public string sprintAction = "Player/Sprint";
        public bool acceptPlayerInput = true;

        [Header("Optional Animator (rigged humanoid)")]
        public Animator animator;
        public string speedParameter = "Speed";

        public float Speed { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float Yaw { get { return m_Yaw; } }
        public bool IsMoving { get { return Speed > 0.05f; } }
        public bool HasDestination { get { return m_HasTarget; } }
        /// <summary>True while a FaceTowards turn is in progress.</summary>
        public bool IsTurning { get { return m_HasFacing; } }
        /// <summary>Peak body turn rate (deg/s) since the last reset; bounded by maxTurnSpeed, so no snapping.</summary>
        public float MaxYawRate { get; private set; }
        public void ResetDiagnostics() { MaxYawRate = 0f; }

#if ENABLE_INPUT_SYSTEM
        InputAction m_Move;
        InputAction m_Sprint;
#endif
        float m_Yaw;
        float m_YawVelocity;
        float m_TargetYaw;
        float m_GroundVelocity;
        bool m_HasTarget;
        Vector3 m_Target;
        float m_ArriveRadius;
        Action m_OnArrive;
        bool m_HasFacing;
        Vector3 m_FacingPoint;
        float m_FaceSpeed;
        AccertoneTransitionOverlay m_Overlay;

        void Start()
        {
            if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
            m_Yaw = m_TargetYaw = transform.eulerAngles.y;
            m_Overlay = FindFirstObjectByType<AccertoneTransitionOverlay>();
#if ENABLE_INPUT_SYSTEM
            var actions = InputSystem.actions;
            if (actions != null)
            {
                m_Move = actions.FindAction(moveAction);
                m_Sprint = actions.FindAction(sprintAction);
                if (m_Move != null && !m_Move.enabled) m_Move.Enable();
                if (m_Sprint != null && !m_Sprint.enabled) m_Sprint.Enable();
            }
#endif
            var p = transform.position;
            if (walkArea != null) p.y = walkArea.GroundHeight(p);
            transform.position = p;
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Walk to a point (e.g. an inspection spot), then call onArrive.</summary>
        public void WalkTo(Vector3 point, Action onArrive = null, float arriveRadius = 0.18f)
        {
            m_Target = point;
            m_HasTarget = true;
            m_OnArrive = onArrive;
            m_ArriveRadius = arriveRadius;
            m_HasFacing = false;
        }

        /// <summary>Turn (with steps) to face a point while standing. turnSpeed (deg/s) &gt; 0 makes it a slow, eased
        /// presentation turn; 0 uses the regular turning limits.</summary>
        public void FaceTowards(Vector3 point, float turnSpeed = 0f)
        {
            m_FacingPoint = point;
            m_HasFacing = true;
            m_FaceSpeed = turnSpeed;
        }

        /// <summary>Set the body's facing immediately (initial pose before anything is shown).</summary>
        public void SetYaw(float yaw)
        {
            m_Yaw = m_TargetYaw = yaw;
            m_YawVelocity = 0f;
            m_HasFacing = false;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void Stop()
        {
            m_HasTarget = false;
            m_OnArrive = null;
        }

        // ------------------------------------------------------------------ update

        bool UiHasFocus()
        {
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null && es.currentSelectedGameObject.activeInHierarchy) return true;
            return m_Overlay != null && m_Overlay.IsBusy;
        }

        Vector2 ReadPlayerInput(out bool brisk)
        {
            brisk = false;
            if (!acceptPlayerInput || UiHasFocus()) return Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (m_Move != null)
            {
                brisk = m_Sprint != null && m_Sprint.IsPressed();
                return Vector2.ClampMagnitude(m_Move.ReadValue<Vector2>(), 1f);
            }
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 v = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                brisk = kb.leftShiftKey.isPressed;
                return Vector2.ClampMagnitude(v, 1f);
            }
#endif
            return Vector2.zero;
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            Vector3 pos = transform.position;

            bool brisk;
            Vector2 input = ReadPlayerInput(out brisk);
            Vector3 desiredDir = Vector3.zero;
            float desiredMag = 0f;

            if (input.sqrMagnitude > 0.0025f)
            {
                // Player input always wins over scripted walking.
                m_HasTarget = false;
                m_HasFacing = false;
                Vector3 f = viewCamera != null ? Vector3.ProjectOnPlane(viewCamera.forward, Vector3.up).normalized : Vector3.forward;
                Vector3 r = viewCamera != null ? Vector3.ProjectOnPlane(viewCamera.right, Vector3.up).normalized : Vector3.right;
                desiredDir = f * input.y + r * input.x;
                desiredMag = Mathf.Clamp01(desiredDir.magnitude);
                desiredDir.Normalize();
            }
            else if (m_HasTarget)
            {
                Vector3 to = m_Target - pos;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist <= m_ArriveRadius)
                {
                    m_HasTarget = false;
                    var cb = m_OnArrive;
                    m_OnArrive = null;
                    if (cb != null) cb();
                }
                else
                {
                    desiredDir = to / dist;
                    desiredMag = Mathf.Clamp01(dist / 0.7f) * 0.85f + 0.15f;
                }
            }

            if (desiredMag > 0f) m_TargetYaw = Mathf.Atan2(desiredDir.x, desiredDir.z) * Mathf.Rad2Deg;
            else if (m_HasFacing)
            {
                Vector3 to = m_FacingPoint - pos;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) m_TargetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(m_Yaw, m_TargetYaw)) < 2f) m_HasFacing = false;
            }

            // Smooth, speed-limited turning: never a snap, never an instant 180.
            float yawBefore = m_Yaw;
            bool presenting = m_HasFacing && desiredMag <= 0f && m_FaceSpeed > 0f;
            m_Yaw = Mathf.SmoothDampAngle(m_Yaw, m_TargetYaw, ref m_YawVelocity,
                presenting ? presentationTurnSmoothTime : turnSmoothTime, presenting ? m_FaceSpeed : maxTurnSpeed, dt);
            float headingError = Mathf.Abs(Mathf.DeltaAngle(m_Yaw, m_TargetYaw));
            float turnFactor = 1f - Mathf.Clamp01((headingError - slowTurnAngle) / Mathf.Max(1f, stopTurnAngle - slowTurnAngle));

            float topSpeed = brisk ? briskSpeed : walkSpeed;
            float targetSpeed = desiredMag * topSpeed * turnFactor;
            Speed = Mathf.MoveTowards(Speed, targetSpeed, (targetSpeed > Speed ? acceleration : deceleration) * dt);

            Vector3 forward = Quaternion.Euler(0f, m_Yaw, 0f) * Vector3.forward;
            Vector3 next = pos + forward * (Speed * dt);
            if (walkArea != null) next = walkArea.Constrain(next);

            Vector3 moved = next - pos;
            moved.y = 0f;
            Velocity = moved / dt;
            float actual = Velocity.magnitude;

            // If the clearance envelope deflected us, turn to walk along it rather than sliding sideways.
            if (actual > 0.15f && Speed > 0.15f)
            {
                float movedYaw = Mathf.Atan2(moved.x, moved.z) * Mathf.Rad2Deg;
                if (Mathf.Abs(Mathf.DeltaAngle(m_Yaw, movedYaw)) > 12f)
                    m_Yaw = Mathf.MoveTowardsAngle(m_Yaw, movedYaw, maxTurnSpeed * 0.6f * dt);
            }
            Speed = Mathf.Min(Speed, actual + 0.05f);

            if (walkArea != null)
            {
                float ground = walkArea.GroundHeight(next);
                next.y = Mathf.SmoothDamp(pos.y, ground, ref m_GroundVelocity, 0.09f, Mathf.Infinity, dt);
            }

            // Never faster than maxTurnSpeed in total, deflection included.
            float turned = Mathf.DeltaAngle(yawBefore, m_Yaw);
            if (Mathf.Abs(turned) > maxTurnSpeed * dt) m_Yaw = yawBefore + Mathf.Sign(turned) * maxTurnSpeed * dt;
            float yawRate = Mathf.Abs(Mathf.DeltaAngle(yawBefore, m_Yaw)) / dt;
            if (yawRate > MaxYawRate) MaxYawRate = yawRate;
            transform.SetPositionAndRotation(next, Quaternion.Euler(0f, m_Yaw, 0f));
            if (animator != null) animator.SetFloat(speedParameter, Speed);
        }
    }
}
