using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Accertone.LobbyModule
{
    /// <summary>
    /// Clean, view-correct ground reflections for the showroom floor (URP has no built-in planar reflections).
    /// A hidden camera mirrors the main camera across the floor plane into a render texture; the
    /// "ACCERTONE/Planar Reflection Overlay" material on this object adds it on top of the floor with Fresnel falloff.
    /// The reflection overlay renderer must sit on a layer excluded from <see cref="reflectLayers"/>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class PlanarFloorReflection : MonoBehaviour
    {
        public Camera sourceCamera;
        public float planeHeight;
        public LayerMask reflectLayers = ~((1 << 4) | (1 << 5));
        [Range(0.25f, 1f)] public float resolutionScale = 0.5f;
        public float clipOffset = 0.01f;

        static readonly int k_ReflectionTex = Shader.PropertyToID("_ReflectionTex");

        Camera m_ReflectionCamera;
        RenderTexture m_Texture;
        Renderer m_Renderer;
        MaterialPropertyBlock m_Block;

        void OnEnable()
        {
            m_Renderer = GetComponent<Renderer>();
            m_Block = new MaterialPropertyBlock();
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            if (m_ReflectionCamera != null) DestroyImmediate(m_ReflectionCamera.gameObject);
            if (m_Texture != null)
            {
                m_Texture.Release();
                DestroyImmediate(m_Texture);
            }
            m_ReflectionCamera = null;
            m_Texture = null;
        }

        void Update()
        {
            Camera src = sourceCamera != null ? sourceCamera : Camera.main;
            if (src == null) return;
            EnsureResources(src);
        }

        void EnsureResources(Camera src)
        {
            int w = Mathf.Max(64, Mathf.RoundToInt(src.pixelWidth * resolutionScale));
            int h = Mathf.Max(64, Mathf.RoundToInt(src.pixelHeight * resolutionScale));
            if (m_Texture == null || m_Texture.width != w || m_Texture.height != h)
            {
                if (m_Texture != null)
                {
                    m_Texture.Release();
                    DestroyImmediate(m_Texture);
                }
                m_Texture = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
                {
                    name = "ACCERTONE_FloorReflection",
                    useMipMap = true,
                    autoGenerateMips = true,
                    hideFlags = HideFlags.DontSave
                };
                m_Texture.Create();
            }

            if (m_ReflectionCamera == null)
            {
                var go = new GameObject("ACCERTONE_FloorReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
                m_ReflectionCamera = go.AddComponent<Camera>();
                var data = go.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = false;
                data.renderShadows = true;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.antialiasing = AntialiasingMode.None;
            }

            var cam = m_ReflectionCamera;
            cam.enabled = true;
            cam.targetTexture = m_Texture;
            cam.cullingMask = reflectLayers;
            cam.clearFlags = src.clearFlags;
            cam.backgroundColor = src.backgroundColor;
            cam.fieldOfView = src.fieldOfView;
            cam.nearClipPlane = src.nearClipPlane;
            cam.farClipPlane = src.farClipPlane;
            cam.allowHDR = true;
            cam.allowMSAA = false;
            cam.depth = src.depth - 10f;

            m_Renderer.GetPropertyBlock(m_Block);
            m_Block.SetTexture(k_ReflectionTex, m_Texture);
            m_Renderer.SetPropertyBlock(m_Block);
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != m_ReflectionCamera) return;
            Camera src = sourceCamera != null ? sourceCamera : Camera.main;
            if (src == null) return;

            Vector3 pos = new Vector3(0f, planeHeight, 0f);
            Vector3 normal = Vector3.up;
            float d = -Vector3.Dot(normal, pos) - clipOffset;
            Matrix4x4 reflection = ReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, d));

            camera.transform.SetPositionAndRotation(reflection.MultiplyPoint(src.transform.position), src.transform.rotation);
            camera.worldToCameraMatrix = src.worldToCameraMatrix * reflection;
            camera.projectionMatrix = src.projectionMatrix;
            Vector4 clip = CameraSpacePlane(camera, pos, normal);
            camera.projectionMatrix = camera.CalculateObliqueMatrix(clip);
            GL.invertCulling = true;
        }

        void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == m_ReflectionCamera) GL.invertCulling = false;
        }

        Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal)
        {
            Vector3 offsetPos = pos + normal * clipOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(offsetPos);
            Vector3 cnormal = m.MultiplyVector(normal).normalized;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = 1f - 2f * p.x * p.x;
            m.m01 = -2f * p.x * p.y;
            m.m02 = -2f * p.x * p.z;
            m.m03 = -2f * p.w * p.x;
            m.m10 = -2f * p.y * p.x;
            m.m11 = 1f - 2f * p.y * p.y;
            m.m12 = -2f * p.y * p.z;
            m.m13 = -2f * p.w * p.y;
            m.m20 = -2f * p.z * p.x;
            m.m21 = -2f * p.z * p.y;
            m.m22 = 1f - 2f * p.z * p.z;
            m.m23 = -2f * p.w * p.z;
            return m;
        }
    }
}
