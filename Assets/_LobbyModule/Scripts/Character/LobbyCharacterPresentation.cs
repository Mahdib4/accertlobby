using UnityEngine;

namespace Accertone.LobbyModule
{
    /// <summary>
    /// Showroom presentation of the lobby driver: the driver stands completely still in the opening pose beside the
    /// car (looking at it, back / side to the camera) and rides the display turntable together with the car, so the
    /// platform, not the driver, does the presentation rotation (<c>CarDisplayMount</c> spins it).
    ///
    /// No walking, stepping or turning: player movement and the body controller are off, and
    /// <see cref="ProceduralLocomotion.holdPose"/> keeps the stance (straight legs, feet slightly apart) locked to the
    /// platform with only a faint breath.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class LobbyCharacterPresentation : MonoBehaviour
    {
        public LobbyCharacterController character;
        public ProceduralLocomotion locomotion;

        void Awake()
        {
            if (character == null) character = GetComponent<LobbyCharacterController>();
            if (locomotion == null) locomotion = GetComponent<ProceduralLocomotion>();
            if (character != null)
            {
                // The controller would otherwise re-apply its own facing every frame and fight the turntable.
                character.acceptPlayerInput = false;
                character.enabled = false;
            }
            if (locomotion != null) locomotion.holdPose = true;
        }

        void Start()
        {
            if (locomotion != null) locomotion.SnapFeetToStance();
        }
    }
}
