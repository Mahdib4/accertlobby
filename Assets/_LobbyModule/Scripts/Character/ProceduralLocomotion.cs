using System;
using UnityEngine;

namespace Accertone.LobbyModule
{
    /// <summary>
    /// Walk cycle for the lobby character, generated from the character's real displacement so the
    /// animation can never run faster or slower than the movement:
    ///
    ///  * Gait phase advances with distance travelled (step length scales with speed), legs alternate.
    ///  * A planted foot is locked to the floor until it lifts; the trailing foot peels heel-first and pivots
    ///    on its toe (toe-off), so the toe contact never slides and the hips don't have to crouch.
    ///  * Swinging feet arc to where the hip will be when they land, turning with the body (feet follow turns).
    ///  * Two-bone IK bends the knees; only planted feet constrain the pelvis height.
    ///  * Pelvis bob / sway / counter-rotation, left-arm counter swing (right arm carries the helmet).
    ///  * Turning on the spot: head and upper body lead the turn, the hips follow, and the feet step around
    ///    it (each step lands where the body will be facing), so the driver never spins on locked feet.
    ///  * Choreographed steps for presentations (<see cref="StepTo"/>, <see cref="Pivot"/>, <see cref="ShiftWeight"/>):
    ///    a few deliberate steps along an arc around the body, planted feet swivelling on the ball of the foot,
    ///    and the pelvis moving over the supporting foot before the other one lifts.
    ///  * Idle: breathing, slow weight shift, small corrective steps, head looks toward the attention target.
    ///  * Hold pose (<see cref="holdPose"/>): for a display platform. The character stands completely still in its
    ///    stance, feet locked to its own frame so they ride the platform; no steps, no weight shift, no head turn.
    ///
    /// Bones come from <c>LobbyCharacterRigBuilder</c> (the static driver meshes have no skeleton of their own).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class ProceduralLocomotion : MonoBehaviour
    {
        [Serializable]
        public class Bones
        {
            public Transform hips, spine, chest, neck, head;
            public Transform upperLegL, lowerLegL, footL;
            public Transform upperLegR, lowerLegR, footR;
            public Transform upperArmL, lowerArmL, handL;
        }

        public Bones bones = new Bones();
        public LobbyCharacterController controller;
        public LobbyWalkArea walkArea;

        [Header("Gait")]
        [Tooltip("Step length at a standstill + per m/s of speed (metres).")]
        public float baseStep = 0.28f;
        public float stepPerSpeed = 0.28f;
        public float maxStep = 0.68f;
        [Range(0.25f, 0.5f)] public float swingFraction = 0.4f;
        [Tooltip("How far ahead of the hip the foot lands, as a fraction of the step.")]
        [Range(0.2f, 0.6f)] public float landingLead = 0.46f;
        public float stepHeight = 0.075f;
        [Tooltip("Max ankle lift of the trailing foot before toe-off (the toe stays planted). Physically limited to sqrt(ankle^2 + footLength^2) - ankle.")]
        public float maxHeelRise = 0.085f;
        [Tooltip("Ankle to toe-contact distance (metres).")]
        public float footLength = 0.15f;
        public float settleDistance = 0.11f;
        public float settleAngle = 26f;
        public float settleDuration = 0.32f;

        [Header("Body")]
        public float walkBend = 0.015f;
        public float pelvisBob = 0.012f;
        public float pelvisSway = 0.02f;
        public float pelvisRoll = 3f;
        public float pelvisYaw = 5f;
        public float forwardLean = 3f;
        [Tooltip("Most the pelvis may drop below its standing height to keep planted feet reachable (beyond this the foot steps instead).")]
        public float maxPelvisDrop = 0.22f;
        public float armSwing = 15f;
        public float breathing = 0.8f;
        public float idleWeightShift = 0.014f;
        [Tooltip("Standing: the pelvis rises until the supporting leg is extended to this fraction of its length "
                 + "(0.9995 = knee about 3 degrees flexed: straight but relaxed). The mesh's bind pose is about 8 degrees.")]
        public float standingExtension = 0.9995f;
        [Tooltip("Stand with the feet under the hip joints (legs vertical) instead of the mesh's own foot placement.")]
        public bool feetUnderHips = true;
        [Tooltip("Stance width beyond hip width (m): the natural gap between the feet.")]
        public float stanceExtraWidth = 0f;
        [Tooltip("Natural toe-out of each foot in the stance (degrees).")]
        public float stanceToeOut = 4f;

        [Header("Display hold")]
        [Tooltip("Stand completely still for a display platform: the feet stay planted in the character's own frame "
                 + "(they ride a rotating turntable with it), no steps, no weight shift, no head turn; only a faint breath.")]
        public bool holdPose;

        [Header("Turning in place")]
        [Tooltip("Upper body leads a turn by this many seconds of the current turn rate (spine + chest twist).")]
        public float turnLead = 0.2f;
        public float maxTurnLead = 22f;

        [Header("Choreographed steps")]
        [Tooltip("Ball of the foot, measured from the ankle: the point a planted foot swivels on.")]
        public float ballDistance = 0.11f;
        [Tooltip("At full weight transfer the pelvis moves this fraction of the way toward the supporting foot.")]
        public float weightShiftAmount = 0.4f;
        [Tooltip("Pelvis roll at full weight transfer (the unweighted hip drops slightly).")]
        public float weightShiftRoll = 2.5f;
        [Tooltip("A turning step swings this much wider around the standing leg at mid-swing (m).")]
        public float swingArcBulge = 0.03f;

        [Header("Look")]
        public float headYawLimit = 55f;
        public float lookSharpness = 3f;

        class Leg
        {
            public Transform upper, lower, foot;
            public float side;
            public float upperLen, lowerLen, ankleHeight;
            public Vector3 hipLocal;      // hip joint in root space
            public Vector3 restFoot;      // ankle ground point in root space
            public Quaternion upperAimInv, lowerAimInv, upperRest, lowerRest, footRest;
            public Vector3 planted;       // ankle ground point while planted
            public float plantedYaw;
            public bool swinging;
            public float t, duration, elapsed;
            public float startPhase, endPhase; // gait phase window of the current swing (settle steps: time only)
            public Vector3 from;
            public float fromYaw, fromPitch, fromRise;
            public int lastCycle = int.MinValue;
            public Vector3 current;
            public Vector3 landing;       // where a swinging foot will land (updated every frame)
            public float currentYaw;
            public float pitch;           // + = heel up / toe down
            public float heelRise;        // ankle lift from a heel-off pivot on the toe
            public Vector3 lastToe;
            public bool wasPlanted;
            public float horiz;           // hip to ankle, horizontal (last IK solve)
            // choreographed step / swivel
            public bool manual;
            public Vector3 manualTarget, arcCentre;
            public float manualYaw, manualLift;
            public bool pivoting, contactWasBall;
            public Vector3 pivotBall;
            public float pivotFrom, pivotTo, pivotT, pivotDuration;
        }

        /// <summary>Largest per-frame horizontal movement of a planted foot's toe contact since the last reset (metres). Should stay ~0.</summary>
        public float PlantedSlideMax { get; private set; }
        /// <summary>Footsteps taken since the last reset.</summary>
        public int StepCount { get; private set; }
        public string PlantedSlideContext { get; private set; }
        public void ResetDiagnostics() { PlantedSlideMax = 0f; StepCount = 0; PlantedSlideContext = ""; }

        Leg m_L, m_R;
        bool m_Ready;
        Vector3 m_HipsRestLocal;
        Quaternion m_HipsRest, m_SpineRest, m_ChestRest, m_NeckRest, m_HeadRest, m_ArmRest, m_ForeArmRest;
        float m_Phase;
        Vector3 m_LastPos;
        float m_HipsYOffset, m_HipsYVel;
        float m_MoveFactor;
        float m_HeadYaw, m_HeadPitch;
        float m_LastSettle;
        float m_LastLower;
        bool m_Standing = true;
        float m_PrevYaw, m_YawRate, m_TurnLead;
        bool m_Choreographed;
        float m_StandLift;          // right hip raised (left lowered) by this much so both legs stand straight
        float m_ReachFactor = 0.998f;
        float m_Weight, m_WeightFrom, m_WeightTo, m_WeightT = 1f, m_WeightDuration = 1f;

        // ------------------------------------------------------------------ choreographed steps (presentations)

        /// <summary>Hand the feet to a script: no automatic corrective steps until <see cref="EndChoreography"/>.</summary>
        public void BeginChoreography() { m_Choreographed = true; }
        public void EndChoreography() { m_Choreographed = false; }

        public bool IsSwinging(bool left) { return m_Ready && (left ? m_L : m_R).swinging; }
        public bool IsPivoting(bool left) { return m_Ready && (left ? m_L : m_R).pivoting; }
        public Vector3 FootPoint(bool left) { return (left ? m_L : m_R).planted; }
        public float FootYaw(bool left) { return (left ? m_L : m_R).plantedYaw; }

        /// <summary>The foot's facing in the rest stance (body yaw plus a natural toe-out).</summary>
        public float StanceYaw(bool left, float bodyYaw) { return bodyYaw + (left ? -stanceToeOut : stanceToeOut); }
        public Vector3 FootCurrent(bool left) { return (left ? m_L : m_R).current; }

        /// <summary>Where a foot stands in the rest stance when the body faces <paramref name="bodyYaw"/>.</summary>
        public Vector3 StancePoint(bool left, float bodyYaw)
        {
            var leg = left ? m_L : m_R;
            Vector3 p = transform.position + Quaternion.Euler(0f, bodyYaw, 0f) * leg.restFoot;
            p.y = Ground(p);
            return p;
        }

        /// <summary>One deliberate step: the foot lifts, travels along an arc around the body and lands flat at
        /// <paramref name="groundPoint"/> facing <paramref name="yaw"/>.</summary>
        public void StepTo(bool left, Vector3 groundPoint, float yaw, float duration, float lift)
        {
            var leg = left ? m_L : m_R;
            leg.pivoting = false;
            StartSwing(leg, Mathf.Max(0.1f, duration));
            leg.manual = true;
            groundPoint.y = Ground(groundPoint);
            leg.manualTarget = leg.landing = groundPoint;
            leg.manualYaw = yaw;
            leg.manualLift = lift;
            leg.arcCentre = Flat(transform.position);
        }

        /// <summary>A planted foot swivels on the ball of the foot (the heel swings) to face <paramref name="yaw"/>.</summary>
        public void Pivot(bool left, float yaw, float duration)
        {
            var leg = left ? m_L : m_R;
            if (leg.swinging) return;
            leg.pivotBall = leg.planted + Quaternion.Euler(0f, leg.plantedYaw, 0f) * Vector3.forward * ballDistance;
            leg.pivotFrom = leg.plantedYaw;
            leg.pivotTo = leg.plantedYaw + Mathf.DeltaAngle(leg.plantedYaw, yaw);
            leg.pivotT = 0f;
            leg.pivotDuration = Mathf.Max(0.05f, duration);
            leg.pivoting = true;
        }

        /// <summary>Move the body's weight: -1 = over the left foot, 0 = centred, +1 = over the right foot.</summary>
        public void ShiftWeight(float weight, float duration)
        {
            m_WeightFrom = m_Weight;
            m_WeightTo = Mathf.Clamp(weight, -1f, 1f);
            m_WeightT = 0f;
            m_WeightDuration = Mathf.Max(0.01f, duration);
        }

        /// <summary>Both feet planted and squared under the body.</summary>
        public bool IsSettled
        {
            get
            {
                if (!m_Ready) return true;
                float yaw = transform.eulerAngles.y;
                return !m_L.swinging && !m_R.swinging && Mathf.Max(SettleError(m_L, yaw), SettleError(m_R, yaw)) <= 1f;
            }
        }

        /// <summary>Plant both feet in the rest stance under the current body pose (use after placing / turning the
        /// character instantly, before it is shown).</summary>
        public void SnapFeetToStance()
        {
            if (!m_Ready) return;
            float yaw = transform.eulerAngles.y;
            foreach (var leg in new[] { m_L, m_R })
            {
                leg.planted = leg.current = RestPoint(leg);
                leg.plantedYaw = leg.currentYaw = yaw + leg.side * stanceToeOut;
                leg.swinging = false;
                leg.heelRise = leg.pitch = 0f;
                leg.wasPlanted = false;
            }
            m_LastPos = transform.position;
            m_PrevYaw = yaw;
            m_YawRate = m_TurnLead = 0f;
        }

        void Awake()
        {
            if (controller == null) controller = GetComponent<LobbyCharacterController>();
            if (walkArea == null && controller != null) walkArea = controller.walkArea;
            m_Ready = bones.hips != null && bones.upperLegL != null && bones.upperLegR != null;
            if (!m_Ready) return;

            var root = transform;
            Quaternion inv = Quaternion.Inverse(root.rotation);
            m_HipsRestLocal = root.InverseTransformPoint(bones.hips.position);
            m_HipsRest = inv * bones.hips.rotation;
            m_SpineRest = bones.spine != null ? bones.spine.localRotation : Quaternion.identity;
            m_ChestRest = bones.chest != null ? bones.chest.localRotation : Quaternion.identity;
            m_NeckRest = bones.neck != null ? bones.neck.localRotation : Quaternion.identity;
            m_HeadRest = bones.head != null ? bones.head.localRotation : Quaternion.identity;
            m_ArmRest = bones.upperArmL != null ? bones.upperArmL.localRotation : Quaternion.identity;
            m_ForeArmRest = bones.lowerArmL != null ? bones.lowerArmL.localRotation : Quaternion.identity;

            m_L = MakeLeg(bones.upperLegL, bones.lowerLegL, bones.footL, -1f);
            m_R = MakeLeg(bones.upperLegR, bones.lowerLegR, bones.footR, 1f);
            if (feetUnderHips)
            {
                // Legs vertical: each foot under its hip joint, the stance a little wider than the hips.
                float cx = (m_L.hipLocal.x + m_R.hipLocal.x) * 0.5f;
                float half = Mathf.Abs(m_R.hipLocal.x - m_L.hipLocal.x) * 0.5f + stanceExtraWidth * 0.5f;
                m_L.restFoot = new Vector3(cx - half, 0f, m_L.hipLocal.z);
                m_R.restFoot = new Vector3(cx + half, 0f, m_R.hipLocal.z);
            }
            foreach (var leg in new[] { m_L, m_R })
            {
                leg.planted = leg.current = RestPoint(leg);
                leg.plantedYaw = leg.currentYaw = root.eulerAngles.y + leg.side * stanceToeOut;
            }
            m_LastPos = root.position;
            m_PrevYaw = root.eulerAngles.y;
        }

        Leg MakeLeg(Transform upper, Transform lower, Transform foot, float side)
        {
            var root = transform;
            Quaternion inv = Quaternion.Inverse(root.rotation);
            var leg = new Leg { upper = upper, lower = lower, foot = foot, side = side };
            Vector3 hip = root.InverseTransformPoint(upper.position);
            Vector3 knee = root.InverseTransformPoint(lower.position);
            Vector3 ankle = root.InverseTransformPoint(foot.position);
            leg.hipLocal = hip;
            leg.upperLen = Vector3.Distance(hip, knee);
            leg.lowerLen = Vector3.Distance(knee, ankle);
            leg.ankleHeight = ankle.y;
            leg.restFoot = new Vector3(ankle.x, 0f, ankle.z);
            leg.upperRest = inv * upper.rotation;
            leg.lowerRest = inv * lower.rotation;
            leg.footRest = inv * foot.rotation;
            leg.upperAimInv = Quaternion.Inverse(Aim(knee - hip, Vector3.forward));
            leg.lowerAimInv = Quaternion.Inverse(Aim(ankle - knee, Vector3.forward));
            Vector3 w = root.TransformPoint(leg.restFoot);
            w.y = Ground(w);
            leg.planted = leg.current = w;
            leg.plantedYaw = leg.currentYaw = root.eulerAngles.y;
            return leg;
        }

        static Quaternion Aim(Vector3 down, Vector3 forwardHint)
        {
            Vector3 up = -down.normalized;
            Vector3 fwd = Vector3.ProjectOnPlane(forwardHint, up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
            return Quaternion.LookRotation(fwd.normalized, up);
        }

        float Ground(Vector3 p)
        {
            // Holding a pose, the character stands on whatever it is placed on (e.g. its own display spinner).
            if (holdPose) return transform.position.y;
            return walkArea != null ? walkArea.GroundHeight(p) : transform.position.y;
        }

        float StepLength(float speed)
        {
            return Mathf.Min(maxStep, baseStep + stepPerSpeed * speed);
        }

        Vector3 RestPoint(Leg leg, float yawAhead = 0f)
        {
            Vector3 p = transform.position + Quaternion.Euler(0f, yawAhead, 0f) * (transform.TransformPoint(leg.restFoot) - transform.position);
            p.y = Ground(p);
            return p;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void LateUpdate()
        {
            if (!m_Ready) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            var root = transform;

            Vector3 delta = root.position - m_LastPos;
            delta.y = 0f;
            m_LastPos = root.position;
            // Holding a pose on a moving platform: the platform's motion is not the character's own.
            Vector3 vel = holdPose ? Vector3.zero : controller != null ? controller.Velocity : delta / dt;
            vel.y = 0f;
            float speed = vel.magnitude;
            Vector3 dir = speed > 1e-3f ? vel / speed : root.forward;
            m_MoveFactor = Mathf.MoveTowards(m_MoveFactor, Mathf.Clamp01(speed / 0.6f), dt * 3f);

            float S = StepLength(speed);
            float cycle = 2f * S;
            m_Phase += delta.magnitude / cycle;
            float rootYaw = root.eulerAngles.y;
            float yawStep = holdPose ? 0f : Mathf.DeltaAngle(m_PrevYaw, rootYaw) / dt;
            m_PrevYaw = rootYaw;
            m_YawRate = Mathf.Lerp(m_YawRate, yawStep, 1f - Mathf.Exp(-8f * dt));
            m_TurnLead = Mathf.Clamp(m_YawRate * turnLead, -maxTurnLead, maxTurnLead) * (1f - m_MoveFactor);

            // --- gait restart: after standing (feet re-settled under the body) a new walk starts a fresh
            // stride, the trailing foot stepping first, instead of finishing a half-elapsed old cycle.
            if (speed <= 0.06f) m_Standing = true;
            else if (m_Standing)
            {
                m_Standing = false;
                float lagL = Vector3.Dot(Flat(root.position) - Flat(m_L.planted), dir);
                float lagR = Vector3.Dot(Flat(root.position) - Flat(m_R.planted), dir);
                int k = Mathf.FloorToInt(m_Phase) + 1;
                if (lagR > lagL) { m_Phase = k + 0.5f; m_R.lastCycle = k - 1; m_L.lastCycle = k; }
                else { m_Phase = k; m_L.lastCycle = k - 1; m_R.lastCycle = k - 1; }
            }

            // --- step triggering ---------------------------------------------------------------
            if (speed > 0.06f)
            {
                TryPhaseStep(m_L, m_R, 0f, cycle, speed);
                TryPhaseStep(m_R, m_L, 0.5f, cycle, speed);
            }
            else if (!holdPose && !m_Choreographed && !m_L.swinging && !m_R.swinging && Time.time - m_LastSettle > 0.12f)
            {
                float eL = SettleError(m_L, rootYaw), eR = SettleError(m_R, rootYaw);
                Leg leg = eL >= eR ? m_L : m_R;
                if (Mathf.Max(eL, eR) > 1f) StartSwing(leg, settleDuration);
            }

            // --- swing / stance -------------------------------------------------------------------
            if (holdPose)
            {
                // Feet locked in the stance, in the character's own frame (riding the platform with it).
                foreach (var leg in new[] { m_L, m_R })
                {
                    leg.swinging = leg.manual = leg.pivoting = false;
                    leg.planted = RestPoint(leg);
                    leg.plantedYaw = rootYaw + leg.side * stanceToeOut;
                    leg.heelRise = leg.pitch = 0f;
                }
                m_Weight = m_WeightFrom = m_WeightTo = 0f;
            }
            UpdateLeg(m_L, dt, speed, dir, S, rootYaw);
            UpdateLeg(m_R, dt, speed, dir, S, rootYaw);

            // --- pelvis ---------------------------------------------------------------------------
            float ph = m_Phase * Mathf.PI * 2f;
            float idleShift = holdPose ? 0f : Mathf.Sin(Time.time * 0.23f * Mathf.PI * 2f) * idleWeightShift * (1f - m_MoveFactor);
            float sway = -Mathf.Sin(ph) * pelvisSway * m_MoveFactor + idleShift;
            float bob = Mathf.Cos(ph * 2f) * pelvisBob * m_MoveFactor;
            float bend = walkBend * m_MoveFactor;

            Vector3 hipsPos = root.TransformPoint(m_HipsRestLocal + new Vector3(sway, 0f, 0f));
            m_WeightT = Mathf.Min(1f, m_WeightT + dt / m_WeightDuration);
            m_Weight = Mathf.Lerp(m_WeightFrom, m_WeightTo, m_WeightT * m_WeightT * (3f - 2f * m_WeightT));
            if (Mathf.Abs(m_Weight) > 1e-4f)
            {
                Vector3 mid = (Flat(m_L.current) + Flat(m_R.current)) * 0.5f;
                Vector3 support = Flat(m_Weight > 0f ? m_R.current : m_L.current);
                hipsPos += (support - mid) * (Mathf.Abs(m_Weight) * weightShiftAmount);
            }
            float groundUnder = root.position.y;
            float desiredY = groundUnder + m_HipsRestLocal.y - bend + bob;
            float tall = 1f - m_MoveFactor;
            m_ReachFactor = Mathf.Lerp(0.998f, Mathf.Max(0.998f, standingExtension + 0.0001f), tall);
            float liftTarget = 0f;
            if (tall > 0.01f)
            {
                // Standing tall: both legs almost straight. The driver meshes stand contrapposto (one knee more bent
                // than the other), so the pelvis rises to the average and rolls a little to even the legs out. With
                // the weight on one foot that leg sets the height and the unweighted leg relaxes (heel may lift).
                float yl = m_L.swinging ? float.MaxValue : HipsYForExtension(m_L, hipsPos, standingExtension);
                float yr = m_R.swinging ? float.MaxValue : HipsYForExtension(m_R, hipsPos, standingExtension);
                float level;
                if (yl < float.MaxValue && yr < float.MaxValue)
                {
                    float k = Mathf.InverseLerp(0.3f, 0.9f, Mathf.Abs(m_Weight));
                    level = Mathf.Lerp((yl + yr) * 0.5f, m_Weight > 0f ? yr : yl, k);
                    liftTarget = Mathf.Clamp((yr - yl) * 0.5f, -0.012f, 0.012f) * (1f - k) * tall;
                }
                else level = Mathf.Min(yl, yr); // one foot in the air: the standing leg sets the height
                if (level < float.MaxValue)
                    desiredY = Mathf.Lerp(desiredY, Mathf.Min(level, groundUnder + m_HipsRestLocal.y + 0.02f), tall);
            }
            m_StandLift = Mathf.MoveTowards(m_StandLift, liftTarget, dt * 0.04f);
            // Planted feet constrain the pelvis (trailing feet may peel their heel to keep reaching); a swinging
            // foot's landing spot is anticipated in late swing so the pelvis is already low enough at contact.
            desiredY = Mathf.Min(desiredY, MaxHipsY(m_L, hipsPos, dir, speed, true), MaxHipsY(m_R, hipsPos, dir, speed, true));
            float offset = Mathf.Max(desiredY - (groundUnder + m_HipsRestLocal.y), -maxPelvisDrop);
            m_HipsYOffset = Mathf.SmoothDamp(m_HipsYOffset, offset, ref m_HipsYVel, offset < m_HipsYOffset ? 0.03f : 0.08f, Mathf.Infinity, dt);
            hipsPos.y = groundUnder + m_HipsRestLocal.y + m_HipsYOffset;

            float yawOsc = Mathf.Sin(ph) * pelvisYaw * m_MoveFactor;
            float hipSpacing = Mathf.Max(0.05f, Mathf.Abs(m_R.hipLocal.x - m_L.hipLocal.x));
            float standRoll = Mathf.Asin(Mathf.Clamp(2f * m_StandLift / hipSpacing, -0.2f, 0.2f)) * Mathf.Rad2Deg;
            float rollOsc = Mathf.Sin(ph) * pelvisRoll * m_MoveFactor + m_Weight * weightShiftRoll + standRoll;
            float lean = forwardLean * m_MoveFactor;
            bones.hips.SetPositionAndRotation(hipsPos, root.rotation * Quaternion.Euler(lean, yawOsc, rollOsc) * m_HipsRest);

            // Hard reach clamp, measured on the real (rotated) hip joints: a planted foot can never be dragged by the IK.
            float lower = Mathf.Max(Overreach(m_L, speed, dir), Overreach(m_R, speed, dir));
            lower = Mathf.Min(lower, Mathf.Max(0f, maxPelvisDrop + m_HipsYOffset));
            if (lower > 0f) bones.hips.position -= Vector3.up * lower;
            m_LastLower = lower;

            float breath = Mathf.Sin(Time.time * 0.24f * Mathf.PI * 2f) * breathing;
            if (bones.spine != null) bones.spine.localRotation = m_SpineRest * Quaternion.Euler(-lean * 0.3f, -yawOsc * 0.6f + m_TurnLead * 0.4f, -rollOsc * 0.6f);
            if (bones.chest != null) bones.chest.localRotation = m_ChestRest * Quaternion.Euler(-breath * 0.6f, -yawOsc * 0.7f + m_TurnLead * 0.6f, -rollOsc * 0.3f);
            UpdateLook(dt);

            // --- heel-off on planted trailing feet, then leg IK -------------------------------------
            UpdateHeel(m_L, dt, speed);
            UpdateHeel(m_R, dt, speed);
            SolveLeg(m_L);
            SolveLeg(m_R);
            Measure(m_L);
            Measure(m_R);

            // --- left arm counter-swing (right arm carries the helmet) -----------------------------
            if (bones.upperArmL != null)
            {
                float swing = Mathf.Sin(ph) * armSwing * m_MoveFactor;
                bones.upperArmL.localRotation = m_ArmRest * Quaternion.Euler(swing, 0f, -2.5f - breath * 0.4f);
                if (bones.lowerArmL != null)
                    bones.lowerArmL.localRotation = m_ForeArmRest * Quaternion.Euler(-(6f + Mathf.Max(0f, -swing) * 0.6f) * Mathf.Max(0.3f, m_MoveFactor), 0f, 0f);
            }
        }

        float SettleError(Leg leg, float rootYaw)
        {
            float d = Vector3.Distance(Flat(leg.planted), Flat(RestPoint(leg))) / settleDistance;
            float a = Mathf.Abs(Mathf.DeltaAngle(leg.plantedYaw, rootYaw + leg.side * stanceToeOut)) / settleAngle;
            return Mathf.Max(d, a);
        }

        void TryPhaseStep(Leg leg, Leg other, float offset, float cycle, float speed)
        {
            float local = m_Phase - offset;
            int cycleIndex = Mathf.FloorToInt(local);
            float frac = local - cycleIndex;
            if (leg.swinging || (other.swinging && other.t < 0.85f)) return;
            // A planted foot about to run out of leg must step now, whatever the phase says.
            bool overstretched = leg.horiz > Reach(leg) * 0.93f;
            if (cycleIndex == leg.lastCycle && !overstretched) return;
            // On time: the step window opens at frac 0. Late (the other foot was still in the air when the window
            // opened, e.g. after a speed change): step immediately and catch up instead of skipping the cycle,
            // which would leave the foot planted for a whole extra stride.
            int stepCycle = cycleIndex;
            if (cycleIndex == leg.lastCycle) stepCycle = cycleIndex + 1;          // overstretched: borrow next cycle's step
            else if (frac >= 0.6f) stepCycle = cycleIndex + 1;                    // very late: counts as the next step
            leg.lastCycle = stepCycle;
            float duration = Mathf.Clamp((swingFraction * cycle) / Mathf.Max(0.2f, speed), 0.24f, 0.5f);
            // The swing must be down by the end of its phase window (so the other leg's window is never blocked),
            // but never shorter than a fifth of a cycle.
            float endPhase = Mathf.Max(offset + stepCycle + swingFraction, m_Phase + 0.2f);
            StartSwing(leg, duration, endPhase);
        }

        void StartSwing(Leg leg, float duration, float endPhase = float.PositiveInfinity)
        {
            leg.swinging = true;
            leg.manual = false;
            leg.pivoting = false;
            leg.t = 0f;
            leg.elapsed = 0f;
            leg.duration = duration;
            leg.startPhase = m_Phase;
            leg.endPhase = endPhase;
            leg.from = leg.planted;
            leg.fromYaw = leg.plantedYaw;
            leg.fromPitch = leg.pitch;
            leg.fromRise = leg.heelRise;
            m_LastSettle = Time.time;
            StepCount++;
        }

        void UpdateLeg(Leg leg, float dt, float speed, Vector3 dir, float S, float rootYaw)
        {
            if (!leg.swinging)
            {
                if (leg.pivoting)
                {
                    leg.pivotT = Mathf.Min(1f, leg.pivotT + dt / leg.pivotDuration);
                    float ep = leg.pivotT * leg.pivotT * (3f - 2f * leg.pivotT);
                    float yaw = Mathf.Lerp(leg.pivotFrom, leg.pivotTo, ep);
                    Vector3 ankle = leg.pivotBall - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * ballDistance;
                    ankle.y = leg.planted.y;
                    leg.planted = ankle;
                    leg.plantedYaw = yaw;
                    if (leg.pivotT >= 1f) leg.pivoting = false;
                }
                leg.current = leg.planted;
                leg.currentYaw = leg.plantedYaw;
                return;
            }

            if (leg.manual)
            {
                leg.elapsed += dt;
                leg.t = Mathf.Min(1f, leg.elapsed / leg.duration);
                float em = leg.t * leg.t * (3f - 2f * leg.t);
                // Along an arc around the body (a turning step swings around, never through, the other foot).
                Vector3 a = Flat(leg.from) - leg.arcCentre, b = Flat(leg.manualTarget) - leg.arcCentre;
                Vector3 pm;
                if (a.sqrMagnitude > 1e-4f && b.sqrMagnitude > 1e-4f)
                    pm = leg.arcCentre + Quaternion.Euler(0f, Vector3.SignedAngle(a, b, Vector3.up) * em, 0f) * a.normalized
                         * (Mathf.Lerp(a.magnitude, b.magnitude, em) + Mathf.Sin(leg.t * Mathf.PI) * swingArcBulge);
                else
                    pm = Vector3.Lerp(Flat(leg.from), Flat(leg.manualTarget), em);
                pm.y = Mathf.Lerp(leg.from.y, leg.manualTarget.y, em) + Mathf.Sin(leg.t * Mathf.PI) * leg.manualLift;
                leg.current = pm;
                leg.landing = leg.manualTarget;
                leg.currentYaw = Mathf.LerpAngle(leg.fromYaw, leg.manualYaw, em);
                float flat = Mathf.Clamp01((leg.t - 0.75f) / 0.25f);
                leg.pitch = Mathf.Lerp(leg.fromPitch, -6f, Mathf.Clamp01(leg.t / 0.5f)) * (1f - flat * flat * (3f - 2f * flat));
                leg.heelRise = Mathf.Lerp(leg.fromRise, 0f, em);
                if (leg.t >= 1f)
                {
                    leg.swinging = false;
                    leg.manual = false;
                    leg.planted = leg.manualTarget;
                    leg.plantedYaw = leg.manualYaw;
                    leg.heelRise = 0f;
                    leg.pitch = 0f;
                }
                return;
            }

            // Progress by time, but never behind the gait phase (speed changes mid-swing can't make it late).
            leg.elapsed += dt;
            float tTime = leg.elapsed / leg.duration;
            float tPhase = float.IsInfinity(leg.endPhase) ? 0f : (m_Phase - leg.startPhase) / Mathf.Max(1e-4f, leg.endPhase - leg.startPhase);
            leg.t = Mathf.Min(1f, Mathf.Max(leg.t, tTime, tPhase));
            float cycle = 2f * S;
            float remaining = (1f - leg.t) * leg.duration * speed;
            if (!float.IsInfinity(leg.endPhase)) remaining = Mathf.Min(remaining, Mathf.Max(0f, leg.endPhase - m_Phase) * cycle);
            float lead = speed > 0.06f ? landingLead * S : 0f;
            // Turning on the spot: land where the body will be facing when the foot touches down.
            float ahead = speed > 0.06f ? 0f : Mathf.Clamp(m_YawRate * (1f - leg.t) * leg.duration, -30f, 30f);
            Vector3 target = RestPoint(leg, ahead) + dir * (remaining + lead);
            target.y = Ground(target);
            leg.landing = target;

            float e = leg.t * leg.t * (3f - 2f * leg.t);
            Vector3 p = Vector3.Lerp(leg.from, target, e);
            float rise = Mathf.Abs(target.y - leg.from.y);
            p.y = Mathf.Lerp(leg.from.y, target.y, e) + Mathf.Sin(leg.t * Mathf.PI) * (stepHeight * Mathf.Lerp(0.55f, 1f, m_MoveFactor) + rise * 0.9f);
            leg.current = p;
            leg.currentYaw = Mathf.LerpAngle(leg.fromYaw, rootYaw + ahead + leg.side * stanceToeOut, e);
            // Toe-off pitch unwinds through the swing, the toe lifts in late swing, and the foot lands flat
            // (so the contact never rolls under a planted foot).
            float u = Mathf.Clamp01(leg.t / 0.7f);
            float landing = Mathf.Clamp01((leg.t - 0.8f) / 0.2f);
            leg.pitch = Mathf.Lerp(leg.fromPitch, -9f * m_MoveFactor, u * u * (3f - 2f * u)) * (1f - landing * landing * (3f - 2f * landing));
            leg.heelRise = Mathf.Lerp(leg.fromRise, 0f, e);

            if (leg.t >= 1f)
            {
                leg.swinging = false;
                leg.planted = target;
                leg.plantedYaw = rootYaw + leg.side * stanceToeOut;
                leg.heelRise = 0f;
                leg.pitch = 0f;
            }
        }

        Vector3 HipJoint(Leg leg, Vector3 hipsPos, bool withStandLift = true)
        {
            Vector3 p = hipsPos + transform.rotation * (leg.hipLocal - m_HipsRestLocal);
            if (withStandLift) p.y += leg.side * m_StandLift;
            return p;
        }

        float Reach(Leg leg) { return (leg.upperLen + leg.lowerLen) * m_ReachFactor; }

        /// <summary>Weight is clearly on the other foot (presentation weight shifts): this leg may relax, heel up.</summary>
        bool Unweighted(Leg leg) { return leg == m_L ? m_Weight > 0.3f : m_Weight < -0.3f; }

        /// <summary>Pelvis height at which this planted leg is extended to <paramref name="extension"/> of its length.</summary>
        float HipsYForExtension(Leg leg, Vector3 hipsPos, float extension)
        {
            Vector3 hip = HipJoint(leg, hipsPos, false);
            Vector3 ankle = leg.current + Vector3.up * leg.ankleHeight;
            float reach = (leg.upperLen + leg.lowerLen) * extension;
            float horizontal = new Vector2(hip.x - ankle.x, hip.z - ankle.z).magnitude;
            return ankle.y + Mathf.Sqrt(Mathf.Max(0.0001f, reach * reach - horizontal * horizontal)) - (hip.y - hipsPos.y);
        }

        /// <summary>Largest ankle lift a toe pivot can produce for this leg (keeps a margin below the geometric max).</summary>
        float HeelRiseLimit(Leg leg)
        {
            float h = leg.ankleHeight;
            float geometric = Mathf.Sqrt(h * h + footLength * footLength) - h;
            return Mathf.Min(maxHeelRise, geometric * 0.92f);
        }

        float MaxHipsY(Leg leg, Vector3 hipsPos, Vector3 dir, float speed, bool anticipate)
        {
            float weight = 1f;
            Vector3 foot = leg.current;
            if (leg.swinging)
            {
                if (!anticipate) return float.MaxValue;
                weight = Mathf.Clamp01((leg.t - 0.5f) / 0.5f);
                weight = weight * weight * (3f - 2f * weight);
                if (weight <= 0f) return float.MaxValue;
                foot = leg.landing;
            }
            Vector3 hip = HipJoint(leg, hipsPos);
            Vector3 hipOffset = hip - hipsPos;
            float lag = Vector3.Dot(Flat(hip) - Flat(foot), dir);
            float allowance = !leg.swinging && ((speed > 0.06f && lag > 0.04f) || Unweighted(leg)) ? HeelRiseLimit(leg) : 0f;
            Vector3 ankle = foot + Vector3.up * (leg.ankleHeight + allowance);
            float reach = Reach(leg);
            float horizontal = new Vector2(hip.x - ankle.x, hip.z - ankle.z).magnitude;
            float vertical = Mathf.Sqrt(Mathf.Max(0.0001f, reach * reach - horizontal * horizontal));
            float allowed = ankle.y + vertical - hipOffset.y;
            return weight >= 1f ? allowed : allowed + (1f - weight) * 0.3f;
        }

        /// <summary>How far the pelvis must drop so this planted leg can reach its foot (with the toe pivot at its limit).</summary>
        float Overreach(Leg leg, float speed, Vector3 dir)
        {
            if (leg.swinging) return 0f;
            Vector3 hip = leg.upper.position;
            float lag = Vector3.Dot(Flat(hip) - Flat(leg.current), dir);
            Vector3 ankle = leg.current + Vector3.up * leg.ankleHeight;
            if ((speed > 0.06f && lag > 0.04f) || Unweighted(leg))
            {
                float th = PivotAngleForRise(HeelRiseLimit(leg), leg.ankleHeight);
                Vector3 fwd = Quaternion.Euler(0f, leg.currentYaw, 0f) * Vector3.forward;
                float h = leg.ankleHeight, L = footLength;
                ankle = leg.current + Vector3.up * (h * Mathf.Cos(th) + L * Mathf.Sin(th)) + fwd * (L * (1f - Mathf.Cos(th)) + h * Mathf.Sin(th));
            }
            float reach = Reach(leg);
            float horizontal = new Vector2(hip.x - ankle.x, hip.z - ankle.z).magnitude;
            float allowedY = ankle.y + Mathf.Sqrt(Mathf.Max(0f, reach * reach - horizontal * horizontal));
            return hip.y - allowedY;
        }

        void UpdateHeel(Leg leg, float dt, float speed)
        {
            if (leg.swinging) return;
            float targetRise = 0f;
            if (speed > 0.06f || Unweighted(leg))
            {
                Vector3 hip = leg.upper.position;
                float reach = Reach(leg);
                float horizontal = new Vector2(hip.x - leg.current.x, hip.z - leg.current.z).magnitude;
                float neededAnkleY = hip.y - Mathf.Sqrt(Mathf.Max(0f, reach * reach - horizontal * horizontal));
                targetRise = Mathf.Clamp(neededAnkleY - (leg.current.y + leg.ankleHeight), 0f, HeelRiseLimit(leg));
            }
            // Rise instantly when the leg needs it (so the planted toe never gets dragged), settle smoothly.
            if (targetRise >= leg.heelRise) leg.heelRise = targetRise;
            else leg.heelRise = Mathf.Lerp(leg.heelRise, targetRise, 1f - Mathf.Exp(-12f * dt));
            leg.pitch = PivotAngleForRise(leg.heelRise, leg.ankleHeight) * Mathf.Rad2Deg;
        }

        /// <summary>Foot pitch (radians) about the toe contact that lifts the ankle by <paramref name="rise"/>.</summary>
        float PivotAngleForRise(float rise, float h)
        {
            if (rise <= 0f) return 0f;
            float L = footLength;
            // rise(th) = h(cos th - 1) + L sin th is monotonic on [0, atan(L/h)]: bisect for robustness.
            float lo = 0f, hi = Mathf.Atan2(L, h);
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * 0.5f;
                float r = h * (Mathf.Cos(mid) - 1f) + L * Mathf.Sin(mid);
                if (r < rise) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        void SolveLeg(Leg leg)
        {
            Vector3 hip = leg.upper.position;
            Quaternion footYaw = Quaternion.Euler(0f, leg.currentYaw, 0f);
            Vector3 fwd = footYaw * Vector3.forward;
            Vector3 ankle;
            if (leg.swinging)
                ankle = leg.current + Vector3.up * (leg.ankleHeight + leg.heelRise);
            else
            {
                // Exact rotation about the toe contact: the toe stays fixed while the heel peels up.
                float pr = Mathf.Max(0f, leg.pitch) * Mathf.Deg2Rad;
                float h = leg.ankleHeight, L = footLength;
                ankle = leg.current
                        + Vector3.up * (h * Mathf.Cos(pr) + L * Mathf.Sin(pr))
                        + fwd * (L * (1f - Mathf.Cos(pr)) + h * Mathf.Sin(pr));
            }

            float a = leg.upperLen, b = leg.lowerLen;
            Vector3 toT = ankle - hip;
            leg.horiz = new Vector2(toT.x, toT.z).magnitude;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, (a + b) * 0.9998f);
            Vector3 dir = toT.normalized;
            Vector3 bendDir = Vector3.ProjectOnPlane(fwd, dir);
            if (bendDir.sqrMagnitude < 1e-6f) bendDir = Vector3.ProjectOnPlane(transform.forward, dir);
            bendDir.Normalize();
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 knee = hip + dir * (a * cosA) + bendDir * (a * sinA);
            Vector3 end = hip + dir * d;

            leg.upper.rotation = Aim(knee - hip, fwd) * leg.upperAimInv * leg.upperRest;
            leg.lower.rotation = Aim(end - knee, fwd) * leg.lowerAimInv * leg.lowerRest;
            leg.foot.rotation = footYaw * Quaternion.Euler(leg.pitch, 0f, 0f) * leg.footRest;
        }

        void Measure(Leg leg)
        {
            // Toe contact of the rendered foot after IK. Planted feet are either flat or pivoting on this toe,
            // so it must not move horizontally while planted.
            bool ball = leg.pivoting;
            Vector3 toe = leg.foot.position + leg.foot.rotation * new Vector3(0f, -leg.ankleHeight, ball ? ballDistance : footLength);
            if (holdPose) toe = transform.InverseTransformPoint(toe);
            if (!leg.swinging && leg.wasPlanted && ball == leg.contactWasBall)
            {
                float slide = new Vector2(toe.x - leg.lastToe.x, toe.z - leg.lastToe.z).magnitude;
                if (slide > PlantedSlideMax)
                {
                    PlantedSlideMax = slide;
                    float reachNow = Vector3.Distance(leg.upper.position, leg.foot.position) / (leg.upperLen + leg.lowerLen);
                    PlantedSlideContext = string.Format("side={0} pitch={1:F1} rise={2:F3} speed={3:F2} legStretch={4:F3} pelvisDrop={5:F3} footDy={6:F3} at={7}",
                        leg.side < 0 ? "L" : "R", leg.pitch, leg.heelRise, controller != null ? controller.Speed : 0f, reachNow, m_LastLower - m_HipsYOffset,
                        Mathf.Abs(m_L.current.y - m_R.current.y), transform.position.ToString("F2"));
                }
            }
            leg.lastToe = toe;
            leg.wasPlanted = !leg.swinging;
            leg.contactWasBall = ball;
        }

        void UpdateLook(float dt)
        {
            if (bones.head == null) return;
            var root = transform;
            float targetYaw = 0f, targetPitch = 0f;
            var attention = holdPose ? null : controller != null ? controller.attentionTarget : null;
            if (attention != null && m_MoveFactor < 0.5f)
            {
                Vector3 to = attention.position - bones.head.position;
                Vector3 local = Quaternion.Inverse(root.rotation) * to;
                // the spine / chest already carry the turn lead
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg - m_TurnLead, -headYawLimit, headYawLimit);
                targetPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -20f, 25f);
                float w = 1f - m_MoveFactor * 2f;
                targetYaw *= w;
                targetPitch *= w;
            }
            float k = 1f - Mathf.Exp(-lookSharpness * dt);
            m_HeadYaw = Mathf.Lerp(m_HeadYaw, targetYaw, k);
            m_HeadPitch = Mathf.Lerp(m_HeadPitch, targetPitch, k);
            if (bones.neck != null) bones.neck.localRotation = m_NeckRest * Quaternion.Euler(m_HeadPitch * 0.4f, m_HeadYaw * 0.4f, 0f);
            bones.head.localRotation = m_HeadRest * Quaternion.Euler(m_HeadPitch * 0.6f, m_HeadYaw * 0.6f, 0f);
        }
    }
}
