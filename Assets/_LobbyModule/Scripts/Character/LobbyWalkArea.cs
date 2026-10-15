using UnityEngine;

namespace Accertone.LobbyModule
{
    /// <summary>
    /// Where the lobby character may walk: the showroom floor plus the display podium, minus a clearance
    /// envelope around the car at <see cref="carSpawnAnchor"/> (every point of the walkable edge stays at least
    /// <see cref="clearance"/> from the car's footprint, wing corners included). Also answers ground height so
    /// feet step up onto the podium instead of clipping through it.
    /// </summary>
    public class LobbyWalkArea : MonoBehaviour
    {
        [Header("Car")]
        [Tooltip("The car's reference point (ACCERTONE_Showroom/DisplayPodium/Turntable/CarSpawnAnchor).")]
        public Transform carSpawnAnchor;
        [Tooltip("Car footprint in metres (width, length). Matches CarDisplayMount's F1 envelope.")]
        public Vector2 carFootprint = new Vector2(2.0f, 5.6f);
        [Tooltip("Walking clearance kept around the footprint (includes the character's own radius).")]
        public float clearance = 0.55f;

        [Header("Podium (heights in metres, radii from the podium centre)")]
        public Transform podium;
        public float turntableRadius = 3.30f;
        public float turntableHeight = 0.211f;
        public float baseTopRadius = 3.70f;
        public float baseHeight = 0.181f;
        public float baseOuterRadius = 3.84f;

        [Header("Showroom bounds (world XZ)")]
        public Vector2 boundsMin = new Vector2(-7.0f, -11.0f);
        public Vector2 boundsMax = new Vector2(7.0f, 5.9f);

        /// <summary>Floor height under a world position (podium, turntable or showroom floor).</summary>
        public float GroundHeight(Vector3 world)
        {
            float baseY = podium != null ? podium.position.y : 0f;
            if (podium == null) return baseY;
            Vector3 c = podium.position;
            float r = new Vector2(world.x - c.x, world.z - c.z).magnitude;
            if (r <= turntableRadius) return baseY + turntableHeight;
            if (r <= baseTopRadius)
            {
                // tiny lip between turntable and base ring, blended so feet don't pop
                float t = Mathf.InverseLerp(turntableRadius, turntableRadius + 0.04f, r);
                return baseY + Mathf.Lerp(turntableHeight, baseHeight, t);
            }
            if (r <= baseOuterRadius)
                return baseY + Mathf.Lerp(baseHeight, 0f, Mathf.InverseLerp(baseTopRadius, baseOuterRadius, r));
            return baseY;
        }

        /// <summary>Clamp a desired position into the walkable area (bounds, then out of the car clearance).</summary>
        public Vector3 Constrain(Vector3 world)
        {
            world.x = Mathf.Clamp(world.x, boundsMin.x, boundsMax.x);
            world.z = Mathf.Clamp(world.z, boundsMin.y, boundsMax.y);
            if (carSpawnAnchor == null) return world;

            // Rounded rectangle: the footprint box grown by the clearance.
            Vector3 local = carSpawnAnchor.InverseTransformPoint(world);
            float hx = carFootprint.x * 0.5f, hz = carFootprint.y * 0.5f;
            var closest = new Vector2(Mathf.Clamp(local.x, -hx, hx), Mathf.Clamp(local.z, -hz, hz));
            var away = new Vector2(local.x, local.z) - closest;
            Vector2 resolved;
            if (away.sqrMagnitude > 1e-8f)
            {
                if (away.sqrMagnitude >= clearance * clearance) return world;
                resolved = closest + away.normalized * clearance;
            }
            else
            {
                // inside the footprint: out through the nearest side
                float dx = hx - Mathf.Abs(local.x), dz = hz - Mathf.Abs(local.z);
                resolved = dx < dz
                    ? new Vector2(Mathf.Sign(local.x == 0f ? 1f : local.x) * (hx + clearance), local.z)
                    : new Vector2(local.x, Mathf.Sign(local.z == 0f ? 1f : local.z) * (hz + clearance));
            }
            Vector3 pushed = carSpawnAnchor.TransformPoint(new Vector3(resolved.x, local.y, resolved.y));
            world.x = pushed.x;
            world.z = pushed.z;
            return world;
        }

