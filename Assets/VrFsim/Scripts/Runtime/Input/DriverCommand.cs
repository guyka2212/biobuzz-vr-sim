using UnityEngine;

namespace VrFsim.Input
{
    /// <summary>
    /// What the driver (or an AUTO routine) asks the robot to do this frame. Sticks are already
    /// shaped (deadzone, response curve). <see cref="translate"/> is x = right, y = forward, in the
    /// driver's frame unless <see cref="robotCentric"/> is set.
    /// </summary>
    public struct DriverCommand
    {
        public Vector2 translate;
        /// <summary>Positive = turn clockwise seen from above (right).</summary>
        public float turn;
        public float tankLeft, tankRight;
        public bool useTank;
        public bool robotCentric;

        public bool intake, outtake, fire, slow;
        // Edge-triggered (true only on the frame the button went down).
        public bool placePollen, placeNectar, humanNectar, rampToggle, pass, driveModeToggle, flipFront;

        public static DriverCommand Idle => default;
    }
}
