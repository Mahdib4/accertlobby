using UnityEngine;
using UnityEngine.UI;

namespace Accertone.Play
{
    /// <summary>
    /// Places a card graphic on a layer at <see cref="depth"/> pixels in front of (positive) or behind
    /// (negative) the card plane, then runs its vertices through the card's perspective projection.
    /// The lobby canvas is Screen Space Overlay (orthographic), so this is what gives the hover tilt
    /// real perspective and the layers genuine parallax.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class PlayCardDepth : BaseMeshEffect
    {
        public PlayModeCard card;
        public float depth;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || card == null || !card.HasProjection) return;
            RectTransform space = card.ProjectionSpace;
            RectTransform self = graphic.rectTransform;
            if (space == null || self == null) return;

            Matrix4x4 toCard = space.worldToLocalMatrix * self.localToWorldMatrix;
            Matrix4x4 toLocal = toCard.inverse;
            UIVertex v = default(UIVertex);
            int count = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Vector3 p = toCard.MultiplyPoint3x4(v.position);
                Vector2 q = card.Project(new Vector2(p.x, p.y), depth);
                v.position = toLocal.MultiplyPoint3x4(new Vector3(q.x, q.y, 0f));
                vh.SetUIVertex(v, i);
            }
        }
    }
}
