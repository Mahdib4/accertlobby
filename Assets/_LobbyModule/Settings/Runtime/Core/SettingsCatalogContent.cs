using System.Collections.Generic;

namespace Accertone.Settings
{
    /// <summary>
    /// The default content of the settings catalog (the approved design). The editor builder writes it into the
    /// catalog asset; the runtime also falls back to it if the asset is missing.
    /// </summary>
    public static class SettingsCatalogContent
    {
        const string Kbm = "Keyboard&Mouse";
        const string Pad = "Gamepad";
        static readonly string[] OffLowHigh = { "Off", "Low", "High" };
        static readonly string[] LowMedHigh = { "Low", "Medium", "High" };

        public static void Fill(SettingsCatalog catalog)
        {
            catalog.categories = new List<SettingCategory> { Display(), Graphics(), Audio(), Controls(), Gameplay(), Camera(), Interface() };
            catalog.presets = new List<SettingPreset> { GraphicsPreset(), AssistPreset() };
        }

        // ------------------------------------------------------------------ builders
        static SettingDefinition Def(string key, string label, SettingKind kind, string info, SettingApplies applies, SettingPreview preview)
        {
            return new SettingDefinition { key = key, label = label, kind = kind, info = info, applies = applies, preview = preview };
        }
        static SettingDefinition Choice(SettingKind kind, string key, string label, string[] options, double def, string info, SettingApplies applies = SettingApplies.Instant, SettingPreview preview = SettingPreview.None, bool ordinal = false)
        {
            var d = Def(key, label, kind, info, applies, preview);
            d.options = options; d.defaultValue = def; d.ordinal = ordinal;
            return d;
        }
        static SettingDefinition Toggle(string key, string label, bool def, string info, SettingApplies applies = SettingApplies.Instant, SettingPreview preview = SettingPreview.None)
        {
            var d = Def(key, label, SettingKind.Toggle, info, applies, preview);
            d.defaultValue = def ? 1 : 0;
            return d;
        }
        static SettingDefinition Slider(string key, string label, double min, double max, double step, double def, string unit, string info, SettingApplies applies = SettingApplies.Instant, SettingPreview preview = SettingPreview.None, int decimals = 0)
        {
            var d = Def(key, label, SettingKind.Slider, info, applies, preview);
            d.min = min; d.max = max; d.step = step; d.defaultValue = def; d.unit = unit; d.decimals = decimals;
            return d;
        }
        static SettingDefinition Bind(string group, string action, string label, string info, bool locked = false)
        {
            var d = Def("bind." + (group == Pad ? "pad." : "kb.") + action, label, SettingKind.Binding, info, SettingApplies.Instant, SettingPreview.Input);
            d.actionName = action; d.bindingGroup = group; d.locked = locked;
            return d;
        }
        static SettingDefinition Volume(string key, string label, double def, string channel, string info, SettingApplies applies = SettingApplies.Instant)
        {
            var d = Slider(key, label, 0, 100, 1, def, "%", info, applies, SettingPreview.AudioBus);
            d.muteKey = key + SettingKeys.MuteSuffix; d.audioChannel = channel;
            return d;
        }
        static SettingDefinition Cost(SettingDefinition d, int gpu, int visual) { d.gpuCost = gpu; d.visualGain = visual; d.preview = SettingPreview.Graphics; return d; }
        static SettingDefinition When(SettingDefinition d, string key, ConditionOp op, double value, string reason)
        {
            var list = new List<SettingCondition>(d.disabledWhen) { new SettingCondition { key = key, op = op, value = value, reason = reason } };
            d.disabledWhen = list.ToArray();
            return d;
        }
        static SettingDefinition Source(SettingDefinition d, SettingOptionSource s) { d.optionSource = s; return d; }
        static SettingSection Section(string title, params SettingDefinition[] settings) { return new SettingSection { title = title, settings = new List<SettingDefinition>(settings) }; }
        static SettingCategory Category(string id, string title, string blurb, SettingDefinition header, params SettingSection[] sections)
        {
            var c = new SettingCategory { id = id, title = title, blurb = blurb, header = header };
            c.pages.Add(new SettingPage { id = "main", label = title, sections = new List<SettingSection>(sections) });
            return c;
        }

