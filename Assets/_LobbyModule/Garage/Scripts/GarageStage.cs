using System;
using System.Collections.Generic;
using Accertone.CarSetup;
using Accertone.Lobby;
using Accertone.UI;
using UnityEngine;

namespace Accertone.Garage
{
    /// <summary>A camera / turntable composition for the Garage.</summary>
    [Serializable]
    public struct GarageShot
    {
        /// <summary>Car heading relative to the camera: 0 = nose on, 35 = front three-quarter (nose to screen left),
        /// 90 = side profile, 180 = rear.</summary>
        public float view;
        /// <summary>Keep the turntable turning instead of holding <see cref="view"/>.</summary>
        public bool spin;
        /// <summary>Horizontal camera distance from the focus point (metres, before the aspect fit).</summary>
        public float distance;
        /// <summary>Camera height above the floor (metres).</summary>
        public float height;
        /// <summary>Car-local point the camera looks at.</summary>
        public Vector3 focus;
        public float fov;

        public static GarageShot Make(float view, bool spin, float distance, float height, Vector3 focus, float fov)
        {
            return new GarageShot { view = view, spin = spin, distance = distance, height = height, focus = focus, fov = fov };
        }
    }

    public enum GarageView { Hero = 0, Front = 1, Side = 2, Rear = 3, RearThreeQuarter = 4, Top = 5 }

