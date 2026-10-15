using UnityEngine;

namespace Accertone.CarSetup
{
    /// <summary>
    /// Vehicle physics integration component. Receives F1CarSetupData and applies
    /// real physical properties to Rigidbody, WheelColliders, and vehicle sub-systems.
    /// Also simulates aerodynamic downforce and drag during gameplay.
    /// </summary>
    public class VehicleSetupApplier : MonoBehaviour, IVehicleSetupReceiver
    {
        [Header("Physics Bindings")]
        [SerializeField] private Rigidbody m_Rigidbody;
        [SerializeField] private WheelCollider[] m_FrontWheels;
        [SerializeField] private WheelCollider[] m_RearWheels;

        [Header("Runtime State")]
        [SerializeField] private F1CarSetupData m_ActiveSetup;
        [SerializeField] private bool m_ApplyAeroForces = true;

        public F1CarSetupData ActiveSetup => m_ActiveSetup;

        private void Awake()
        {
            if (m_Rigidbody == null)
            {
                m_Rigidbody = GetComponentInParent<Rigidbody>();
            }

            // Auto-locate wheels if not set
            if ((m_FrontWheels == null || m_FrontWheels.Length == 0) &&
                (m_RearWheels == null || m_RearWheels.Length == 0))
            {
                var allWheels = GetComponentsInChildren<WheelCollider>();
                if (allWheels.Length >= 4)
                {
                    m_FrontWheels = new WheelCollider[] { allWheels[0], allWheels[1] };
                    m_RearWheels = new WheelCollider[] { allWheels[2], allWheels[3] };
                }
            }
        }

        private void Start()
        {
            if (CarSetupPresetManager.Instance != null && CarSetupPresetManager.Instance.ActiveSetup != null)
            {
                ApplyCarSetup(CarSetupPresetManager.Instance.ActiveSetup);
            }
        }

        public void ApplyCarSetup(F1CarSetupData setup)
        {
            if (setup == null) return;
            m_ActiveSetup = setup.Clone();
            m_ActiveSetup.ValidateAndClamp();

            // 1. Apply suspension springs & dampers to WheelColliders if present
            if (m_FrontWheels != null)
            {
                foreach (var wheel in m_FrontWheels)
                {
                    if (wheel == null) continue;
                    var spring = wheel.suspensionSpring;
                    // F1 spring rates: range 1-41 maps to 70,000 N/m to 140,000 N/m
                    spring.spring = 70000f + (m_ActiveSetup.frontSuspension * 1750f);
                    spring.damper = spring.spring * 0.12f;
                    spring.targetPosition = 0.5f;
                    wheel.suspensionSpring = spring;
                    wheel.suspensionDistance = Mathf.Clamp(m_ActiveSetup.frontRideHeight * 0.001f, 0.03f, 0.10f);
                }
            }

            if (m_RearWheels != null)
            {
                foreach (var wheel in m_RearWheels)
                {
                    if (wheel == null) continue;
                    var spring = wheel.suspensionSpring;
                    spring.spring = 65000f + (m_ActiveSetup.rearSuspension * 1650f);
                    spring.damper = spring.spring * 0.12f;
                    spring.targetPosition = 0.5f;
                    wheel.suspensionSpring = spring;
                    wheel.suspensionDistance = Mathf.Clamp(m_ActiveSetup.rearRideHeight * 0.001f, 0.03f, 0.10f);
                }
            }

            // 2. Broadcast to other IVehicleSetupReceiver components on car
            var receivers = GetComponentsInChildren<IVehicleSetupReceiver>();
            foreach (var receiver in receivers)
            {
                if (receiver != (IVehicleSetupReceiver)this)
                {
                    receiver.ApplyCarSetup(m_ActiveSetup);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!m_ApplyAeroForces || m_Rigidbody == null || m_ActiveSetup == null) return;

            // Speed in m/s
            float speed = m_Rigidbody.linearVelocity.magnitude;
            if (speed < 1.0f) return;

            // Downforce = 0.5 * rho * Cl * A * v^2
            // F1 downforce generation simulation based on front/rear wing angles
            float frontAeroLoad = (m_ActiveSetup.frontWingAngle * 1.8f) * (speed * speed * 0.5f) * 0.015f;
            float rearAeroLoad = (m_ActiveSetup.rearWingAngle * 2.2f) * (speed * speed * 0.5f) * 0.015f;
            float totalDownforce = frontAeroLoad + rearAeroLoad;

            // Apply downward aerodynamic force
            m_Rigidbody.AddForce(-transform.up * totalDownforce, ForceMode.Force);

            // Aerodynamic drag force opposing velocity
            float dragCoefficient = 0.004f + (m_ActiveSetup.frontWingAngle + m_ActiveSetup.rearWingAngle) * 0.00018f;
            float aeroDrag = dragCoefficient * (speed * speed);
            m_Rigidbody.AddForce(-m_Rigidbody.linearVelocity.normalized * aeroDrag, ForceMode.Force);
        }
    }
}