        // ------------------------------------------------------------------ categories
        static SettingCategory Display()
        {
            const SettingApplies C = SettingApplies.Confirm;
            return Category("display", "Display", "Screen, timing and image calibration.", null,
                Section("Screen",
                    Source(Choice(SettingKind.Dropdown, SettingKeys.Monitor, "Display", new string[0], 0,
                        "The monitor the game runs on. Moving to another display asks you to confirm and reverts on its own if you don't.", C, SettingPreview.Monitor), SettingOptionSource.Displays),
                    Choice(SettingKind.Segmented, SettingKeys.WindowMode, "Window mode", new[] { "Fullscreen", "Borderless", "Windowed" }, 1,
                        "Fullscreen gives the game the whole display and lets you choose a refresh rate. Borderless switches to other apps instantly. Windowed runs in a window you can move.", C, SettingPreview.Monitor),
                    Source(Choice(SettingKind.Dropdown, SettingKeys.Resolution, "Resolution", new string[0], 0,
                        "Size of the game image. To trade sharpness for frame rate while keeping menus crisp, lower Render scale in Graphics instead.", C, SettingPreview.Monitor), SettingOptionSource.Resolutions),
                    When(Source(Choice(SettingKind.Selector, SettingKeys.RefreshRate, "Refresh rate", new string[0], 0,
                        "How many times per second the display updates. Match it to your monitor for the smoothest image.", C, SettingPreview.Monitor, true), SettingOptionSource.RefreshRates),
                        SettingKeys.WindowMode, ConditionOp.NotEquals, 0, "Borderless and Windowed use your desktop refresh rate.")),
                Section("Sync & frame rate",
                    Toggle(SettingKeys.VSync, "V-Sync", false, "Synchronises frames with the display to remove tearing. Adds a little input delay.", SettingApplies.Instant, SettingPreview.Monitor),
                    When(Choice(SettingKind.Selector, SettingKeys.FrameRateLimit, "Frame-rate limit", new[] { "30", "60", "120", "144", "165", "240", "Unlimited" }, 3,
                        "Caps the frame rate to keep your GPU cooler and frame pacing even. Unlimited renders as fast as possible.", SettingApplies.Instant, SettingPreview.Monitor, true),
                        SettingKeys.VSync, ConditionOp.Equals, 1, "V-Sync caps the frame rate at your refresh rate.")),
                Section("Image",
                    Slider(SettingKeys.Brightness, "Brightness", 0, 100, 1, 50, null, "Raise or lower until the left mark below is only just visible against black.", SettingApplies.Instant, SettingPreview.Calibration),
                    Slider(SettingKeys.Gamma, "Gamma", 1.8, 2.6, 0.05, 2.2, null, "Shapes mid-tones. Lower values lift shadows; higher values deepen them. 2.2 matches most displays.", SettingApplies.Instant, SettingPreview.Calibration, 2),
                    Slider(SettingKeys.InterfaceScale, "Interface scale", 80, 120, 5, 100, "%", "Size of menus and on-screen text. It takes effect when you apply. The race HUD has its own scale under Interface.", SettingApplies.Instant, SettingPreview.InterfaceScale)));
        }

