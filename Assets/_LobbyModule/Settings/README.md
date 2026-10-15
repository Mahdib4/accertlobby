# Accert one Settings

The lobby's SETTINGS tab: an overlay over the live showroom with seven categories (Display, Graphics, Audio,
Controls, Gameplay, Camera, Interface). It replaces the earlier settings screen (`GameSettingsData / Manager / UI`,
backed up in `F:/Accertonelobby/_Backup/Settings_before_refurbish_2026-10-11`). The design is the approved interactive
prototype (https://claude.ai/artifact/WNwfsCmfUVABXoiwnK3NdN).

## How it works

- **Edit, then Apply.** Changes are previewed straight away where that is safe (brightness, audio, graphics, motion),
  counted as unsaved changes (header pill, rail diamonds, row diamonds) and only kept when you press **Apply**.
  **Discard** throws them away, and leaving with unsaved changes asks Apply & exit / Discard & exit / Keep editing.
- **Display changes** (display, window mode, resolution, refresh rate) apply on Apply and must be confirmed within
  15 seconds, or they revert on their own.
- **Reset category / Reset all** go back to the defaults; you can still discard before applying.
- **Saved** to `persistentDataPath/accertone_settings_v2.json` (values plus Input System binding overrides) and loaded
  automatically before the first scene, so every scene starts with the player's settings.
- **Input:**

  | Action | Keyboard | Controller |
  |---|---|---|
  | Category | Q / E, or 1–7 to jump | LB / RB |
  | Move between rows | ↑ ↓ | D-pad or left stick |
  | Change value | ← → (Shift for big steps) | D-pad or left stick |
  | Open, rebind, test sound | Enter | A |
  | Apply | Space | Y |
  | Reset category | R | X |
  | Back | Esc | B |

  The mouse works everywhere, including the wheel, dragging sliders and double-clicking rows. Prompts switch
  between keyboard and controller glyphs automatically.

## What each category drives

| Category | Status | Connected to |
|---|---|---|
| Display | Works now | `Screen.SetResolution` (mode, refresh), `Screen.MoveMainWindowTo`, V-Sync, frame-rate cap, brightness and gamma (post-processing), interface scale (`SettingsCanvasScale` on the lobby canvas). Resolution / window changes apply in a **built player**; the Editor's Game view keeps its own size. |
| Graphics | Works now | Runtime copy of the URP asset and renderer (render scale, upscaling, MSAA, shadows, lights, SSAO), camera anti-aliasing, texture mip limit, anisotropic level, the showroom floor reflection, and runtime copies of the volume profiles (bloom, motion blur, depth of field, film grain). **Project assets are never modified**; originals are restored when Play mode ends. |
| Audio | Works now | `Resources/AccertoneAudioMixer` with groups Master, Music, Effects, Engine, Environment, Interface, Voice (exposed `Vol_*`), mutes, speaker mode, mute in background. The project has no audio yet, so interface sounds and per-channel test sounds are synthesised and routed through the mixer. Route game sounds with `AccertoneSettings.Instance.Audio.Group("Engine")`. |
| Controls | Works now / on track | New **Driving** action map in `InputSystem_Actions` (keyboard + controller). Rebinding with conflict detection (Swap / Replace / Cancel), saved overrides, reset to defaults, live input preview. Steering / pedal curves and vibration are exposed through `DrivingInput` for the future car controller. |
| Gameplay | On track | Assists, gearbox, damage, AI difficulty, tyre wear, fuel, rules: saved and read by race systems (`AccertoneSettings.Instance.Int(SettingKeys.TractionControl)` ...). |
| Camera | Works now / on track | Showroom camera motion drives the lobby camera's drift and mouse follow now; cockpit / chase / shake / look settings are saved for the race cameras. |
| Interface | Works now / on track | Speed units (lobby track length via `SpeedUnitsLabel`), interface animations (`AccertoneMotion.Scale`: lobby reveals and every settings transition), notifications (toasts). HUD and subtitle options are saved for the race HUD. |

Settings that only affect systems that don't exist yet carry an **ON TRACK** (or **ONLINE**) tag in the UI.

## Architecture

| Layer | Files |
|---|---|
| Data | `Runtime/Core/SettingDefinition.cs` (setting, section, page, category, preset, conditions), `SettingsCatalog.cs` (ScriptableObject, `Resources/AccertoneSettingsCatalog.asset`), `SettingsCatalogContent.cs` (default content), `SettingKeys.cs` |
| State | `SettingsStore.cs`: applied vs pending values, dirty tracking, presets (Graphics quality, Assists), save data |
| Service | `AccertoneSettings.cs`: boots before the first scene, loads / saves, Apply / Discard / Reset, display confirmation, `Effective(key)` for game systems |
| Systems | `Runtime/Systems/`: Display, Graphics, PostProcess, Audio (+ `SettingsAudioPlayer`), Input (+ `DrivingInput`, `InputGlyphs`), Lobby (+ `SpeedUnitsLabel`, `SettingsCanvasScale`) |
| UI | `Runtime/UI/`: `SettingsScreen` (rail, pages, focus, footer, dialogs), `SettingsRow`, `SettingControls` (selector, slider, toggle, segmented, dropdown, binding, action), `SettingsDetailPanel` (live previews), `SettingsOverlays` (buttons, dialog, toast, dropdown list), `SettingsMenuInput`, `SettingsUI` (theme + element factory) |
| Lobby | `LobbyNavigationCoordinator.cs`: HOME / GARAGE / SETTINGS routing, unsaved-change prompt when leaving, blocks lobby hotkeys while open |
| Editor | `Editor/SettingsBuilder.cs`: **Accert one > Settings > Build Assets** (catalog, mixer, Driving map) and **Stage In Lobby**; staging also runs with every lobby rebuild |

### Adding a setting
1. Add a key to `SettingKeys` and a definition in `SettingsCatalogContent` (or edit the catalog asset directly).
2. Run **Accert one > Settings > Build Assets**. The row, its control, detail text, default, reset and save all come
   from the definition.
3. Read it where it is used: `AccertoneSettings.Instance.Effective(SettingKeys.MySetting)`, and react to
   `SettingChanged` / `Applied`, or add a system implementing `ISettingsSystem`.

## Verified in Play mode (2026-10-11)
- **Navigation and pages.**
  - All 7 categories open, and the Controls page switches between Keyboard and Controller.
  - Keyboard, controller-style commands and mouse all work.
  - Esc in HOME opens Settings.
  - Leaving with unsaved changes asks first; Discard & exit restores everything.
- **Graphics.** The Ultra preset counts 6 changes and the runtime pipeline switches to STP, 4096 shadows over 80 m,
  TAA, motion blur and depth of field. After Apply it is saved and reloads on the next Play.
  `PC_RPAsset` and the post-FX profile asset were unchanged afterwards.
- **Audio.** Master 50% gives −6.02 dB, Engine 25% gives −12.04 dB, and Music muted gives −80 dB. Discard restores
  the saved levels, and startup levels apply correctly.
- **Bindings.** Rebinding Shift down to D detected the conflict with Steer right, and Swap gave Steer right the old Q.
  Discard restored both.
- **Display.** A resolution change opened the countdown dialog, and it reverted automatically after 15 seconds.
- **Reset.** Reset category (Gameplay) returned 5 changed settings to their defaults.
- **Console.** No errors or warnings.

## Not possible yet
- Gameplay assists, race cameras, HUD layout and subtitles: no race scene or car controller exists yet; values are
  stored and exposed for them.
- Voice chat volume: no voice chat system yet (routes to the Voice mixer group when one exists).
- Wheel / force-feedback devices: not in the Input Actions yet; add a control scheme and a page when needed.
- Resolution, window mode and display changes can only be seen in a built player.
