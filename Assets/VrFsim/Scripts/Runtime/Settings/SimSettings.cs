using System;
using System.Collections.Generic;
using UnityEngine;

namespace VrFsim.Settings
{
    public enum GameMode { Match, FreeDrive }
    public enum DriveControlMode { Arcade, Tank }
    public enum StickSide { Left, Right }
    public enum PracticeSeat { None, Dummy }
    public enum AutoRoutine { DoNothing, Leave, LaunchPreloadsAndLeave, LaunchPreloadsAndPark }
    public enum StartAnchor { WallNearAudience, WallCenter, WallBehindLoadingZone, AudienceWall, Custom }
    public enum StationSlot { NearAudience, FarFromAudience }
    public enum CameraView { DriverStation, Overhead, Chase, RobotPov, Free }
    public enum QualityPreset { Low, Medium, High, Custom }
    public enum Msaa { Off = 1, X2 = 2, X4 = 4 }
    public enum ShadowLevel { Off, Low, High }
    /// <summary>Which moving objects cast realtime shadows (the field's lighting is baked).</summary>
    public enum DynamicShadows { Off, RobotsOnly, RobotsAndElements }
    public enum Venue { Arena, Gym, Night }
    public enum PerfOverlay { Off, Fps, Full }

    [Serializable]
    public class AssistSettings
    {
        public bool fieldCentric = true;
        public bool aimAssist = true;
        public bool autoIntake = true;
        public bool autoFire = false;
        public DriveControlMode controlMode = DriveControlMode.Arcade;
        [Range(10, 100)] public float slowModePercent = 30f;

        public void Validate() => slowModePercent = Mathf.Clamp(slowModePercent, 10f, 100f);
    }

    [Serializable]
    public class ControlSettings
    {
        public StickSide driveStick = StickSide.Left;
        [Range(0f, 0.4f)] public float deadzone = 0.12f;
        /// <summary>Response exponent: 1 = linear, 2 = squared (finer control near centre).</summary>
        [Range(1f, 3f)] public float curve = 1f;
        [Range(0.05f, 0.95f)] public float triggerThreshold = 0.35f;
        /// <summary>Input System binding overrides (InputActionAsset.SaveBindingOverridesAsJson).</summary>
        public string bindingOverridesJson = "";

        public void Validate()
        {
            deadzone = Mathf.Clamp(deadzone, 0f, 0.4f);
            curve = Mathf.Clamp(curve, 1f, 3f);
            triggerThreshold = Mathf.Clamp(triggerThreshold, 0.05f, 0.95f);
            bindingOverridesJson ??= "";
        }
    }

    [Serializable]
    public class CustomPose
    {
        public float xIn = -60f, yIn = 0f, headingDeg = 0f;
    }

    [Serializable]
    public class MatchSettings
    {
        public GameMode mode = GameMode.Match;
        public Alliance alliance = Alliance.Red;
        public StartAnchor start = StartAnchor.WallCenter;
        public CustomPose customStart = new CustomPose();
        public List<CustomPose> savedStarts = new List<CustomPose>();
        public AutoRoutine auto = AutoRoutine.LaunchPreloadsAndLeave;
        public PracticeSeat partner = PracticeSeat.None;
        public PracticeSeat opponent1 = PracticeSeat.None;
        public PracticeSeat opponent2 = PracticeSeat.None;
        public bool showEventLog = true;
        public bool enforcePenalties = true;

        public void Validate()
        {
            customStart ??= new CustomPose();
            savedStarts ??= new List<CustomPose>();
            if (savedStarts.Count > 8) savedStarts.RemoveRange(8, savedStarts.Count - 8);
        }
    }

    /// <summary>
    /// Values FIRST does not publish. Each default is a documented assumption
    /// (Docs/biobuzz-facts.md, "NOT published by FIRST") and is user-adjustable.
    /// </summary>
    [Serializable]
    public class RuleAssumptions
    {
        /// <summary>
        /// HIVE tip model: an up-CELL tips when pollen × 1 + nectar × nectarWeight ≥ tipLoad.
        /// The two official calibration points (8 POLLEN; 3 POLLEN + 3 NECTAR) give
        /// tipLoad = 8 and nectarWeight = 5/3.
        /// </summary>
        public float tipLoadPollen = 8f;
        public float nectarWeight = 5f / 3f;
        public float hiveSwingSeconds = 3f;
        public float pollenMassGrams = 30f;
        public float nectarMassGrams = 55f;
        public bool parkOwnLoadingZoneOnly = true;

        public void Validate()
        {
            tipLoadPollen = Mathf.Clamp(tipLoadPollen, 2f, 20f);
            nectarWeight = Mathf.Clamp(nectarWeight, 0.5f, 5f);
            hiveSwingSeconds = Mathf.Clamp(hiveSwingSeconds, 0.5f, 8f);
            pollenMassGrams = Mathf.Clamp(pollenMassGrams, 10f, 120f);
            nectarMassGrams = Mathf.Clamp(nectarMassGrams, 10f, 200f);
        }
    }