        static SettingCategory Graphics()
        {
            const string postOff = "Turn on Post-processing to use this.";
            var preset = Choice(SettingKind.Segmented, SettingKeys.GraphicsPreset, "Quality preset", new[] { "Low", "Medium", "High", "Ultra", "Custom" }, 2,
                "Sets every option below at once. Changing any single option switches the preset to Custom.", SettingApplies.Instant, SettingPreview.Graphics);
            return Category("graphics", "Graphics", "Image quality and performance.", preset,
                Section("Resolution & anti-aliasing",
                    Cost(Slider(SettingKeys.RenderScale, "Render scale", 50, 200, 5, 100, "%", "Resolution of the 3D image relative to the screen. Menus and text always stay sharp."), 5, 4),
                    When(Cost(Choice(SettingKind.Selector, SettingKeys.Upscaling, "Upscaling", new[] { "Bilinear", "FSR 1.0", "STP" }, 1,
                        "How the 3D image is enlarged when Render scale is below 100%. STP gives the cleanest result; FSR is lighter."), 1, 3),
                        SettingKeys.RenderScale, ConditionOp.AtLeast, 100, "Only used when Render scale is below 100%."),
                    Cost(Choice(SettingKind.Selector, SettingKeys.AntiAliasing, "Anti-aliasing", new[] { "Off", "FXAA", "SMAA", "TAA", "MSAA 2×", "MSAA 4×" }, 2,
                        "Smooths jagged edges. SMAA is sharp and cheap, TAA is smoothest in motion, MSAA is the most accurate on geometry."), 3, 4)),
                Section("Textures",
                    Cost(Choice(SettingKind.Selector, SettingKeys.TextureQuality, "Texture quality", new[] { "Quarter", "Half", "Full" }, 2, "Resolution of surfaces and liveries. Lower settings use less video memory.", ordinal: true), 2, 5),
                    Cost(Choice(SettingKind.Selector, SettingKeys.Anisotropic, "Anisotropic filtering", new[] { "Off", "2×", "4×", "8×", "16×" }, 4, "Keeps the track surface and kerbs sharp at grazing angles.", ordinal: true), 1, 3)),
                Section("Lighting & shadows",
                    Cost(Choice(SettingKind.Selector, SettingKeys.ShadowQuality, "Shadow quality", new[] { "Off", "Low", "Medium", "High", "Ultra" }, 3, "Shadow resolution, distance and softness.", ordinal: true), 5, 5),
                    Cost(Choice(SettingKind.Selector, SettingKeys.LightingQuality, "Lighting quality", LowMedHigh, 2, "How many lights shade each object and whether they cast shadows.", ordinal: true), 3, 3),
                    Cost(Choice(SettingKind.Selector, SettingKeys.AmbientOcclusion, "Ambient occlusion", OffLowHigh, 2, "Soft contact shadows where surfaces meet, such as under the car and inside the cockpit.", ordinal: true), 3, 3),
                    Cost(Choice(SettingKind.Selector, SettingKeys.Reflections, "Reflections", LowMedHigh, 2, "Floor reflections and reflection quality in the showroom and garage.", ordinal: true), 3, 4)),
                Section("Post-processing",
                    Cost(Toggle(SettingKeys.PostProcessing, "Post-processing", true, "Master switch for the effects below. The showroom's colour grade always stays on."), 2, 4),
                    When(Cost(Toggle(SettingKeys.Bloom, "Bloom", true, "Glow around bright lights and LED strips."), 1, 3), SettingKeys.PostProcessing, ConditionOp.Equals, 0, postOff),
                    When(Cost(Choice(SettingKind.Selector, SettingKeys.MotionBlur, "Motion blur", OffLowHigh, 1, "Blur from camera movement at speed.", ordinal: true), 1, 2), SettingKeys.PostProcessing, ConditionOp.Equals, 0, postOff),
                    When(Cost(Toggle(SettingKeys.DepthOfField, "Depth of field", false, "Focus blur in replays and photo mode."), 2, 2), SettingKeys.PostProcessing, ConditionOp.Equals, 0, postOff),
                    When(Cost(Toggle(SettingKeys.FilmGrain, "Film grain", true, "Subtle grain that softens banding in dark areas."), 1, 1), SettingKeys.PostProcessing, ConditionOp.Equals, 0, postOff)));
        }

        static SettingCategory Audio()
        {
            return Category("audio", "Audio", "Mix, mute and output.", null,
                Section("Mix",
                    Volume(SettingKeys.MasterVolume, "Master", 85, "Master", "Overall volume. Press Enter to play a test sound."),
                    Volume(SettingKeys.MusicVolume, "Music", 65, "Music", "Menu and replay music."),
                    Volume(SettingKeys.EffectsVolume, "Effects", 80, "Effects", "Tyres, impacts, gear shifts and pit equipment."),
                    Volume(SettingKeys.EngineVolume, "Engine", 90, "Engine", "Your power unit and other cars."),
                    Volume(SettingKeys.EnvironmentVolume, "Environment", 70, "Environment", "Crowd, wind, rain and the showroom ambience."),
                    Volume(SettingKeys.InterfaceVolume, "Interface", 60, "Interface", "Menu clicks and confirmations, like the ticks you hear moving through this screen."),
                    Volume(SettingKeys.VoiceVolume, "Voice chat", 85, "Voice", "Other players in online sessions.", SettingApplies.Online)),
                Section("Output",
                    Choice(SettingKind.Selector, SettingKeys.SpeakerMode, "Speaker setup", new[] { "Stereo", "Quad", "5.1 Surround", "7.1 Surround" }, 0,
                        "Match your speakers or headphones. Changing it restarts audio for a moment when you apply.", SettingApplies.Instant, SettingPreview.AudioBus),
                    Toggle(SettingKeys.MuteInBackground, "Mute in background", true, "Silences the game while you are in another window.", SettingApplies.Instant, SettingPreview.AudioBus)));
        }

