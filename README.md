# ACCERT ONE — Formula 1 Racing Lobby & Setup System

**Accert one** is a Formula 1–inspired PC racing game developed in **Unity 6 (6000.6.2f1) URP**.

---

## Features

### 1. Showroom & Presentation
* Real-time 3D showroom turntable featuring the AC-26 Formula 1 prototype car and driver avatar.
* Accert one motorsport brand identity: Dark Graphite (`#0A0B0E`), Carbon (`#111318`), and Violet Magenta accents (`#C042FF`).
* Custom chamfered procedural UI system (`AccertoneShape`), Inter & Roboto typography.

### 2. Game Modes (PLAY Selection Screen)
* **01. Quick Match (Play with AI)**: Full 20-car grid against 19 AI opponents, 4 difficulty tiers (*Amateur*, *Clubman*, *Pro*, *Legend*), distance options (3–30 laps), and dynamic weather.
* **02. Online Multiplayer**: 20-player room browser, custom room creation, room code connection, driver slot roster, and ready state toggling.
* **03. Career**: 2026 FIA Formula One Championship calendar across 24 official Grand Prix rounds, Driver profile (Super Licence Grade A), and live Drivers & Constructors points standings table.
* **04. Hot Lap**: Qualifying shootout with real-time sector split deltas against the Valcorsa benchmark record (1:29.874), ghost car, and tyre compound selection.
* **05. Free Practice**: Open track session with real-time aerodynamic and mechanical telemetry readouts, plus one-click launch into the detailed 35+ parameter car setup engineering system.

### 3. Engineering & Car Setup System
* **Aerodynamics**: Front and rear wing flap angles, dynamic aero balance %, rake angle, downforce vs. drag telemetry.
* **Transmission**: On-throttle and off-throttle differential locking, custom gear ratios (1st–8th + final drive).
* **Suspension & Geometry**: Front/rear spring stiffness, bump/rebound dampers, anti-roll bars, camber, and toe angles.
* **Braking**: Front/rear brake bias %, braking pressure, and brake duct cooling.
* **Tyre Model**: Pressures (PSI), compound thermal ranges, and wear simulation.
* **Preset Management**: Factory balanced, Monaco high downforce, Monza low drag speed, and wet weather setups.

### 4. Game Settings
* **Graphics**: Display resolution, window mode, refresh rate, URP quality presets (Low, Medium, High, Ultra), render scale, shadows, anti-aliasing (TAA/FXAA/SMAA), bloom, and motion blur.
* **Audio**: Multi-bus audio mixing with dedicated Master, Engine, Transmission Whine, Wind & Tyre Scrub, Radio/Engineer, and UI sliders.
* **Controls**: Dual Input System binding for steering wheels, gamepads, and keyboard/mouse, with deadzones, linearity, steering speed, and force feedback configuration.
* **Accessibility & Gameplay**: Racing line assists, braking assist, traction control (TC), ABS, HUD customization, and camera settings.

---

## Technical Specifications
* **Engine Version**: Unity 6000.6.2f1 (64-bit)
* **Render Pipeline**: Universal Render Pipeline (URP 17.6.0)
* **Input System**: Unity Input System (1.20.0)
* **UI Framework**: TextMeshPro + UGUI procedural rendering (`AccertoneShape`)
* **Target Resolution**: 1920×1080 (16:9)

---

## Getting Started
1. Clone or download this repository.
2. Open **Unity Hub** and click **Add project from disk**.
3. Select this folder (`accertlobby`).
4. Ensure **Unity 6000.6.2f1** or compatible Unity 6 URP editor is selected.
5. Open the project and launch scene `Assets/ACCERTONE/Scenes/ACCERTONE_Lobby.unity`.
