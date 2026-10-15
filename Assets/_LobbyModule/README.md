# Accert one Lobby Module: AC-26 display car + presented driver

Everything for the lobby's hero car and driver presentation lives in `Assets/_LobbyModule/`. It is staged into
`ACCERTONE_Lobby.unity` automatically whenever the lobby is rebuilt (**Accert one > Build Lobby Scene**).

## What plays when the lobby loads
Two display spinners present the car and the driver, then stop:

- **Main platform** (the podium turntable, carrying the AC-26 and the driver): holds while the showroom powers up
  (1.6 s), eases in, and eases to a complete stop after 10° counter-clockwise, with the car at its hero angle (5 s).
- **Driver spinner** (`DriverSpinner`, a small disc in the podium's style under the driver, riding the main
  platform): starts on the same frame with the same ease-in, keeps turning after the platform has stopped, and
  eases to a stop with the driver facing the camera (76.8° counter-clockwise, 9.4 s).
- **Driver:** completely still on his spinner. Opening facing: his original pose (looking at the cockpit, back to
  the camera) turned 30° to his own left (`DriverTurnLeft`). No walking, stepping, turning or head movement; only a
  faint breath. Legs straight (knees ~3.7°, vertical), feet under the hips with an ~11.6 cm gap.
- **Camera:** holds its showroom framing.

Measured in Play mode: the platform rests at exactly -10.000°, the driver spinner at -76.771°, and the driver ends
within 0.5° of facing the camera. His feet move 0.0000 m relative to his spinner. Both motions are a pure function
of time, so they land on the same angles at any frame rate.

Settings:
- Spinners: `PresentationSpinner` on `DisplayPodium` (main platform) and on `Turntable/DriverSpinner` (`travelDegrees`,
  `startDelay`, `cruiseSpeed`, `spinUpSeconds`, `spinDownSeconds`). Constants in `Editor/LobbyModuleBuilder.cs`
  (`PlatformTravel`, `SpinnerStartDelay`, ...). The driver spinner's travel is computed at staging so he ends facing
  the camera's showroom position; restage after moving the camera or changing `PlatformTravel`.
- `CarDisplayMount.rotate` (endless turntable) is off.
- Stance: on `ProceduralLocomotion` (`feetUnderHips` / `stanceExtraWidth`, `stanceToeOut`, `standingExtension`).
  `breathing = 0` makes the driver fully static.

> In the Editor, Play mode only advances while the Unity window has focus (Player Settings > Run In Background is off).

## Components

| Component | Role |
|---|---|
| `PresentationSpinner` (in `ACCERTONE/Scripts/Lobby`) | Turns a transform once by a set angle on the scene clock (smooth ease-in, cruise, ease-out to an exact stop). Drives the main platform and the driver spinner. |
| `CarDisplayMount` (in `ACCERTONE/Scripts/Lobby`) | The display slot: mounts the car under `Turntable`. Its endless turn (`rotate`) is off in the lobby. |
| `LobbyCharacterPresentation` | Keeps the driver a still display figure: switches the body controller and player movement off and puts `ProceduralLocomotion` into `holdPose`. |
| `ProceduralLocomotion` | Legs, pelvis, spine and head. `holdPose`: feet locked in the stance in the driver's own frame (so they ride the spinners) on the surface he is placed on, no steps, weight shift or head turn. Standing: the pelvis lifts and rolls slightly so both legs are nearly straight. (Walking, stepping-turn and choreography features remain available for other scenes.) |
| `LobbyCharacterController` | Body control for other scenes (walking, turning); disabled in the lobby display. |
| `LobbyWalkArea` | Podium / floor ground heights and the car's clearance envelope around the driver's spot. |
| `CharacterFollowLight` | Key light (camera side) and violet rim light on the driver. |

`LobbyCameraRig` (in `ACCERTONE/Scripts/Lobby`) keeps the cinematic showroom framing (`followTarget` is cleared).
Its drift and mouse follow obey Settings > Camera > Showroom camera motion.

The SETTINGS tab (display, graphics, audio, controls, gameplay, camera, interface) lives in `Settings/`; see
`Settings/README.md`. Staging the lobby also stages the settings screen.

## Why a procedural rig
The project has no Animator Controller, avatar or animation clips, and the three driver GLBs are single static meshes
with no skeleton, so there is no turn / idle / head-look clip to reuse. `Editor/LobbyCharacterRigBuilder.cs`
measures each mesh and builds a 14-bone skeleton with region-gated skin weights:
- **Cross-sections, not vertex sampling.** Body / arm / helmet boundaries come from where the mesh's triangles cut each
  height slice: a clear gap with something beyond it, or the neck where a hand or the helmet rests against the body.
  Nothing below the lowest measured gap counts as arm, so thighs can never be welded to the chest.
- **Legs split at the measured gap between them**, tracked slice by slice (these drivers stand with the pelvis shifted
  over one leg). No vertex is shared by both legs below the crotch, so the knees can't web together.
- Hip joints sit at the measured pelvis. Contact faces (hand on thigh, inner knees) are removed so nothing stretches.

When a rigged humanoid with clips arrives, drop it in and assign `LobbyCharacterController.animator`: it receives the
`Speed` float, and turn / idle clips can replace `ProceduralLocomotion`.

## The car
- `Prefabs/ACCERTONE_Car_AC26.prefab` sits under `DisplayPodium/Turntable/CarSpawnAnchor` (nose along the anchor's +Z,
  214° hero yaw). The driver (`LobbyCharacter_01`) stands on `Turntable/DriverSpinner`, so the platform turns car and driver
  together while his own spinner turns him.
- It is the team's own F1 model with the de-branded Accert one livery (`Editor/AccertoneLiveryDesigns.cs`). No
  placeholder or primitive car is used.
- To show a different car: assign its prefab to `CarDisplayMount → Car Prefab` (or change `CarPrefabPath`) and rebuild.

## Assets

| Area | Assets |
|---|---|
| Car | `Prefabs/ACCERTONE_Car_AC26.prefab`, `Materials/Car/*` (35 URP materials), `Textures/Car/Livery/*` |
| Characters | `Prefabs/Characters/LobbyCharacter_01..03.prefab` (01 is staged), skinned meshes in `Characters/` |
| Driver suits | `Materials/Driver/M_Driver_0X_*_Suit`: URP Lit, glossy helmet/visor + cloth suit |
| Scripts | `Scripts/Character/*`, `Scripts/PlanarFloorReflection.cs` |

## Swapping the character
Set `LobbyModuleBuilder.ActiveCharacter` (0, 1 or 2) and run **Accert one > Lobby Module > Build All**.

## Rebuilding
- **Accert one > Lobby Module > Build Assets**: livery, car + suit materials, car prefab, rigged characters.
- **Accert one > Lobby Module > Stage In Lobby**: restages car, driver, lights and reflections.
- **Accert one > Lobby Module > Build All**: both, plus a full lobby rebuild.
- `Tools~/process_textures.py` (Python 3 + Pillow + numpy) regenerates the plate, logo and driver maps.