        static SettingCategory Controls()
        {
            var tabs = Choice(SettingKind.DeviceTabs, SettingKeys.ControlsDevice, "Device", new[] { "Keyboard & mouse", "Controller" }, 0, "Switch between keyboard and controller settings.");
            var c = new SettingCategory { id = "controls", title = "Controls", blurb = "Bindings, steering and feedback.", header = tabs };

            var kbm = new SettingPage { id = "kbm", label = "Keyboard & mouse" };
            var driving = Section("Driving",
                Bind(Kbm, "Throttle", "Accelerate", "Throttle. Keyboard pedals ramp in quickly; adjust the response below."),
                Bind(Kbm, "Brake", "Brake", "Brake pedal."),
                Bind(Kbm, "SteerLeft", "Steer left", "Steer left. Keyboard steering eases toward full lock at the speed set below."),
                Bind(Kbm, "SteerRight", "Steer right", "Steer right."),
                Bind(Kbm, "ShiftUp", "Shift up", "Next gear (manual gearbox)."),
                Bind(Kbm, "ShiftDown", "Shift down", "Previous gear (manual gearbox)."),
                Bind(Kbm, "Drs", "DRS / overtake", "Opens the rear wing in DRS zones."),
                Bind(Kbm, "LookBack", "Look back", "Rear view while held."),
                Bind(Kbm, "ChangeCamera", "Change camera", "Cycles the driving cameras."),
                Bind(Kbm, "PitLimiter", "Pit limiter", "Limits speed in the pit lane."),
                Bind(Kbm, "Pause", "Pause", "Pause is always on Esc so you can't lock yourself out.", true));
            driving.bindingColumns = true;
            kbm.sections.Add(driving);
            kbm.sections.Add(Section("Response",
                Slider(SettingKeys.KeyboardSteerSpeed, "Keyboard steering speed", 1, 10, 1, 6, null, "How quickly a held steering key reaches full lock.", SettingApplies.OnTrack, SettingPreview.Input),
                Slider(SettingKeys.KeyboardSteerSensitivity, "Steering sensitivity", 50, 150, 5, 100, "%", "Overall steering response. Above 100% reaches full lock sooner.", SettingApplies.OnTrack, SettingPreview.Input),
                Slider(SettingKeys.KeyboardSteerLinearity, "Steering linearity", 0, 100, 5, 30, "%", "0% is linear. Higher values soften the centre for small corrections at speed.", SettingApplies.OnTrack, SettingPreview.Input),
                Slider(SettingKeys.KeyboardThrottle, "Throttle sensitivity", 50, 150, 5, 100, "%", "How hard a key press applies throttle.", SettingApplies.OnTrack, SettingPreview.Input),
                Slider(SettingKeys.KeyboardBrake, "Brake sensitivity", 50, 150, 5, 100, "%", "How hard a key press applies the brake.", SettingApplies.OnTrack, SettingPreview.Input)));

            var pad = new SettingPage { id = "pad", label = "Controller" };
            var padDriving = Section("Driving",
                Choice(SettingKind.Selector, SettingKeys.PadSteerStick, "Steering", new[] { "Left stick", "Right stick" }, 0, "Which stick steers the car.", SettingApplies.Instant, SettingPreview.Input),
                Bind(Pad, "Throttle", "Accelerate", "Throttle trigger."),
                Bind(Pad, "Brake", "Brake", "Brake trigger."),
                Bind(Pad, "ShiftUp", "Shift up", "Next gear."),
                Bind(Pad, "ShiftDown", "Shift down", "Previous gear."),
                Bind(Pad, "Drs", "DRS / overtake", "Opens the rear wing in DRS zones."),
                Bind(Pad, "LookBack", "Look back", "Rear view while held."),
                Bind(Pad, "ChangeCamera", "Change camera", "Cycles the driving cameras."),
                Bind(Pad, "PitLimiter", "Pit limiter", "Limits speed in the pit lane."),
                Bind(Pad, "Pause", "Pause", "Pause is always on the Menu button.", true));
            padDriving.bindingColumns = true;
            pad.sections.Add(padDriving);
            pad.sections.Add(Section("Response",
                Slider(SettingKeys.PadSteerSensitivity, "Steering sensitivity", 50, 150, 5, 100, "%", "Overall steering response.", SettingApplies.OnTrack, SettingPreview.Input),
                Slider(SettingKeys.PadSteerDeadzone, "Steering deadzone", 0, 30, 1, 8, "%", "Stick travel ignored around the centre. Raise it if the car drifts with the stick released.", SettingApplies.OnTrack, SettingPreview.Input),
                Slider(SettingKeys.PadSteerLinearity, "Steering linearity", 0, 100, 5, 40, "%", "0% is linear. Higher values give finer control near the centre.", SettingApplies.OnTrack, SettingPreview.Input),
                Slider(SettingKeys.PadTriggerDeadzone, "Trigger deadzone", 0, 30, 1, 5, "%", "Trigger travel ignored before the pedal starts to move.", SettingApplies.OnTrack, SettingPreview.Input)));
            const string vibOff = "Vibration is off.";
            var test = Def("controls.pad.testVibration", "Test vibration", SettingKind.Action, "Plays a short rumble on the connected controller at the current intensity.", SettingApplies.Instant, SettingPreview.Vibration);
            test.options = new[] { "Test" };
            pad.sections.Add(Section("Feedback",
                Toggle(SettingKeys.Vibration, "Vibration", true, "Rumble from kerbs, wheel slip and gear shifts.", SettingApplies.OnTrack, SettingPreview.Vibration),
                When(Slider(SettingKeys.VibrationIntensity, "Vibration intensity", 0, 100, 5, 70, "%", "Strength of all rumble effects.", SettingApplies.OnTrack, SettingPreview.Vibration), SettingKeys.Vibration, ConditionOp.Equals, 0, vibOff),
                When(test, SettingKeys.Vibration, ConditionOp.Equals, 0, vibOff)));
            c.pages.Add(kbm);
            c.pages.Add(pad);
            return c;
        }