        /// <summary>
        /// A point on the walking loop around the car: the clearance outline grown by <paramref name="extra"/>
        /// (a rounded rectangle, so the margin holds at the wing corners too), along the ray at
        /// <paramref name="angleDegrees"/>: 0 = in front of the nose, 90 = car's left side, 180 = behind the rear wing.
        /// The loop stays on the flat podium top rather than its edge.
        /// </summary>
        public Vector3 LoopPoint(float angleDegrees, float extra = 0.35f)
        {
            if (carSpawnAnchor == null) return transform.position;
            float radius = clearance + extra;
            float hx = carFootprint.x * 0.5f, hz = carFootprint.y * 0.5f;
            if (podium != null) hz = Mathf.Max(hz - extra + 0.1f, Mathf.Min(hz, baseTopRadius - 0.2f - radius));
            float rad = angleDegrees * Mathf.Deg2Rad;
            var dir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            float dist = RayToOutline(dir, hx, hz, radius);
            if (podium != null)
            {
                // Every footfall on the flat podium top (the rounded corners would otherwise reach its edge),
                // but never inside the clearance itself.
                Vector3 c = carSpawnAnchor.InverseTransformPoint(podium.position);
                float onTop = RayCircle(new Vector2(c.x, c.z), dir, baseTopRadius - 0.25f);
                float minimum = RayToOutline(dir, carFootprint.x * 0.5f, carFootprint.y * 0.5f, clearance + 0.05f);
                dist = Mathf.Max(minimum, Mathf.Min(dist, onTop));
            }
            Vector2 q = dir * dist;
            var local = new Vector3(q.x, 0f, q.y);
            Vector3 w = carSpawnAnchor.TransformPoint(local);
            w.y = GroundHeight(w);
            return w;
        }

        /// <summary>Distance along a ray from the car origin to the outline at <paramref name="radius"/> around a
        /// half-extent box (distance to the box grows monotonically along the ray, so bisect).</summary>
        static float RayToOutline(Vector2 dir, float hx, float hz, float radius)
        {
            float lo = 0f, hi = hx + hz + radius * 2f;
            for (int i = 0; i < 28; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vector2 p = dir * mid;
                var d = new Vector2(Mathf.Max(0f, Mathf.Abs(p.x) - hx), Mathf.Max(0f, Mathf.Abs(p.y) - hz));
                if (d.magnitude < radius) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>Distance along a ray from the origin (unit <paramref name="dir"/>) to a circle around <paramref name="centre"/>.</summary>
        static float RayCircle(Vector2 centre, Vector2 dir, float radius)
        {
            float b = Vector2.Dot(dir, centre);
            float disc = b * b - (centre.sqrMagnitude - radius * radius);
            return disc < 0f ? float.MaxValue : b + Mathf.Sqrt(disc);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.75f, 0.35f, 1f, 0.9f);
            if (carSpawnAnchor != null)
            {
                float hx = carFootprint.x * 0.5f, hz = carFootprint.y * 0.5f;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= 64; i++)
                {
                    // rounded rectangle: a quarter arc around each footprint corner
                    float t = i / 64f * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Sin(t), Mathf.Cos(t));
                    var corner = new Vector2(Mathf.Sign(dir.x) * hx, Mathf.Sign(dir.y) * hz);
                    Vector3 p = carSpawnAnchor.TransformPoint(new Vector3(corner.x + dir.x * clearance, 0.02f, corner.y + dir.y * clearance));
                    if (i > 0) Gizmos.DrawLine(prev, p);
                    prev = p;
                }
            }
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Vector3 c = new Vector3((boundsMin.x + boundsMax.x) * 0.5f, 0.02f, (boundsMin.y + boundsMax.y) * 0.5f);
            Gizmos.DrawWireCube(c, new Vector3(boundsMax.x - boundsMin.x, 0.01f, boundsMax.y - boundsMin.y));
        }
    }
}
