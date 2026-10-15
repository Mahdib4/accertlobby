namespace Accertone.Settings
{
    /// <summary>
    /// Every stored setting key. Game systems read settings through these, e.g.
    /// <c>AccertoneSettings.Effective(SettingKeys.CockpitFov)</c>.
    /// </summary>
    public static class SettingKeys
    {
        // Display
        public const string Monitor = "display.monitor";
        public const string WindowMode = "display.windowMode";
        public const string Resolution = "display.resolution";
        public const string RefreshRate = "display.refreshRate";
        public const string VSync = "display.vsync";
        public const string FrameRateLimit = "display.frameRateLimit";
        public const string Brightness = "display.brightness";
        public const string Gamma = "display.gamma";
        public const string InterfaceScale = "display.interfaceScale";

        // Graphics
        public const string GraphicsPreset = "graphics.preset";
        public const string RenderScale = "graphics.renderScale";
        public const string Upscaling = "graphics.upscaling";
        public const string AntiAliasing = "graphics.antiAliasing";
        public const string TextureQuality = "graphics.textureQuality";
        public const string Anisotropic = "graphics.anisotropic";
        public const string ShadowQuality = "graphics.shadows";
        public const string LightingQuality = "graphics.lighting";
        public const string AmbientOcclusion = "graphics.ambientOcclusion";
        public const string Reflections = "graphics.reflections";
        public const string PostProcessing = "graphics.postProcessing";
        public const string Bloom = "graphics.bloom";
        public const string MotionBlur = "graphics.motionBlur";
        public const string DepthOfField = "graphics.depthOfField";
        public const string FilmGrain = "graphics.filmGrain";

        // Audio (each volume has "<key>.mute")
        public const string MasterVolume = "audio.master";
        public const string MusicVolume = "audio.music";
        public const string EffectsVolume = "audio.effects";
        public const string EngineVolume = "audio.engine";
        public const string EnvironmentVolume = "audio.environment";
        public const string InterfaceVolume = "audio.interface";
        public const string VoiceVolume = "audio.voice";
        public const string SpeakerMode = "audio.speakerMode";
        public const string MuteInBackground = "audio.muteInBackground";
        public const string MuteSuffix = ".mute";

        // Controls
        public const string KeyboardSteerSpeed = "controls.kb.steerSpeed";
        public const string KeyboardSteerSensitivity = "controls.kb.steerSensitivity";
        public const string KeyboardSteerLinearity = "controls.kb.steerLinearity";
        public const string KeyboardThrottle = "controls.kb.throttleSensitivity";
        public const string KeyboardBrake = "controls.kb.brakeSensitivity";
        public const string PadSteerStick = "controls.pad.steerStick";
        public const string PadSteerSensitivity = "controls.pad.steerSensitivity";
        public const string PadSteerDeadzone = "controls.pad.steerDeadzone";
        public const string PadSteerLinearity = "controls.pad.steerLinearity";
        public const string PadTriggerDeadzone = "controls.pad.triggerDeadzone";
        public const string Vibration = "controls.pad.vibration";
        public const string VibrationIntensity = "controls.pad.vibrationIntensity";
        public const string ControlsDevice = "controls.device";

        // Gameplay (read by race systems)
        public const string AssistPreset = "gameplay.assistPreset";
        public const string TractionControl = "gameplay.tractionControl";
        public const string Abs = "gameplay.abs";
        public const string BrakingAssist = "gameplay.brakingAssist";
        public const string SteeringAssist = "gameplay.steeringAssist";
        public const string RacingLine = "gameplay.racingLine";
        public const string Gearbox = "gameplay.gearbox";
        public const string PitAssist = "gameplay.pitAssist";
        public const string Damage = "gameplay.damage";
        public const string AiDifficulty = "gameplay.aiDifficulty";
        public const string TyreWear = "gameplay.tyreWear";
        public const string FuelUsage = "gameplay.fuelUsage";
        public const string RulesAndFlags = "gameplay.rules";

        // Camera
        public const string StartingCamera = "camera.startView";
        public const string CockpitFov = "camera.cockpitFov";
        public const string HeadMovement = "camera.headMovement";
        public const string ChaseDistance = "camera.chaseDistance";
        public const string ChaseHeight = "camera.chaseHeight";
        public const string ChaseFov = "camera.chaseFov";
        public const string CameraShake = "camera.shake";
        public const string SpeedEffect = "camera.speedEffect";
        public const string LookSensitivity = "camera.lookSensitivity";
        public const string ShowroomMotion = "camera.showroomMotion";

        // Interface
        public const string Hud = "interface.hud";
        public const string HudScale = "interface.hudScale";
        public const string SpeedUnits = "interface.speedUnits";
        public const string Speedometer = "interface.speedometer";
        public const string Telemetry = "interface.telemetry";
        public const string PositionAndLap = "interface.positionLap";
        public const string TrackMap = "interface.trackMap";
        public const string Notifications = "interface.notifications";
        public const string Subtitles = "interface.subtitles";
        public const string SubtitleSize = "interface.subtitleSize";
        public const string InterfaceAnimations = "interface.animations";
    }
}