    /// <summary>
    /// The 3D half of the Garage. Leaves the lobby's own systems running and layers the garage on top, so leaving
    /// restores the lobby exactly:
    /// <list type="bullet">
    /// <item>Camera: runs after <see cref="LobbyCameraRig"/> and blends from its live pose to the garage shot by a
    /// weight; at weight 0 the rig's pose (drift, parallax) passes through untouched.</item>
    /// <item>Turntable: turns the <c>DisplayPodium</c> root (the presentation spinner keeps owning the Turntable
    /// beneath it), so the dial and car turn while the lobby's rest angle is never touched. Exit returns the root to identity.</item>
    /// <item>Driver: his spinner disc sinks into the platform like a stage lift and is deactivated; it rises on exit.</item>
    /// <item>Lights: multipliers applied after the showroom power-up writes its levels each frame, plus garage
    /// softboxes and a violet kicker that fade with the weight.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class GarageStage : MonoBehaviour
    {
        const float CameraBearing = 192f;     // where the garage camera stands, seen from the turntable axis (degrees)
        const float AutoSpinSpeed = -7f;      // degrees per second (counter-clockwise from above, like the lobby)
        const float DragDegreesPerPixel = 0.24f;
        const float ResumeDelay = 2.6f;
        const float LiftDepth = 2.45f;
        const float HeroView = 35f;

        public static GarageStage Instance { get; private set; }

        Camera m_Camera;
        Transform m_Podium, m_DriverSpinner;
        Vector3 m_DriverRest;
        bool m_DriverRestKnown;
        GarageCarRig m_CarRig;
        Transform m_Car;

        // Weight 0 = lobby, 1 = garage.
        float m_Weight, m_WeightFrom, m_WeightTo, m_WeightStart, m_WeightDuration;
        bool m_Active;          // in the garage or on the way in
        Action m_OnExited;
        float m_LiftFrom, m_LiftTo, m_LiftStart, m_LiftDuration, m_Lift;
        bool m_LiftRunning;

        // Turntable (podium root yaw in degrees, unwrapped).
        float m_Yaw, m_YawVelocity, m_SpinVelocity, m_LastInput = -100f, m_PresentFrom, m_PresentTo, m_PresentStart, m_PresentDuration;
        bool m_Presenting, m_Dragging, m_UserHeld, m_Returning;
        float m_InputRotate;

        // Shot (targets and smoothed values).
        GarageShot m_Shot;
        float m_Distance, m_DistanceVel, m_Height, m_HeightVel, m_Fov, m_FovVel, m_Frame, m_FrameTarget, m_FrameVel;
        Vector3 m_Focus, m_FocusVel;
        float m_Zoom = 1f, m_ZoomTarget = 1f, m_ZoomVel;
        bool m_ShotInit;
        float m_LobbyFov;

        // Photo mode: a free orbit camera that takes over from the garage shot by its own weight. Leaving, it flies
        // back along the orbit to the garage camera before handing over, so it never cuts through the car.
        bool m_Photo, m_PhotoInit, m_PhotoLeaving;
        float m_PhotoWeight, m_PhotoFrom, m_PhotoTo, m_PhotoStart, m_PhotoDuration, m_PhotoLeaveAt;
        float m_PYaw, m_PPitch, m_PDistance, m_PFocal = 45f, m_PRoll;
        float m_PYawVel, m_PPitchVel, m_PDistanceVel, m_PFocalVel, m_PRollVel;
        Vector3 m_PPivot, m_PPivotVel;
        // Showroom interior (world space, inset from the walls) the photo camera stays inside.
        Bounds m_Room;
        bool m_HasRoom;

        // Lighting.
        class LightMod { public Light light; public float garageScale; public bool powered, wall; public float baseIntensity; }
        readonly List<LightMod> m_Lights = new List<LightMod>();
        readonly List<Light> m_GarageLights = new List<Light>();
        readonly List<float> m_GarageLightBase = new List<float>();
        readonly List<Color> m_GarageLightColor = new List<Color>();
        // Photo looks: garage light multipliers and colours, eased toward the active look.
        readonly float[] m_LookScale = { 1f, 1f, 1f };
        readonly Color[] m_LookColor = { Color.white, Color.white, Color.white };
        float m_LookBackdrop = 1f;
        class EmissiveMod { public Renderer renderer; public int property; public float scale; }
        readonly List<EmissiveMod> m_Emissives = new List<EmissiveMod>();
        class TextMod { public TMPro.TMP_Text text; public float alpha; }
        readonly List<TMPro.TMP_Text> m_Texts = new List<TMPro.TMP_Text>();
        readonly List<TextMod> m_StaticTexts = new List<TextMod>();
        // The brand wall sits above the garage framing, so it fades out fully rather than showing cropped letters.
        const float SignDim = 0f;
        MaterialPropertyBlock m_Block;
        bool m_Located;

        public GarageCarRig CarRig { get { Locate(); return m_CarRig; } }
        public Transform Car { get { Locate(); return m_Car; } }
        public Camera Camera { get { Locate(); return m_Camera; } }
        /// <summary>0 in the lobby, 1 in the garage (eased).</summary>
        public float Weight { get { return m_Weight; } }
        public bool InGarage { get { return m_Active; } }
        public bool Busy { get { return m_Active ? m_Weight < 0.999f : m_Weight > 0.001f; } }
        public bool Dragging { get { return m_Dragging; } }
        /// <summary>Car heading relative to the camera (0 front, 90 side, 180 rear), for the yaw readout.</summary>
        public float CurrentView { get; private set; }
        public float SpinSpeed { get { return m_YawVelocity; } }
        public GarageShot Shot { get { return m_Shot; } }
        /// <summary>0 = garage camera, 1 = photo camera (eased).</summary>
        public float PhotoWeight { get { return m_PhotoWeight; } }
        public bool InPhoto { get { return m_Photo; } }
        /// <summary>Distance from the photo camera to its target (metres), for auto focus.</summary>
        public float PhotoFocusDistance { get; private set; }
        /// <summary>The photo lens as it is now (mm), eased toward the setting.</summary>
        public float PhotoFocal { get { return m_PFocal; } }

        public static GarageStage Ensure()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GarageStage>();
            if (existing != null) { Instance = existing; existing.Locate(); return existing; }
            var go = new GameObject("GarageStage");
            var stage = go.AddComponent<GarageStage>();
            stage.Locate();
            return stage;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            m_Block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Locate()
        {
            if (m_Located) return;
            m_Located = true;
            m_Camera = Camera.main;
            if (m_Camera != null) m_LobbyFov = m_Camera.fieldOfView;
            var mount = FindAnyObjectByType<CarDisplayMount>();
            if (mount != null) m_Podium = mount.transform;
            if (m_Podium == null)
            {
                var go = GameObject.Find("DisplayPodium");
                if (go != null) m_Podium = go.transform;
            }
            var applier = FindAnyObjectByType<VehicleSetupApplier>();
            if (applier != null) m_Car = applier.transform;
            else if (mount != null && mount.carSlot != null && mount.carSlot.childCount > 0) m_Car = mount.carSlot.GetChild(0);
            if (m_Car != null) m_CarRig = GarageCarRig.Attach(m_Car.gameObject);
            if (m_Podium != null)
            {
                foreach (var t in m_Podium.GetComponentsInChildren<Transform>(true))
                    if (t.name == "DriverSpinner") { m_DriverSpinner = t; break; }
            }
            CollectLighting();
            FindRoom();
            GaragePhotoMode.Ensure(gameObject);
            m_Shot = HeroShot();
            if (m_Podium != null) m_Yaw = m_Podium.localEulerAngles.y;
        }

        // =====================================================================================================
        // ENTER / EXIT
        // =====================================================================================================
        public void Enter()
        {
            Locate();
            if (m_Camera == null || m_Podium == null) return;
            bool wasIdle = !m_Active && m_Weight <= 0.001f;
            m_Active = true;
            m_Returning = false;
            m_OnExited = null;
            StartWeight(1f, 1.65f);
            if (wasIdle)
            {
                m_LobbyFov = m_Camera.fieldOfView;
                m_Yaw = Mathf.DeltaAngle(0f, m_Podium.localEulerAngles.y);
                m_YawVelocity = 0f;
                m_SpinVelocity = 0f;
                m_Zoom = m_ZoomTarget = 1f;
                m_UserHeld = false;
                m_Shot = HeroShot();
                m_ShotInit = false;
                // Present the car: the platform spins up and lands on the hero three-quarter (nose to screen right, turning
                // the same way as the display spin), then the slow spin takes over from standstill.
                m_PresentFrom = m_Yaw;
                m_PresentTo = YawForView(-HeroView);
                if (m_PresentTo > m_PresentFrom - 25f) m_PresentTo -= 360f * Mathf.Ceil((m_PresentTo - m_PresentFrom + 25f) / 360f);
                // Sign text alpha as it is now (it is restored to this on exit).
                foreach (var t in m_StaticTexts) if (t.text != null) t.alpha = t.text.alpha;
                m_PresentStart = Time.unscaledTime + 0.15f;
                m_PresentDuration = Mathf.Max(0.01f, AccertoneMotion.Duration(2.1f));
                m_Presenting = true;
            }
            SetGarageLightsActive(true);
            StartLift(LiftDepth, 0.0f, 0.95f);
        }

        /// <summary>Returns everything to the lobby; <paramref name="onExited"/> runs once the camera has landed.</summary>
        public void Exit(Action onExited = null)
        {
            if (!m_Active && m_Weight <= 0.001f) { if (onExited != null) onExited(); return; }
            m_Active = false;
            m_Returning = true;
            m_Presenting = false;
            m_Dragging = false;
            m_OnExited = onExited;
            if (m_CarRig != null) { m_CarRig.SetDrsPreview(false); m_CarRig.SetBrakeHeat(0f, 0f); }
            if (m_Photo || m_PhotoWeight > 0f)
            {
                m_Photo = false;
                m_PhotoLeaving = false;
                StartPhotoWeight(0f, 0.6f);
            }
            StartWeight(0f, 1.25f);
            StartLift(0f, 0.45f, 0.85f);
        }

        void StartWeight(float to, float seconds)
        {
            m_WeightFrom = m_Weight;
            m_WeightTo = to;
            m_WeightStart = Time.unscaledTime;
            m_WeightDuration = AccertoneMotion.Duration(seconds) * Mathf.Abs(to - m_Weight);
        }

        void StartLift(float depth, float delay, float seconds)
        {
            if (m_DriverSpinner == null) return;
            if (!m_DriverRestKnown)
            {
                m_DriverRest = m_DriverSpinner.localPosition;
                m_DriverRestKnown = true;
            }
            if (depth > 0f && !m_DriverSpinner.gameObject.activeSelf) { m_Lift = depth; return; }
            if (depth <= 0f && !m_DriverSpinner.gameObject.activeSelf)
            {
                m_DriverSpinner.localPosition = m_DriverRest - Vector3.up * m_Lift;
                m_DriverSpinner.gameObject.SetActive(true);
            }
            m_LiftFrom = m_Lift;
            m_LiftTo = depth;
            m_LiftStart = Time.unscaledTime + AccertoneMotion.Duration(delay);
            m_LiftDuration = AccertoneMotion.Duration(seconds);
            m_LiftRunning = true;
        }

        // =====================================================================================================
        // SHOTS AND INPUT
        // =====================================================================================================
        public static GarageShot HeroShot()
        {
            return GarageShot.Make(35f, true, 9.9f, 1.55f, new Vector3(0f, 0.6f, 0f), 30f);
        }

        public static GarageShot ViewShot(GarageView view)
        {
            switch (view)
            {
                case GarageView.Front: return GarageShot.Make(0f, false, 8.2f, 1.05f, new Vector3(0f, 0.55f, 0.6f), 30f);
                case GarageView.Side: return GarageShot.Make(90f, false, 9.6f, 1.0f, new Vector3(0f, 0.55f, 0f), 30f);
                case GarageView.Rear: return GarageShot.Make(180f, false, 8.2f, 1.25f, new Vector3(0f, 0.62f, -0.6f), 30f);
                case GarageView.RearThreeQuarter: return GarageShot.Make(145f, false, 9.4f, 1.6f, new Vector3(0f, 0.62f, -0.2f), 30f);
                case GarageView.Top: return GarageShot.Make(62f, false, 3.6f, 8.6f, new Vector3(0f, 0.4f, 0f), 34f);
            }
            var hero = HeroShot();
            hero.spin = false;
            return hero;
        }

        /// <summary>Moves to a composition. A held shot turns the car to its view; a spinning shot keeps it turning.</summary>
        public void SetShot(GarageShot shot)
        {
            m_Shot = shot;
            m_UserHeld = false;
            if (!shot.spin) m_Presenting = false;
        }

        /// <summary>Horizontal framing: positive moves the car to the right of the screen (fraction of half the width).</summary>
        public void SetFraming(float frame)
        {
            m_FrameTarget = Mathf.Clamp(frame, -0.6f, 0.6f);
        }

        public void BeginDrag()
        {
            m_Dragging = true;
            m_Presenting = false;
            m_LastInput = Time.unscaledTime;
        }

        public void Drag(float pixels)
        {
            if (!m_Dragging) return;
            float delta = pixels * DragDegreesPerPixel;
            m_Yaw -= delta;
            float dt = Mathf.Max(1e-3f, Time.unscaledDeltaTime);
            m_YawVelocity = Mathf.Lerp(m_YawVelocity, -delta / dt, 0.5f);
            m_LastInput = Time.unscaledTime;
        }

        public void EndDrag()
        {
            m_Dragging = false;
            m_UserHeld = true;
            m_LastInput = Time.unscaledTime;
            m_YawVelocity = Mathf.Clamp(m_YawVelocity, -240f, 240f);
        }

        /// <summary>Continuous rotation from keys / sticks (-1..1).</summary>
        public void Rotate(float input)
        {
            m_InputRotate = Mathf.Clamp(input, -1f, 1f);
            if (Mathf.Abs(m_InputRotate) > 0.01f)
            {
                m_Presenting = false;
                m_UserHeld = true;
                m_LastInput = Time.unscaledTime;
            }
        }

        public void Zoom(float steps)
        {
            m_ZoomTarget = Mathf.Clamp(m_ZoomTarget * Mathf.Pow(0.92f, steps), 0.78f, 1.18f);
        }

        // =====================================================================================================
        // PHOTO MODE
        // =====================================================================================================
        /// <summary>Hands the camera to the photo orbit, starting exactly where the camera is now.</summary>
        public void BeginPhoto()
        {
            Locate();
            if (m_Camera == null) return;
            var s = GaragePhotoMode.Settings;
            bool fresh = !m_Photo && m_PhotoWeight <= 0.001f;
            m_Photo = true;
            m_PhotoLeaving = false;
            m_Presenting = false;
            StartPhotoWeight(1f, 0.6f);
            if (!fresh) return;
            Vector3 pivot = PhotoPivot(s);
            Vector3 off = m_Camera.transform.position - pivot;
            float d = Mathf.Max(0.01f, off.magnitude);
            s.distance = Mathf.Clamp(d, GaragePhotoSettings.MinDistance, GaragePhotoSettings.MaxDistance);
            s.yaw = Mathf.Repeat(Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg, 360f);
            s.pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(off.y / d, -1f, 1f)) * Mathf.Rad2Deg, GaragePhotoSettings.MinPitch, GaragePhotoSettings.MaxPitch);
            s.focal = Mathf.Clamp(Mathf.Round(GaragePhotoSettings.FocalFromFov(m_Camera.fieldOfView)), GaragePhotoSettings.MinFocal, GaragePhotoSettings.MaxFocal);
            s.roll = 0f;
            m_PhotoInit = false;
        }

        /// <summary>Flies the photo camera back to the garage shot, then hands over.</summary>
        public void EndPhoto()
        {
            if (!m_Photo) return;
            m_Photo = false;
            m_PhotoLeaving = true;
            m_PhotoLeaveAt = Time.unscaledTime;
        }

        /// <summary>Drag in reference pixels: right orbits the camera around the car, up looks from lower.</summary>
        public void PhotoOrbit(float dx, float dy)
        {
            var s = GaragePhotoMode.Settings;
            s.yaw = Mathf.Repeat(s.yaw + dx * 0.22f, 360f);
            s.pitch = Mathf.Clamp(s.pitch - dy * 0.16f, GaragePhotoSettings.MinPitch, GaragePhotoSettings.MaxPitch);
        }

        public void PhotoDolly(float steps)
        {
            var s = GaragePhotoMode.Settings;
            s.distance = Mathf.Clamp(s.distance * Mathf.Pow(0.93f, steps), GaragePhotoSettings.MinDistance, GaragePhotoSettings.MaxDistance);
        }

        /// <summary>Which side of the car the camera is on (car-local X sign).</summary>
        public float CameraSideOfCar()
        {
            Locate();
            if (m_Car == null || m_Camera == null) return -1f;
            return m_Car.InverseTransformPoint(m_Camera.transform.position).x >= 0f ? 1f : -1f;
        }

        void StartPhotoWeight(float to, float seconds)
        {
            m_PhotoFrom = m_PhotoWeight;
            m_PhotoTo = to;
            m_PhotoStart = Time.unscaledTime;
            m_PhotoDuration = AccertoneMotion.Duration(seconds) * Mathf.Abs(to - m_PhotoWeight);
        }

        void UpdatePhotoWeight()
        {
            float t = m_PhotoDuration <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - m_PhotoStart) / m_PhotoDuration);
            m_PhotoWeight = Mathf.Lerp(m_PhotoFrom, m_PhotoTo, t);
            if (m_PhotoWeight <= 0f && m_PhotoTo <= 0f) m_PhotoLeaving = false;
        }

        Vector3 PhotoPivot(GaragePhotoSettings s)
        {
            Vector3 local;
            switch (s.target)
            {
                case PhotoTarget.Nose:
                    local = m_CarRig != null ? m_CarRig.PartLocal(GaragePart.FrontWing) : new Vector3(0f, 0.3f, 2.4f);
                    local.x = 0f;
                    break;
                case PhotoTarget.Cockpit:
                    local = m_CarRig != null ? m_CarRig.PartLocal(GaragePart.Cockpit) : new Vector3(0f, 0.86f, 0.2f);
                    break;
                case PhotoTarget.RearWing:
                    local = m_CarRig != null ? m_CarRig.PartLocal(GaragePart.RearWing) : new Vector3(0f, 0.94f, -2.5f);
                    local.x = 0f;
                    break;
                case PhotoTarget.FrontWheel:
                    local = m_CarRig != null ? m_CarRig.PartLocal(GaragePart.Tyre) : new Vector3(0.95f, 0.38f, 1.5f);
                    local.x = Mathf.Abs(local.x) * (s.wheelSide >= 0f ? 1f : -1f);
                    break;
                default:
                    local = new Vector3(0f, 0.55f, m_CarRig != null ? m_CarRig.LocalBounds.center.z : 0f);
                    break;
            }
            Vector3 world = m_Car != null ? m_Car.TransformPoint(local) : new Vector3(0f, 0.6f, 0f);
            return world + Vector3.up * s.lift;
        }

        void UpdatePhotoCamera(float dt, Vector3 garagePos, float garageFov, out Vector3 pos, out Quaternion rot, out float fov)
        {
            var s = GaragePhotoMode.Settings;
            Vector3 pivot;
            float yaw, pitch, distance, focal, roll;
            if (m_PhotoLeaving)
            {
                // Aim the orbit at the garage camera; once it is there, fade the weight out.
                pivot = m_Focus;
                Vector3 off = garagePos - pivot;
                float d = Mathf.Max(0.01f, off.magnitude);
                yaw = Mathf.Atan2(off.x, off.z) * Mathf.Rad2Deg;
                pitch = Mathf.Asin(Mathf.Clamp(off.y / d, -1f, 1f)) * Mathf.Rad2Deg;
                distance = d;
                focal = GaragePhotoSettings.FocalFromFov(garageFov);
                roll = 0f;
            }
            else
            {
                pivot = PhotoPivot(s);
                yaw = s.yaw;
                pitch = s.pitch;
                distance = s.distance;
                focal = s.focal;
                roll = s.roll;
            }
            if (!m_PhotoInit)
            {
                m_PhotoInit = true;
                m_PYaw = yaw; m_PPitch = pitch; m_PDistance = distance; m_PFocal = focal; m_PRoll = roll; m_PPivot = pivot;
                m_PYawVel = m_PPitchVel = m_PDistanceVel = m_PFocalVel = m_PRollVel = 0f;
                m_PPivotVel = Vector3.zero;
            }
            float st = m_PhotoLeaving ? 0.32f : 0.16f;
            m_PYaw = Mathf.SmoothDampAngle(m_PYaw, yaw, ref m_PYawVel, st, Mathf.Infinity, dt);
            m_PPitch = Mathf.SmoothDamp(m_PPitch, pitch, ref m_PPitchVel, st, Mathf.Infinity, dt);
            m_PDistance = Mathf.SmoothDamp(m_PDistance, distance, ref m_PDistanceVel, st, Mathf.Infinity, dt);
            m_PFocal = Mathf.SmoothDamp(m_PFocal, focal, ref m_PFocalVel, 0.2f, Mathf.Infinity, dt);
            m_PRoll = Mathf.SmoothDamp(m_PRoll, roll, ref m_PRollVel, st, Mathf.Infinity, dt);
            m_PPivot = Vector3.SmoothDamp(m_PPivot, pivot, ref m_PPivotVel, 0.3f, Mathf.Infinity, dt);

            float yr = m_PYaw * Mathf.Deg2Rad, pr = m_PPitch * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), Mathf.Cos(yr) * Mathf.Cos(pr));
            pos = m_PPivot + dir * m_PDistance;
            pos = KeepInsideRoom(m_PPivot, pos);
            pos = KeepOutsideCar(pos);
            rot = Quaternion.LookRotation(m_PPivot - pos, Vector3.up) * Quaternion.Euler(0f, 0f, m_PRoll);
            fov = GaragePhotoSettings.FovFromFocal(m_PFocal);
            PhotoFocusDistance = Vector3.Distance(pos, m_PPivot);

            bool arrived = Mathf.Abs(Mathf.DeltaAngle(m_PYaw, yaw)) < 2f && Mathf.Abs(m_PPitch - pitch) < 1.5f &&
                           Mathf.Abs(m_PDistance - distance) < 0.12f && (m_PPivot - pivot).sqrMagnitude < 0.01f;
            if (m_PhotoLeaving && m_PhotoTo > 0f && (arrived || Time.unscaledTime - m_PhotoLeaveAt > 1.8f))
                StartPhotoWeight(0f, 0.45f);
        }

        void FindRoom()
        {
            var showroom = GameObject.Find("ACCERTONE_Showroom");
            if (showroom == null) return;
            var left = showroom.transform.Find("SideWall_L");
            var right = showroom.transform.Find("SideWall_R");
            if (left == null || right == null) return;
            Bounds a, b;
            if (!RendererBounds(left, out a) || !RendererBounds(right, out b)) return;
            if (a.center.x > b.center.x) { var t = a; a = b; b = t; }
            float minZ = Mathf.Min(a.min.z, b.min.z), maxZ = Mathf.Max(a.max.z, b.max.z), maxY = Mathf.Max(a.max.y, b.max.y);
            Bounds w;
            var back = showroom.transform.Find("RearWall");
            if (back != null && RendererBounds(back, out w)) maxZ = w.min.z;
            var ceiling = showroom.transform.Find("Ceiling");
            if (ceiling != null && RendererBounds(ceiling, out w)) maxY = w.min.y;
            // Inset so the near clip plane never shows the back of a wall.
            m_Room.SetMinMax(new Vector3(a.max.x + 0.45f, -100f, minZ + 0.45f), new Vector3(b.min.x - 0.45f, maxY - 0.35f, maxZ - 0.45f));
            m_HasRoom = m_Room.size.x > 1f && m_Room.size.z > 1f;
        }

        static bool RendererBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return true;
        }

        /// <summary>Pulls the camera in along its line to the target when the orbit would leave the showroom.</summary>
        Vector3 KeepInsideRoom(Vector3 pivot, Vector3 pos)
        {
            if (!m_HasRoom) return pos;
            Vector3 d = pos - pivot;
            float len = d.magnitude;
            if (len < 1e-4f) return pos;
            Vector3 dir = d / len;
            float t = len;
            for (int axis = 0; axis < 3; axis++)
            {
                float p = pivot[axis], v = dir[axis];
                if (v > 1e-5f) t = Mathf.Min(t, (m_Room.max[axis] - p) / v);
                else if (v < -1e-5f) t = Mathf.Min(t, (m_Room.min[axis] - p) / v);
            }
            return pivot + dir * Mathf.Max(0.5f, t);
        }

        Vector3 KeepOutsideCar(Vector3 pos)
        {
            if (m_Car == null) return pos;
            // An ellipse around the body (plus a margin) the camera may not enter, and a floor it may not go under.
            Bounds b = m_CarRig != null ? m_CarRig.LocalBounds : new Bounds(Vector3.zero, new Vector3(2f, 1.2f, 5.6f));
            Vector3 local = m_Car.InverseTransformPoint(pos);
            float ax = b.extents.x + 0.35f, az = b.extents.z + 0.35f;
            if (local.y < b.max.y + 0.3f)
            {
                float ex = (local.x - b.center.x) / ax, ez = (local.z - b.center.z) / az;
                float r = Mathf.Sqrt(ex * ex + ez * ez);
                if (r < 1f)
                {
                    if (r < 1e-3f) { ex = 1f; ez = 0f; r = 1f; }
                    local.x = b.center.x + ex / r * ax;
                    local.z = b.center.z + ez / r * az;
                }
            }
            local.y = Mathf.Max(local.y, 0.12f);
            return m_Car.TransformPoint(local);
        }

        float YawForView(float view)
        {
            // Car heading (world yaw) = podium yaw + whatever sits under the podium (turntable spinner, spawn anchor).
            float baseYaw = m_Car != null && m_Podium != null ? m_Car.eulerAngles.y - m_Podium.eulerAngles.y : 204f;
            float target = CameraBearing + view - baseYaw;
            return m_Yaw + Mathf.DeltaAngle(m_Yaw, target);
        }

        // =====================================================================================================
        // FRAME
        // =====================================================================================================
        void Update()
        {
            if (!m_Located) return;
            UpdateLift();
        }

        void LateUpdate()
        {
            if (!m_Located || m_Camera == null || m_Podium == null) return;
            float dt = Time.unscaledDeltaTime;
            UpdateWeight();
            UpdatePhotoWeight();
            if (m_Weight <= 0f && !m_Active)
            {
                FinishExitIfDone();
                return;
            }

            UpdateTurntable(dt);
            UpdateCamera(dt);
            ApplyLighting();
            FinishExitIfDone();
        }

        void UpdateWeight()
        {
            float t = m_WeightDuration <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - m_WeightStart) / m_WeightDuration);
            m_Weight = Mathf.Lerp(m_WeightFrom, m_WeightTo, t);
        }

        void FinishExitIfDone()
        {
            if (m_Active || !m_Returning || m_Weight > 0f) return;
            m_Returning = false;
            m_Podium.localRotation = Quaternion.identity;
            m_Yaw = 0f;
            if (m_Camera != null) m_Camera.fieldOfView = m_LobbyFov;
            SetGarageLightsActive(false);
            var done = m_OnExited;
            m_OnExited = null;
            if (done != null) done();
        }

        void UpdateLift()
        {
            if (!m_LiftRunning || m_DriverSpinner == null) return;
            float t = m_LiftDuration <= 0f ? 1f : (Time.unscaledTime - m_LiftStart) / m_LiftDuration;
            if (t < 0f) return;
            t = Mathf.Clamp01(t);
            m_Lift = Mathf.Lerp(m_LiftFrom, m_LiftTo, AccertoneEase.InOutCubic(t));
            m_DriverSpinner.localPosition = m_DriverRest - Vector3.up * m_Lift;
            if (t >= 1f)
            {
                m_LiftRunning = false;
                if (m_LiftTo > 0f) m_DriverSpinner.gameObject.SetActive(false);
                else m_DriverSpinner.localPosition = m_DriverRest;
            }
        }

        void UpdateTurntable(float dt)
        {
            if (m_Returning)
            {
                // Back to the lobby's rest angle along the shortest way, timed with the camera.
                float target = Mathf.Round(m_Yaw / 360f) * 360f;
                float k = 1f - Mathf.Clamp01(m_Weight);
                float st = Mathf.Lerp(0.35f, 0.06f, k);
                m_Yaw = Mathf.SmoothDamp(m_Yaw, target, ref m_YawVelocity, st, Mathf.Infinity, dt);
                if (m_Weight <= 0.02f) m_Yaw = target;
            }
            else if (m_Photo)
            {
                // Photo Mode: the platform holds still (coasting to a stop) or keeps its display spin.
                m_Presenting = false;
                if (GaragePhotoMode.Settings.spin) m_YawVelocity = Mathf.Lerp(m_YawVelocity, AutoSpinSpeed, 1f - Mathf.Exp(-0.9f * dt));
                else m_YawVelocity *= Mathf.Exp(-2.4f * dt);
                m_Yaw += m_YawVelocity * dt;
            }
            else if (m_Presenting)
            {
                float t = (Time.unscaledTime - m_PresentStart) / m_PresentDuration;
                if (t >= 0f)
                {
                    float e = OutQuart(Mathf.Clamp01(t));
                    float prev = m_Yaw;
                    m_Yaw = Mathf.Lerp(m_PresentFrom, m_PresentTo, e);
                    m_YawVelocity = (m_Yaw - prev) / Mathf.Max(1e-4f, dt);
                    if (t >= 1f) { m_Presenting = false; m_YawVelocity = 0f; m_SpinVelocity = 0f; }
                }
            }
            else if (m_Dragging)
            {
                // Drag writes the yaw directly; velocity was measured in Drag().
            }
            else if (Mathf.Abs(m_InputRotate) > 0.01f)
            {
                m_YawVelocity = Mathf.Lerp(m_YawVelocity, -m_InputRotate * 95f, 1f - Mathf.Exp(-10f * dt));
                m_Yaw += m_YawVelocity * dt;
                m_SpinVelocity = m_YawVelocity;
            }
            else
            {
                bool idle = Time.unscaledTime - m_LastInput > ResumeDelay;
                if (m_Shot.spin && (idle || !m_UserHeld))
                {
                    // Coast down from a throw, then ease up to the display speed.
                    m_UserHeld = false;
                    m_YawVelocity = Mathf.Lerp(m_YawVelocity, AutoSpinSpeed, 1f - Mathf.Exp(-0.9f * dt));
                    m_Yaw += m_YawVelocity * dt;
                }
                else if (!m_Shot.spin && !m_UserHeld)
                {
                    float target = YawForView(m_Shot.view);
                    m_Yaw = Mathf.SmoothDamp(m_Yaw, target, ref m_YawVelocity, 0.55f, 600f, dt);
                }
                else
                {
                    // Inertia after a throw.
                    m_YawVelocity *= Mathf.Exp(-3.2f * dt);
                    m_Yaw += m_YawVelocity * dt;
                }
            }
            if (Mathf.Abs(m_Yaw) > 7200f) m_Yaw = Mathf.Repeat(m_Yaw, 360f);
            m_Podium.localRotation = Quaternion.Euler(0f, m_Yaw, 0f);
            if (m_Car != null) CurrentView = Mathf.Repeat(m_Car.eulerAngles.y - CameraBearing, 360f);
        }

        static float OutQuart(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u * u;
        }

        void UpdateCamera(float dt)
        {
            // Pose the lobby rig left this frame (its LateUpdate ran first).
            Vector3 rigPos = m_Camera.transform.position;
            Quaternion rigRot = m_Camera.transform.rotation;

            float aspect = Mathf.Max(0.5f, m_Camera.aspect);
            float fit = Mathf.Clamp(Mathf.Pow((16f / 9f) / aspect, 0.85f), 0.85f, 1.6f);
            Vector3 focusWorld = m_Car != null ? m_Car.TransformPoint(m_Shot.focus) : new Vector3(0f, 0.8f, 0f);
            float targetDistance = m_Shot.distance * fit * m_ZoomTarget;
            if (!m_ShotInit)
            {
                m_ShotInit = true;
                m_Distance = targetDistance;
                m_Height = m_Shot.height;
                m_Fov = m_Shot.fov;
                m_Focus = focusWorld;
                m_Frame = m_FrameTarget;
                m_DistanceVel = m_HeightVel = m_FovVel = m_FrameVel = 0f;
                m_FocusVel = Vector3.zero;
            }
            const float st = 0.62f;
            m_Distance = Mathf.SmoothDamp(m_Distance, targetDistance, ref m_DistanceVel, st, Mathf.Infinity, dt);
            m_Height = Mathf.SmoothDamp(m_Height, m_Shot.height, ref m_HeightVel, st, Mathf.Infinity, dt);
            m_Fov = Mathf.SmoothDamp(m_Fov, m_Shot.fov, ref m_FovVel, st, Mathf.Infinity, dt);
            m_Frame = Mathf.SmoothDamp(m_Frame, m_FrameTarget, ref m_FrameVel, 0.5f, Mathf.Infinity, dt);
            m_Focus = Vector3.SmoothDamp(m_Focus, focusWorld, ref m_FocusVel, 0.5f, Mathf.Infinity, dt);

            float bearing = CameraBearing * Mathf.Deg2Rad;
            Vector3 flat = new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing));
            Vector3 pos = new Vector3(m_Focus.x, 0f, m_Focus.z) + flat * m_Distance;
            pos.y = Mathf.Max(0.25f, m_Height);

            // Barely-there handheld drift (honours Settings > Camera > Showroom camera motion); none in Photo Mode.
            float pw = SmoothWeight(m_PhotoWeight);
            if (AccertoneMotion.CameraMotion)
            {
                float n = Time.unscaledTime * 0.09f;
                pos += new Vector3(Mathf.PerlinNoise(n, 2.1f) - 0.5f, Mathf.PerlinNoise(4.3f, n) - 0.5f, 0f) * (0.06f * (1f - pw));
            }

            Quaternion look = Quaternion.LookRotation(m_Focus - pos, Vector3.up);
            // Framing: aim off the car so it sits in the free part of the screen.
            float vfov = Mathf.Lerp(m_LobbyFov, m_Fov, m_Weight);
            float hHalf = Mathf.Atan(Mathf.Tan(vfov * 0.5f * Mathf.Deg2Rad) * aspect);
            float yawOffset = Mathf.Atan(Mathf.Tan(hHalf) * m_Frame) * Mathf.Rad2Deg;
            Quaternion garageRot = Quaternion.AngleAxis(-yawOffset, Vector3.up) * look;

            float fov = m_Fov;
            if (m_PhotoWeight > 0f)
            {
                Vector3 photoPos;
                Quaternion photoRot;
                float photoFov;
                UpdatePhotoCamera(dt, pos, m_Fov, out photoPos, out photoRot, out photoFov);
                pos = Vector3.Lerp(pos, photoPos, pw);
                garageRot = Quaternion.Slerp(garageRot, photoRot, pw);
                fov = Mathf.Lerp(fov, photoFov, pw);
            }

            float w = SmoothWeight(m_Weight);
            m_Camera.transform.position = Vector3.Lerp(rigPos, pos, w);
            m_Camera.transform.rotation = Quaternion.Slerp(rigRot, garageRot, w);
            m_Camera.fieldOfView = Mathf.Lerp(m_LobbyFov, fov, w);
        }

        static float SmoothWeight(float w)
        {
            // Ease in-out (cubic): leaves promptly, lands without a jolt.
            w = Mathf.Clamp01(w);
            return w < 0.5f ? 4f * w * w * w : 1f - Mathf.Pow(-2f * w + 2f, 3f) * 0.5f;
        }

        /// <summary>Screen position (pixels) of a car part, and whether it is in front of the camera.</summary>
        public bool PartScreenPoint(GaragePart part, out Vector2 screen)
        {
            screen = Vector2.zero;
            if (m_CarRig == null || m_Camera == null) return false;
            Vector3 world = m_CarRig.PartPoint(part, m_Camera.transform.position);
            Vector3 sp = m_Camera.WorldToScreenPoint(world);
            screen = sp;
            return sp.z > 0f;
        }

        // =====================================================================================================
        // LIGHTING
        // =====================================================================================================
        void CollectLighting()
        {
            m_Lights.Clear();
            m_Emissives.Clear();
            var showroom = GameObject.Find("ACCERTONE_Showroom");
            if (showroom == null) return;
            foreach (var light in showroom.GetComponentsInChildren<Light>(true))
            {
                if (m_Podium != null && light.transform.IsChildOf(m_Podium)) continue; // driver lights leave with him
                float scale = 1f;
                string n = light.name;
                if (n == "Fill_Front") scale = 1.6f;
                else if (n == "Key_Overhead") scale = 1.12f;
                else if (n.StartsWith("WallWash_Top")) scale = 0.4f;
                else if (n.StartsWith("WallWash_")) scale = 0.3f;
                if (Mathf.Approximately(scale, 1f)) continue;
                m_Lights.Add(new LightMod { light = light, garageScale = scale, powered = light.GetComponent<ShowroomPoweredElement>() != null, wall = n.StartsWith("WallWash"), baseIntensity = light.intensity });
            }
            foreach (var e in showroom.GetComponentsInChildren<ShowroomPoweredElement>(true))
            {
                var r = e.GetComponent<Renderer>();
                if (r == null || string.IsNullOrEmpty(e.colorProperty)) continue;
                float scale = -1f;
                if (e.name == "Podium_LEDRing") scale = 1.3f;
                else if (e.name == "AccentFloorGlow") scale = 1.15f;
                else if (e.name.StartsWith("LED_Run") || e.name.StartsWith("Ceiling_LightBox")) scale = 0.75f;
                // The brand wall steps back into the dark so the light belongs to the car.
                else if (IsWallSign(e.transform)) scale = SignDim;
                if (scale < 0f) continue;
                m_Emissives.Add(new EmissiveMod { renderer = r, property = Shader.PropertyToID(e.colorProperty), scale = scale });
            }
            m_Texts.Clear();
            foreach (var e in showroom.GetComponentsInChildren<ShowroomPoweredElement>(true))
            {
                var text = e.GetComponent<TMPro.TMP_Text>();
                if (text != null && IsWallSign(e.transform)) m_Texts.Add(text);
            }
            var sign = showroom.transform.Find("ACCERTONE_WallSign");
            if (sign != null)
            {
                // Sign letters that are not powered elements (static text / meshes) dim through their own graphics.
                foreach (var text in sign.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (!m_Texts.Contains(text) && text.GetComponent<ShowroomPoweredElement>() == null) m_StaticTexts.Add(new TextMod { text = text, alpha = text.alpha });
            }
        }

        static bool IsWallSign(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == "ACCERTONE_WallSign") return true;
            return false;
        }

        void SetGarageLightsActive(bool on)
        {
            if (on && m_GarageLights.Count == 0)
            {
                // Softboxes: long front-left key and cooler front-right fill for paint highlights; violet kicker behind.
                AddGarageLight("Garage_Softbox_Left", new Vector3(-4.6f, 5.0f, -4.4f), new Vector3(0f, 0.55f, 0f), new Color(1f, 0.97f, 0.93f), 30f, 52f, 16f);
                AddGarageLight("Garage_Softbox_Right", new Vector3(5.0f, 4.4f, -3.2f), new Vector3(0f, 0.6f, 0f), new Color(0.88f, 0.93f, 1f), 17f, 50f, 15f);
                AddGarageLight("Garage_Kicker", new Vector3(0f, 0.95f, 6.2f), new Vector3(0f, 0.5f, 0f), new Color(0.62f, 0.34f, 1f), 24f, 64f, 13f);
            }
            foreach (var l in m_GarageLights) if (l != null) l.enabled = on || m_Weight > 0f;
            if (!on)
            {
                foreach (var l in m_GarageLights) if (l != null) l.enabled = false;
                foreach (var m in m_Lights) if (m.light != null && !m.powered) m.light.intensity = m.baseIntensity;
                foreach (var t in m_StaticTexts) if (t.text != null) t.text.alpha = t.alpha;
            }
        }

        void AddGarageLight(string name, Vector3 position, Vector3 aim, Color color, float intensity, float angle, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(aim - position, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = 0f;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.45f;
            l.range = range;
            l.shadows = LightShadows.None;
            l.enabled = false;
            m_GarageLights.Add(l);
            m_GarageLightBase.Add(intensity);
            m_GarageLightColor.Add(color);
            m_LookColor[Mathf.Min(m_GarageLights.Count - 1, 2)] = color;
        }

        void ApplyLighting()
        {
            float w = SmoothWeight(m_Weight);
            float pw = SmoothWeight(m_PhotoWeight);
            if (pw > 0f)
            {
                // Photo looks ease the softboxes, kicker and back wall toward the chosen look.
                var look = GaragePhotoLooks.Get(GaragePhotoMode.Settings.look);
                float lk = GarageTheme.Approach(4f);
                m_LookScale[0] = Mathf.Lerp(m_LookScale[0], look.key, lk);
                m_LookScale[1] = Mathf.Lerp(m_LookScale[1], look.fill, lk);
                m_LookScale[2] = Mathf.Lerp(m_LookScale[2], look.kick, lk);
                m_LookColor[0] = Color.Lerp(m_LookColor[0], look.keyColor, lk);
                m_LookColor[1] = Color.Lerp(m_LookColor[1], look.fillColor, lk);
                m_LookColor[2] = Color.Lerp(m_LookColor[2], look.kickColor, lk);
                m_LookBackdrop = Mathf.Lerp(m_LookBackdrop, look.backdrop, lk);
            }
            foreach (var m in m_Lights)
            {
                if (m.light == null) continue;
                float k = Mathf.Lerp(1f, m.garageScale, w);
                if (m.wall) k *= Mathf.Lerp(1f, m_LookBackdrop, pw);
                // Powered lights are re-levelled every Update by the showroom power-up, so scale what it wrote.
                m.light.intensity = (m.powered ? m.light.intensity : m.baseIntensity) * k;
            }
            for (int i = 0; i < m_GarageLights.Count; i++)
            {
                var l = m_GarageLights[i];
                if (l == null) continue;
                l.enabled = w > 0.001f;
                int li = Mathf.Min(i, 2);
                l.intensity = m_GarageLightBase[i] * w * Mathf.Lerp(1f, m_LookScale[li], pw);
                l.color = Color.Lerp(m_GarageLightColor[i], m_LookColor[li], pw);
            }
            // Powered sign text is re-levelled every Update; static sign text is scaled from its own alpha.
            float signK = Mathf.Lerp(1f, SignDim, w);
            foreach (var t in m_Texts) if (t != null) t.alpha *= signK;
            foreach (var t in m_StaticTexts) if (t.text != null) t.text.alpha = t.alpha * signK;
            foreach (var e in m_Emissives)
            {
                if (e.renderer == null) continue;
                e.renderer.GetPropertyBlock(m_Block);
                if (!m_Block.HasVector(e.property)) continue;
                Vector4 v = m_Block.GetVector(e.property);
                float k = Mathf.Lerp(1f, e.scale, w);
                m_Block.SetVector(e.property, new Vector4(v.x * k, v.y * k, v.z * k, v.w));
                e.renderer.SetPropertyBlock(m_Block);
            }
        }
    }
}
