# Accert one: Lobby & Brand System

The official game name is **Accert one** (capital A, lowercase "one", one space). Every visible name string reads it from
`AccertoneBrandIdentity.OfficialName` / `DisplayName`, so it is never retyped or re-cased in UI code. (Folder, asset and
class names keep the legacy `ACCERTONE` identifier; they are internal and never shown to players.)

Open **`Assets/ACCERTONE/Scenes/ACCERTONE_Lobby.unity`** (it is build index 0) and press Play.

## Brand system (single source of truth)

| Asset | What it controls |
|---|---|
| `Branding/Resources/ACCERTONE_BrandIdentity` | Game name ("Accert one"), `Accert| one` tone split, tagline, **official logo slot**, links to palette + typography |
| `Branding/ACCERTONE_BrandPalette` | Semantic colours (Graphite, Carbon, Panel, Hairline, Metallic, Silver, White, **Accent**, Positive, text tones) |
| `Branding/ACCERTONE_Typography` | Font families + role styles (Wordmark, Display, Headline, Title, Label, Body, Caption, Data, DataLarge, Tagline) |

UI elements bind to *roles*, never raw values (`BrandColor`, `BrandTextStyle`), so editing the palette or
typography asset updates every screen live.

**Palette.** Dark graphite / carbon neutrals, metallic greys, white, and one controlled accent:
*Accert one Violet* `#C042FF`, taken from the AC-26 car livery. The accent is reserved for selection, the
primary action, LEDs and brand marks. (The original placeholder was signal red `#FF2B3A`; change `accent`
in the palette asset and rebuild the lobby to switch.)
`Positive` (`#3FD69A`) is functional only (online / ready).

**Typography.** Roboto Black Italic (wordmark, hero actions), Roboto Black (headlines), Roboto Bold (titles),
Inter SemiBold (navigation, labels), Inter Regular (body), Roboto Mono (telemetry), Roboto Light (large numerals).
Fonts are open-licensed (Roboto/Roboto Mono: Apache 2.0, Inter: SIL OFL) and live in `Art/Fonts/Source`.

## Replacing the temporary wordmark with the official logo

The current wordmark is a **temporary typographic treatment**: TextMeshPro "Accert one" set in Roboto Black Italic with a
weight/tone split ("Accert" white, " one" silver), plus two forward-leaning accent slashes. In the showroom it appears
once, as the backlit `ACCERTONE_WallSign` above the podium, and on the HUD wordmark; nothing else repeats it.
When the final logo exists:

1. Import it as a Sprite (PNG/SVG, transparent background).
2. Assign it to `ACCERTONE_BrandIdentity → Official Logo` (and optionally `Official Logo Signage` for the wall).
3. Every `ACCERTONE_Wordmark` (UI) and `ACCERTONE_WallSign` (showroom) instance switches to the artwork automatically.

## Reusable prefabs (`Prefabs/`)

| Prefab | Use on |
|---|---|
| `Branding/ACCERTONE_Wordmark` | Main menu, lobby, pause, results: includes the restrained entrance reveal |
| `Branding/ACCERTONE_TransitionOverlay` | Loading screen / race launch / return-to-lobby: `Play(status, seconds, onCovered, revealAfter)` |
| `Branding/ACCERTONE_WallSign` | Dimensional, backlit in-world signage |
| `Lobby/ACCERTONE_DisplayPodium` | Turntable display podium with LED ring and car slot |

## The podium, the car and the lobby character

`ACCERTONE_Showroom/DisplayPodium/Turntable/CarSpawnAnchor` is the car's reference point (origin, 214° hero yaw).
The AC-26 display car (`Assets/_LobbyModule/Prefabs/ACCERTONE_Car_AC26.prefab`) is staged there by the lobby module.
The yellow gizmo on `CarDisplayMount` shows the 2.0 × 0.95 × 5.6 m F1 envelope, and `Lobby_WalkArea` keeps a 0.55 m
clearance around it. To display a different car, assign its prefab to `CarDisplayMount → Car Prefab`. Car and
character are not reflection-probe static, so the baked probe shows the room and never the car itself.

The turntable is the main display spinner and carries the car and the driver. The driver stands still on his own small
spinner (`Turntable/DriverSpinner`). Both `PresentationSpinner`s start together after the showroom powers up:
- the platform eases to a stop with the car at its hero angle;
- the driver's spinner keeps turning until he faces the camera.

See `Assets/_LobbyModule/README.md`.

## Wiring the lobby

`LobbyController` handles RACE (Enter / click), tab navigation (Q / E) and the deploy transition.
Set **Race Scene Name** once the race scene exists; until then the transition returns to the lobby.
`LobbyNavigationCoordinator` routes HOME / GARAGE / SETTINGS (Esc in HOME opens Settings). The settings system,
including how game systems read player settings, is documented in `Assets/_LobbyModule/Settings/README.md`.
Placeholder copy (driver name, track, telemetry) is plain TextMeshPro, ready to bind to real data.

## Rebuilding

`Accert one → Build Lobby Scene` regenerates the scene, materials, meshes and prefabs from
`Editor/AccertoneLobbyBuilder.cs`. **It overwrites `ACCERTONE_Lobby.unity`**, so make manual scene edits
in a copy, or move them into the builder.
