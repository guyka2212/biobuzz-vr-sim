namespace VrFsim.Settings
{
    /// <summary>
    /// Starting builds covering each launcher archetype and the main drivetrains. The archetype
    /// mix follows dsim's preset list; the numbers are this project's own tuning.
    /// </summary>
    public static class RobotPresets
    {
        public static RobotConfig[] All()
        {
            return new[]
            {
                new RobotConfig
                {
                    name = "Pollinator", teamName = "Turret + Box Tube",
                    drivetrain = DrivetrainType.Mecanum, lengthIn = 15f, widthIn = 17f, heightIn = 17f, stowHeightIn = 15f,
                    massLb = 27f, driveRpm = 435f,
                    intakeKind = IntakeKind.SideRollers, intakeMount = IntakeMount.Front,
                    launcher = LauncherKind.Turret, launcherMount = MountPos.Center,
                    hasBoxTube = true, boxTubeMount = MountPos.Back,
                    chassisColor = "#2B2F36", accentColor = "#F2B705",
                },
                new RobotConfig
                {
                    name = "Forager", teamName = "Dumper, shifts to push",
                    drivetrain = DrivetrainType.Butterfly, lengthIn = 15f, widthIn = 17f, heightIn = 15f, stowHeightIn = 14f,
                    massLb = 31f, driveRpm = 420f, butterflyTractionRpm = 300f,
                    intakeKind = IntakeKind.Sweeper, intakeMount = IntakeMount.FrontAndBack,
                    launcher = LauncherKind.Dumper, launcherMount = MountPos.Left, hoodDeg = 75f,
                    chassisColor = "#1E3A2B", accentColor = "#9BE15D",
                },
                new RobotConfig
                {
                    name = "Skimmer", teamName = "Double turret on the strafe",
                    drivetrain = DrivetrainType.XDrive, lengthIn = 15f, widthIn = 16f, heightIn = 16f, stowHeightIn = 14f,
                    massLb = 28f, driveRpm = 520f,
                    intakeKind = IntakeKind.Sweeper, intakeMount = IntakeMount.Front,
                    launcher = LauncherKind.DoubleTurret, launcherMount = MountPos.Front, launcherMount2 = MountPos.Back,
                    chassisColor = "#2A2340", accentColor = "#C08CFF",
                },
                new RobotConfig
                {
                    name = "Sniper", teamName = "Single turret, swerve",
                    drivetrain = DrivetrainType.Swerve, lengthIn = 15f, widthIn = 17f, heightIn = 16f, stowHeightIn = 14f,
                    massLb = 27f, driveRpm = 480f,
                    intakeKind = IntakeKind.Ramp, intakeMount = IntakeMount.Front,
                    launcher = LauncherKind.Turret, launcherMount = MountPos.Back,
                    chassisColor = "#3A2A1E", accentColor = "#FF8A3D",
                },
                new RobotConfig
                {
                    name = "Bruiser", teamName = "Tank-drive defender",
                    drivetrain = DrivetrainType.Tank, lengthIn = 18f, widthIn = 18f, heightIn = 14f, stowHeightIn = 13f,
                    massLb = 38f, driveRpm = 340f,
                    intakeKind = IntakeKind.Sweeper, intakeMount = IntakeMount.Front,
                    launcher = LauncherKind.Dumper, launcherMount = MountPos.Back, hoodDeg = 78f,
                    chassisColor = "#3B1F1F", accentColor = "#E8E8E8",
                },
            };
        }

        public static RobotConfig Default()
        {
            var c = All()[0];
            c.name = "My Robot";
            c.teamName = "";
            c.Validate();
            return c;
        }
    }
}
