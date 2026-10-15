using System;

namespace Accertone.Branding
{
    /// <summary>Broadcasts brand asset edits so every bound component refreshes live in the editor.</summary>
    public static class BrandEvents
    {
        public static event Action Changed;

        public static void Raise()
        {
            if (Changed != null) Changed();
        }

        /// <summary>Safe to call from OnValidate: defers the broadcast until the editor is idle.</summary>
        public static void RaiseDeferred()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= Raise;
            UnityEditor.EditorApplication.delayCall += Raise;
#else
            Raise();
#endif
        }
    }
}
