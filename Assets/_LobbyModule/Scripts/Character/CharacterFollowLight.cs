using UnityEngine;

namespace Accertone.LobbyModule
{
    /// <summary>
    /// Keeps a soft key light (camera side) and a violet rim light (far side) on the walking character,
    /// so the silhouette reads anywhere in the dim showroom, like a broadcast presentation.
    /// </summary>
    public class CharacterFollowLight : MonoBehaviour
    {
        public Transform character;
        public Transform viewCamera;
        public Light keyLight;
        public Light rimLight;
        public float keyDistance = 3.0f;
        public float keySide = 1.3f;
        public float keyHeight = 2.5f;
        public float rimDistance = 2.2f;
        public float rimHeight = 2.8f;
        public float aimHeight = 1.25f;
        public float sharpness = 6f;

        void LateUpdate()
        {
            if (character == null) return;
            if (viewCamera == null && Camera.main != null) viewCamera = Camera.main.transform;
            Vector3 c = character.position;
            Vector3 toCam = viewCamera != null ? Vector3.ProjectOnPlane(viewCamera.position - c, Vector3.up).normalized : -character.forward;
            Vector3 left = Vector3.Cross(toCam, Vector3.up);
            Vector3 aim = c + Vector3.up * aimHeight;
            float k = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            if (keyLight != null) Place(keyLight.transform, c + toCam * keyDistance + left * keySide + Vector3.up * keyHeight, aim, k);
            if (rimLight != null) Place(rimLight.transform, c - toCam * rimDistance - left * 0.6f + Vector3.up * rimHeight, aim, k);
        }

        static void Place(Transform t, Vector3 pos, Vector3 aim, float k)
        {
            t.position = Vector3.Lerp(t.position, pos, k);
            t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(aim - t.position), k);
        }
    }
}
