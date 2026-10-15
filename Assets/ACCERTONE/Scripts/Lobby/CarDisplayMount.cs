using UnityEngine;

namespace Accertone.Lobby
{
    /// <summary>
    /// The hero display slot on the podium. Assign the imported F1 car prefab to <see cref="carPrefab"/>
    /// (or drop the model under CarSlot) and it is centred on the turntable automatically.
    /// Anything parented under <see cref="turntable"/> (the car, the presented driver) rotates with it as one display.
    /// The yellow gizmo shows a regulation-sized F1 envelope for placement.
    /// </summary>
    public class CarDisplayMount : MonoBehaviour
    {
        public Transform turntable;
        public Transform carSlot;
        public GameObject carPrefab;
        public bool rotate = true;
        [Tooltip("Degrees per second (negative turns counter-clockwise seen from above).")]
        public float rotationSpeed = 3.5f;
        [Tooltip("Seconds the turntable holds still after the scene starts.")]
        public float startDelay = 0f;
        [Tooltip("Seconds to ease from standstill to full speed, like a motorised display turntable.")]
        public float spinUpSeconds = 0f;
        [Tooltip("Width, height, length of the display envelope in metres.")]
        public Vector3 carEnvelope = new Vector3(2.0f, 0.95f, 5.6f);

        GameObject m_Instance;
        float m_RunTime;

        public GameObject CurrentCar { get { return m_Instance; } }
        /// <summary>Current turntable speed in degrees per second (eased during spin-up).</summary>
        public float CurrentSpeed { get; private set; }

        void Start()
        {
            if (carPrefab != null && carSlot != null && carSlot.childCount == 0) Mount(carPrefab);
        }

        void Update()
        {
            if (!rotate || turntable == null)
            {
                CurrentSpeed = 0f;
                return;
            }
            m_RunTime += Time.deltaTime;
            // Smooth spin-up (no jolt at either end), then a constant speed.
            float ramp = spinUpSeconds > 0f ? Mathf.Clamp01((m_RunTime - startDelay) / spinUpSeconds) : (m_RunTime >= startDelay ? 1f : 0f);
            ramp = ramp * ramp * ramp * (ramp * (ramp * 6f - 15f) + 10f);
            CurrentSpeed = rotationSpeed * ramp;
            turntable.Rotate(0f, CurrentSpeed * Time.deltaTime, 0f, Space.Self);
        }

        public GameObject Mount(GameObject prefab)
        {
            if (carSlot == null || prefab == null) return null;
            for (int i = carSlot.childCount - 1; i >= 0; i--) Destroy(carSlot.GetChild(i).gameObject);
            m_Instance = Instantiate(prefab, carSlot);
            m_Instance.transform.localPosition = Vector3.zero;
            m_Instance.transform.localRotation = Quaternion.identity;
            return m_Instance;
        }

        void OnDrawGizmos()
        {
            var slot = carSlot != null ? carSlot : transform;
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.matrix = slot.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, carEnvelope.y * 0.5f, 0f), carEnvelope);
            Gizmos.DrawLine(new Vector3(0f, 0.02f, carEnvelope.z * 0.5f), new Vector3(0f, 0.02f, carEnvelope.z * 0.5f + 0.6f));
        }
    }
}