    [Serializable]
    public class SoundSettings
    {
        [Range(0, 1)] public float master = 0.8f;
        [Range(0, 1)] public float fieldCues = 1f;
        [Range(0, 1)] public float robot = 0.7f;
        [Range(0, 1)] public float launch = 0.8f;
        [Range(0, 1)] public float intake = 0.6f;
        [Range(0, 1)] public float hive = 0.8f;
        [Range(0, 1)] public float alerts = 0.9f;
        [Range(0, 1)] public float voice = 1f;
        public bool voiceCues = true;

        public void Validate()
        {
            master = Mathf.Clamp01(master); fieldCues = Mathf.Clamp01(fieldCues); robot = Mathf.Clamp01(robot);
            launch = Mathf.Clamp01(launch); intake = Mathf.Clamp01(intake); hive = Mathf.Clamp01(hive);
            alerts = Mathf.Clamp01(alerts); voice = Mathf.Clamp01(voice);
        }
    }

    [Serializable]
    public class VisualSettings
    {
        public QualityPreset preset = QualityPreset.Medium;
        [Range(0.6f, 1.4f)] public float renderScale = 1f;
        public Msaa msaa = Msaa.X4;
        public ShadowLevel shadows = ShadowLevel.Low;
        public DynamicShadows dynamicShadows = DynamicShadows.RobotsOnly;
        public float targetRefreshRate = 90f;
        public Venue venue = Venue.Arena;
        public bool reducedMotion = true;
        public bool minimap = false;
        public PerfOverlay perfOverlay = PerfOverlay.Off;

        public void Validate()
        {
            renderScale = Mathf.Clamp(renderScale, 0.6f, 1.4f);
            targetRefreshRate = Mathf.Clamp(targetRefreshRate, 60f, 144f);
        }

        public void ApplyPreset(QualityPreset p)
        {
            preset = p;
            switch (p)
            {
                case QualityPreset.Low:
                    renderScale = 0.8f; msaa = Msaa.Off; shadows = ShadowLevel.Off; dynamicShadows = DynamicShadows.Off; break;
                case QualityPreset.Medium:
                    renderScale = 1f; msaa = Msaa.X2; shadows = ShadowLevel.Low; dynamicShadows = DynamicShadows.RobotsOnly; break;
                case QualityPreset.High:
                    renderScale = 1.2f; msaa = Msaa.X4; shadows = ShadowLevel.High; dynamicShadows = DynamicShadows.RobotsAndElements; break;
            }
        }
    }

    [Serializable]
    public class CameraSettings
    {
        public CameraView view = CameraView.DriverStation;
        public StationSlot station = StationSlot.NearAudience;
        /// <summary>0 = use the headset's tracked height; otherwise force eye height (inches).</summary>
        public float eyeHeightOverrideIn = 0f;
        public float chaseDistanceIn = 60f;
        public float chaseHeightIn = 40f;
        public float overheadHeightIn = 160f;
        public bool comfortVignette = true;

        public void Validate()
        {
            eyeHeightOverrideIn = eyeHeightOverrideIn <= 0f ? 0f : Mathf.Clamp(eyeHeightOverrideIn, 36f, 84f);
            chaseDistanceIn = Mathf.Clamp(chaseDistanceIn, 24f, 160f);
            chaseHeightIn = Mathf.Clamp(chaseHeightIn, 12f, 120f);
            overheadHeightIn = Mathf.Clamp(overheadHeightIn, 80f, 300f);
        }
    }

    /// <summary>Everything the player can change. Persisted as one JSON file.</summary>
    [Serializable]
    public class SimSettings
    {
        public int version = 1;
        public RobotConfig robot = RobotPresets.Default();
        public List<RobotConfig> savedRobots = new List<RobotConfig>();
        public AssistSettings assists = new AssistSettings();
        public ControlSettings controls = new ControlSettings();
        public MatchSettings match = new MatchSettings();
        public RuleAssumptions rules = new RuleAssumptions();
        public SoundSettings audio = new SoundSettings();
        public VisualSettings graphics = new VisualSettings();
        public CameraSettings camera = new CameraSettings();

        public const int MaxSavedRobots = 12;

        public void Validate()
        {
            robot ??= RobotPresets.Default();
            savedRobots ??= new List<RobotConfig>();
            assists ??= new AssistSettings();
            controls ??= new ControlSettings();
            match ??= new MatchSettings();
            rules ??= new RuleAssumptions();
            audio ??= new SoundSettings();
            graphics ??= new VisualSettings();
            camera ??= new CameraSettings();

            robot.Validate();
            savedRobots.RemoveAll(r => r == null);
            if (savedRobots.Count > MaxSavedRobots) savedRobots.RemoveRange(MaxSavedRobots, savedRobots.Count - MaxSavedRobots);
            foreach (var r in savedRobots) r.Validate();
            assists.Validate(); controls.Validate(); match.Validate(); rules.Validate();
            audio.Validate(); graphics.Validate(); camera.Validate();
        }

        public SimSettings Clone() => JsonUtility.FromJson<SimSettings>(JsonUtility.ToJson(this));
    }
}