        static SettingCategory Gameplay()
        {
            const SettingApplies T = SettingApplies.OnTrack;
            const SettingPreview A = SettingPreview.Assists;
            var preset = Choice(SettingKind.Segmented, SettingKeys.AssistPreset, "Assist preset", new[] { "Off", "Medium", "Full", "Custom" }, 1,
                "Sets all driving assists at once. Changing one assist switches to Custom.", T, A);
            return Category("gameplay", "Gameplay", "Assists, rules and opponents. These apply from your next session on track.", preset,
                Section("Driving assists",
                    Choice(SettingKind.Selector, SettingKeys.TractionControl, "Traction control", new[] { "Off", "Medium", "Full" }, 1, "Limits wheelspin under power.", T, A, true),
                    Toggle(SettingKeys.Abs, "Anti-lock brakes", true, "Stops the wheels locking under heavy braking.", T, A),
                    Choice(SettingKind.Selector, SettingKeys.BrakingAssist, "Braking assist", new[] { "Off", "Low", "Medium", "High" }, 0, "Brakes for you before corners.", T, A, true),
                    Toggle(SettingKeys.SteeringAssist, "Steering assist", false, "Gently steers toward the racing line.", T, A),
                    Choice(SettingKind.Selector, SettingKeys.RacingLine, "Racing line", new[] { "Off", "Corners only", "Full" }, 1, "Shows the ideal line, coloured by when to brake.", T, A),
                    Choice(SettingKind.Segmented, SettingKeys.Gearbox, "Gearbox", new[] { "Automatic", "Manual" }, 0, "Manual uses the Shift up and Shift down bindings.", T, A),
                    Toggle(SettingKeys.PitAssist, "Pit assist", true, "Handles the pit entry speed limit and box stop.", T, A)),
                Section("Simulation",
                    Choice(SettingKind.Selector, SettingKeys.Damage, "Car damage", new[] { "Off", "Simplified", "Full" }, 1, "How collisions affect the car's handling and parts.", T, A, true),
                    Slider(SettingKeys.AiDifficulty, "AI difficulty", 0, 110, 1, 70, null, "Pace of the other drivers. 100 matches the fastest real-world laps.", T, A),
                    Toggle(SettingKeys.TyreWear, "Tyre wear", true, "Tyres lose grip as they wear.", T, A),
                    Toggle(SettingKeys.FuelUsage, "Fuel usage", true, "Fuel burns over the race and the car gets lighter.", T, A),
                    Choice(SettingKind.Selector, SettingKeys.RulesAndFlags, "Rules & flags", new[] { "Off", "Reduced", "Full" }, 2, "Track limits, penalties and flag rules.", T, A, true)));
        }

