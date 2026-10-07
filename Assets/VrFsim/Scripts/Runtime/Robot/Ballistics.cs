using UnityEngine;

namespace VrFsim.Robot
{
    /// <summary>Drag-free projectile solutions (SI units). Elements are slow and heavy enough
    /// for drag to be a small error over a few metres; launch dispersion covers it.</summary>
    public static class Ballistics
    {
        public const float MaxLaunchSpeed = 10f;

        /// <summary>
        /// Velocity that leaves <paramref name="from"/>, rises to <paramref name="apexHeight"/>
        /// (world Y), and comes down through <paramref name="to"/>. Returns false if the apex is
        /// below either endpoint or the speed exceeds <see cref="MaxLaunchSpeed"/>.
        /// </summary>
        public static bool ApexShot(Vector3 from, Vector3 to, float apexHeight, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            float g = Units.Gravity;
            float up = apexHeight - from.y, down = apexHeight - to.y;
            if (up <= 0f || down < 0f) return false;
            float vy = Mathf.Sqrt(2f * g * up);
            float t = vy / g + Mathf.Sqrt(2f * down / g);
            Vector3 flat = new Vector3(to.x - from.x, 0f, to.z - from.z);
            velocity = flat / t + Vector3.up * vy;
            return velocity.magnitude <= MaxLaunchSpeed;
        }

        /// <summary>
        /// Speed at a fixed elevation (fixed hood) that reaches horizontal distance
        /// <paramref name="distance"/> at height difference <paramref name="rise"/>.
        /// </summary>
        public static bool FixedAngleSpeed(float distance, float rise, float elevationRad, out float speed)
        {
            speed = MaxLaunchSpeed;
            float c = Mathf.Cos(elevationRad), tan = Mathf.Tan(elevationRad);
            float denom = 2f * c * c * (distance * tan - rise);
            if (distance <= 0f || denom <= 0f) return false;
            speed = Mathf.Sqrt(Units.Gravity * distance * distance / denom);
            if (speed > MaxLaunchSpeed) { speed = MaxLaunchSpeed; return false; }
            return true;
        }

        /// <summary>Apply small random speed and direction errors, like a real launcher.</summary>
        public static Vector3 Disperse(Vector3 v, float speedPct, float angleDeg)
        {
            float s = 1f + Random.Range(-speedPct, speedPct);
            var q = Quaternion.AngleAxis(Random.Range(-angleDeg, angleDeg), Vector3.up) *
                    Quaternion.AngleAxis(Random.Range(-angleDeg, angleDeg), Vector3.Cross(v, Vector3.up).normalized);
            return q * v * s;
        }
    }
}