        static SettingCategory Camera()
        {
            const SettingApplies T = SettingApplies.OnTrack;
            return Category("camera", "Camera", "Views on track and in the showroom.", null,
                Section("Default view",
                    Choice(SettingKind.Selector, SettingKeys.StartingCamera, "Starting camera", new[] { "Cockpit", "T-cam", "Nose", "Chase near", "Chase far" }, 3,
                        "The view you start each session in. Change camera switches views while driving.", T, SettingPreview.CockpitCamera)),
                Section("Cockpit",
                    Slider(SettingKeys.CockpitFov, "Field of view", 60, 110, 1, 77, "°", "Wider shows more of the track; narrower makes the car feel bigger and the speed lower.", T, SettingPreview.CockpitCamera),
                    Slider(SettingKeys.HeadMovement, "Head movement", 0, 100, 5, 50, "%", "How much the driver's head moves under braking, cornering and kerbs.", T, SettingPreview.CockpitCamera)),
                Section("Chase",
                    Slider(SettingKeys.ChaseDistance, "Distance", 0, 100, 5, 50, "%", "How far behind the car the camera sits.", T, SettingPreview.ChaseCamera),
                    Slider(SettingKeys.ChaseHeight, "Height", 0, 100, 5, 40, "%", "How high above the car the camera sits.", T, SettingPreview.ChaseCamera),
                    Slider(SettingKeys.ChaseFov, "Field of view", 50, 100, 1, 70, "°", "Lens width of the chase cameras.", T, SettingPreview.ChaseCamera)),
                Section("Motion",
                    Slider(SettingKeys.CameraShake, "Camera shake", 0, 100, 5, 60, "%", "Vibration from kerbs, bumps and engine. Lower it if motion makes you uncomfortable.", T, SettingPreview.Shake),
                    Toggle(SettingKeys.SpeedEffect, "Speed effect", true, "Widens the view slightly at high speed.", T, SettingPreview.CockpitCamera),
                    Slider(SettingKeys.LookSensitivity, "Look sensitivity", 10, 200, 5, 100, "%", "Free-look speed with the mouse or right stick.", T, SettingPreview.CockpitCamera),
                    Toggle(SettingKeys.ShowroomMotion, "Showroom camera motion", true, "The lobby camera's slow drift and its follow of your mouse. Turn it off for a completely still showroom.", SettingApplies.Instant, SettingPreview.Showroom)));
        }

        static SettingCategory Interface()
        {
            const SettingApplies T = SettingApplies.OnTrack;
            const SettingPreview H = SettingPreview.Hud;
            const string hudOff = "The HUD is hidden.";
            return Category("interface", "Interface", "HUD, messages and menus.", null,
                Section("HUD",
                    Toggle(SettingKeys.Hud, "Show HUD", true, "All on-track displays. Off hides everything for screenshots and replays.", T, H),
                    When(Slider(SettingKeys.HudScale, "HUD scale", 60, 120, 5, 100, "%", "Size of the on-track displays.", T, H), SettingKeys.Hud, ConditionOp.Equals, 0, hudOff),
                    Choice(SettingKind.Segmented, SettingKeys.SpeedUnits, "Speed units", new[] { "km/h", "mph" }, 0, "Used everywhere speed or distance appears, including the track length in the lobby.", SettingApplies.Instant, H),
                    When(Choice(SettingKind.Selector, SettingKeys.Speedometer, "Speedometer", new[] { "Digital", "Dial", "Dial + digital" }, 2, "How speed and revs are shown.", T, H), SettingKeys.Hud, ConditionOp.Equals, 0, hudOff),
                    When(Toggle(SettingKeys.Telemetry, "Telemetry", true, "Throttle and brake traces beside the speedometer.", T, H), SettingKeys.Hud, ConditionOp.Equals, 0, hudOff),
                    When(Toggle(SettingKeys.PositionAndLap, "Position & lap", true, "Race position and lap counter.", T, H), SettingKeys.Hud, ConditionOp.Equals, 0, hudOff),
                    When(Toggle(SettingKeys.TrackMap, "Track map", true, "Mini map with the positions of nearby cars.", T, H), SettingKeys.Hud, ConditionOp.Equals, 0, hudOff)),
                Section("Messages",
                    Toggle(SettingKeys.Notifications, "Notifications", true, "Short messages such as “Settings applied” and race-control updates.", SettingApplies.Instant, H),
                    Toggle(SettingKeys.Subtitles, "Subtitles", true, "Text for team radio and race engineer calls.", T, H),
                    When(Choice(SettingKind.Selector, SettingKeys.SubtitleSize, "Subtitle size", new[] { "Small", "Medium", "Large" }, 1, "Size of subtitle text.", T, H, true), SettingKeys.Subtitles, ConditionOp.Equals, 0, "Subtitles are off.")),
                Section("Menus",
                    Choice(SettingKind.Selector, SettingKeys.InterfaceAnimations, "Interface animations", new[] { "Full", "Reduced", "Off" }, 0,
                        "Menu transitions and lobby reveals. Reduced keeps them short; Off switches instantly.", SettingApplies.Instant, SettingPreview.Animation)));
        }

        // ------------------------------------------------------------------ presets
        static SettingPreset GraphicsPreset()
        {
            var p = new SettingPreset
            {
                presetKey = SettingKeys.GraphicsPreset,
                keys = new[] { SettingKeys.RenderScale, SettingKeys.Upscaling, SettingKeys.AntiAliasing, SettingKeys.TextureQuality, SettingKeys.Anisotropic,
                    SettingKeys.ShadowQuality, SettingKeys.LightingQuality, SettingKeys.AmbientOcclusion, SettingKeys.Reflections,
                    SettingKeys.PostProcessing, SettingKeys.Bloom, SettingKeys.MotionBlur, SettingKeys.DepthOfField, SettingKeys.FilmGrain }
            };
            p.levels.Add(new PresetValues { values = new double[] { 75, 1, 1, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 0 } });   // Low
            p.levels.Add(new PresetValues { values = new double[] { 90, 1, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1, 0, 1 } });   // Medium
            p.levels.Add(new PresetValues { values = new double[] { 100, 1, 2, 2, 4, 3, 2, 2, 2, 1, 1, 1, 0, 1 } });  // High (defaults)
            p.levels.Add(new PresetValues { values = new double[] { 100, 2, 3, 2, 4, 4, 2, 2, 2, 1, 1, 2, 1, 1 } });  // Ultra
            return p;
        }

        static SettingPreset AssistPreset()
        {
            var p = new SettingPreset
            {
                presetKey = SettingKeys.AssistPreset,
                keys = new[] { SettingKeys.TractionControl, SettingKeys.Abs, SettingKeys.BrakingAssist, SettingKeys.SteeringAssist, SettingKeys.RacingLine, SettingKeys.Gearbox, SettingKeys.PitAssist }
            };
            p.levels.Add(new PresetValues { values = new double[] { 0, 0, 0, 0, 0, 1, 0 } }); // Off
            p.levels.Add(new PresetValues { values = new double[] { 1, 1, 0, 0, 1, 0, 1 } }); // Medium (defaults)
            p.levels.Add(new PresetValues { values = new double[] { 2, 1, 3, 1, 2, 0, 1 } }); // Full
            return p;
        }
    }
}
