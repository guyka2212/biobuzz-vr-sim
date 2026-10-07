using System;
using System.Collections.Generic;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Settings;

namespace VrFsim.Robot
{
    /// <summary>Supplies a robot's command each frame: the driver, an AUTO routine, or nothing.</summary>
    public interface ICommandSource
    {
        DriverCommand Read(RobotController robot);
    }

    /// <summary>
    /// One robot on the field: drive physics, intake, storage, launcher, Box Tube, passing.
    /// Robot frame: +Z forward, +X right, +Y up.
    /// </summary>
    public class RobotController : MonoBehaviour
    {
        public RobotConfig Config { get; private set; }
        public Alliance Alliance { get; private set; }
        public RobotRig Rig { get; private set; }
        public AssistSettings Assists { get; private set; }
        public bool IsPlayer { get; private set; }
        public ICommandSource Source { get; set; }

        /// <summary>Drive and mechanisms respond to commands. When false the robot brakes.</summary>
        public bool Enabled { get; set; } = true;
        public bool TractionMode { get; private set; }
        public bool RampDeployed { get; private set; }
        public bool FlipFront { get; private set; }

        /// <summary>AUTO routines aim with AprilTags regardless of the driver's assist setting.</summary>
        public bool ForceAimAssist { get; set; }
        public bool AimAssist => Assists.aimAssist || ForceAimAssist;

        public DriverCommand LastCommand { get; private set; }
        public Vector3 Velocity => Rig.body.linearVelocity;

        public event Action<RobotController, GameElement> Intaken, Launched, Placed;

        GameElement[] slots;
        DriverCommand latched;
        float nextIntake, nextShot, placeBusyUntil, dumpUntil;
        float moduleAngle;
        bool hasAimSolution;
        const float IntakeInterval = 0.12f, TurretInterval = 0.22f, DumperInterval = 0.12f, PlaceTime = 0.5f;

        public void Init(RobotConfig config, Alliance alliance, RobotRig rig, AssistSettings assists, bool isPlayer)
        {
            Config = config; Alliance = alliance; Rig = rig; Assists = assists; IsPlayer = isPlayer;
            slots = new GameElement[config.storage];
            SimWorld.Robots.Add(this);
        }

        void OnDestroy()
        {
            SimWorld.Robots.Remove(this);
            if (slots != null)
                foreach (var e in slots) if (e) e.Release(e.transform.position, Vector3.zero, false);
        }

        // ── Storage ─────────────────────────────────────────────────────────────────────────

        public int StoredCount { get { int n = 0; foreach (var e in slots) if (e) n++; return n; } }
        public bool HasSpace => StoredCount < slots.Length;
        public IEnumerable<GameElement> Stored { get { foreach (var e in slots) if (e) yield return e; } }

        public bool Accepts(ElementKind k) =>
            !k.IsNectar() || (k.NectarAlliance() == Alliance && Config.CarriesNectar); // G408: never opponent NECTAR

        public bool Store(GameElement e)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i]) continue;
                slots[i] = e;
                e.Grab(this, Alliance, Rig.storageSlots[i]);
                return true;
            }
            return false;
        }

        Collider[] ownColliders;

        /// <summary>An element leaving the robot must not collide with the robot that just released it.</summary>
        void Detach(GameElement e, float seconds = 0.35f)
        {
            ownColliders ??= GetComponentsInChildren<Collider>();
            foreach (var c in ownColliders) if (c) Physics.IgnoreCollision(e.Collider, c, true);
            StartCoroutine(Reattach(e, seconds));
        }

        System.Collections.IEnumerator Reattach(GameElement e, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!e) yield break;
            foreach (var c in ownColliders) if (c) Physics.IgnoreCollision(e.Collider, c, false);
        }

        GameElement Take(Func<GameElement, bool> pick)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] && pick(slots[i])) { var e = slots[i]; slots[i] = null; return e; }
            return null;
        }

        /// <summary>Empty storage (elements are released where they are). Used by match reset.</summary>
        public void DropAll()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]) { slots[i].Release(slots[i].transform.position, Vector3.zero, false); slots[i] = null; }
        }

        // ── Pose ────────────────────────────────────────────────────────────────────────────

        public void PlaceAt(Vector3 world, float yawDeg)
        {
            var rb = Rig.body;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(world, Quaternion.Euler(0f, yawDeg, 0f));
            rb.position = world;
            rb.rotation = transform.rotation;
            TractionMode = false; RampDeployed = false; FlipFront = false;
            SetDeployed(false);
        }

        /// <summary>Starting configuration (stowed, R102) or expanded (R105).</summary>
        public void SetDeployed(bool deployed) =>
            RobotBuilder.SetUpperHeight(Rig, Config, deployed ? Config.heightIn : Config.stowHeightIn);

        public FieldObb Footprint()
        {
            var f = transform.forward; var r = transform.right;
            return new FieldObb
            {
                center = Units.ToFieldPlane(transform.position),
                axisX = new Vector2(r.x, r.z).normalized,
                axisY = new Vector2(f.x, f.z).normalized,
                halfX = Config.widthIn * 0.5f,
                halfY = Config.lengthIn * 0.5f,
            };
        }

        // ── Frame loop ──────────────────────────────────────────────────────────────────────

        void Update()
        {
            var cmd = Source != null ? Source.Read(this) : DriverCommand.Idle;
            // Continuous inputs take the latest value; edge-triggered ones are held until FixedUpdate consumes them.
            latched.translate = cmd.translate; latched.turn = cmd.turn; latched.useTank = cmd.useTank;
            latched.tankLeft = cmd.tankLeft; latched.tankRight = cmd.tankRight; latched.robotCentric = cmd.robotCentric;
            latched.intake = cmd.intake; latched.outtake = cmd.outtake; latched.fire = cmd.fire; latched.slow = cmd.slow;
            latched.placePollen |= cmd.placePollen; latched.placeNectar |= cmd.placeNectar; latched.humanNectar |= cmd.humanNectar;
            latched.rampToggle |= cmd.rampToggle; latched.pass |= cmd.pass;
            latched.driveModeToggle |= cmd.driveModeToggle; latched.flipFront |= cmd.flipFront;
            AnimateWheels(Time.deltaTime);
        }

        void FixedUpdate()
        {
            var cmd = latched;
            latched.placePollen = latched.placeNectar = latched.humanNectar = latched.rampToggle = latched.pass =
                latched.driveModeToggle = latched.flipFront = false;
            if (!Enabled) cmd = DriverCommand.Idle;
            LastCommand = cmd;

            if (cmd.driveModeToggle && Config.drivetrain == DrivetrainType.Butterfly) TractionMode = !TractionMode;
            if (cmd.flipFront) FlipFront = !FlipFront;
            if (cmd.rampToggle && Config.intakeKind == IntakeKind.Ramp) RampDeployed = !RampDeployed;

            float dt = Time.fixedDeltaTime;
            Drive(cmd, dt);
            if (!Enabled) return;
            AimTurrets(dt);
            RunIntake(cmd);
            RunLauncher(cmd);
            if (cmd.placePollen) PlaceWithBoxTube(false);
            if (cmd.placeNectar) PlaceWithBoxTube(true);
            if (cmd.pass) Pass();
            UpdateBoxTubeVisual();
            if (Rig.ramp) Rig.ramp.localRotation = Quaternion.Slerp(Rig.ramp.localRotation,
                Quaternion.LookRotation(Rig.mouths[0].outward) * Quaternion.Euler(RampDeployed ? 70f : 0f, 0f, 0f), 10f * dt);
        }

        // ── Drive ───────────────────────────────────────────────────────────────────────────

        void Drive(DriverCommand cmd, float dt)
        {
            var dp = DriveParams.For(Config, TractionMode);
            BodyTarget tgt = default;
            if (cmd.useTank && !dp.holonomic)
            {
                float k = cmd.slow ? Assists.slowModePercent / 100f : 1f;
                tgt = DriveKinematics.TankTarget(dp, cmd.tankLeft * k, cmd.tankRight * k);
            }
            else
            {
                float strafe, fwd;
                bool fieldCentric = Assists.fieldCentric && dp.holonomic && !cmd.robotCentric;
                if (fieldCentric)
                {
                    Vector3 world = SimWorld.DriverRight(Alliance) * cmd.translate.x + SimWorld.DriverForward(Alliance) * cmd.translate.y;
                    Vector3 local = transform.InverseTransformDirection(world);
                    strafe = local.x; fwd = local.z;
                }
                else
                {
                    strafe = cmd.translate.x; fwd = cmd.translate.y;
                    if (FlipFront) { strafe = -strafe; fwd = -fwd; }
                }
                float turn = cmd.turn;
                if (cmd.slow)
                {
                    float k = Assists.slowModePercent / 100f;
                    strafe *= k; fwd *= k; turn *= k;
                }
                tgt = DriveKinematics.Target(dp, strafe, fwd, turn);
            }

            if (dp.swerve) ApplySwerveSteering(ref tgt, dt);

            var rb = Rig.body;
            Vector3 vLocal = transform.InverseTransformDirection(rb.linearVelocity);
            float g = Units.Gravity, m = dp.massKg;
            float ax = DriveKinematics.AxisAccel(vLocal.x, tgt.x, dp.vStrafe, dp.fStrafe / m, dp.muLat * g, dt);
            float az = DriveKinematics.AxisAccel(vLocal.z, tgt.y, dp.vFwd, dp.fFwd / m, dp.muFwd * g, dt);
            var a = new Vector2(ax, az);
            float maxA = Mathf.Max(dp.muFwd, dp.muLat) * g;
            if (a.sqrMagnitude > maxA * maxA) a = a.normalized * maxA;

            float w = rb.angularVelocity.y;
            float alphaMotor = dp.fFwd * dp.turnRadius / dp.yawInertia;
            float alphaTraction = dp.muFwd * m * g * dp.turnRadius / dp.yawInertia;
            float aw = DriveKinematics.AxisAccel(w, tgt.w, dp.omegaMax, alphaMotor, alphaTraction, dt);

            rb.AddForce(transform.TransformDirection(new Vector3(a.x, 0f, a.y)), ForceMode.Acceleration);
            rb.AddTorque(Vector3.up * aw, ForceMode.Acceleration);
        }

        /// <summary>Swerve modules must physically turn before the robot can move in a new direction.</summary>
        void ApplySwerveSteering(ref BodyTarget tgt, float dt)
        {
            var v = new Vector2(tgt.x, tgt.y);
            if (v.sqrMagnitude < 1e-4f) return;
            float want = Mathf.Atan2(v.x, v.y);
            float err = Mathf.DeltaAngle(moduleAngle * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            // A module can also reverse its wheel instead of turning more than 90°.
            if (Mathf.Abs(err) > Mathf.PI * 0.5f) err -= Mathf.Sign(err) * Mathf.PI;
            const float steerRate = 14f;
            moduleAngle += Mathf.Clamp(err, -steerRate * dt, steerRate * dt);
            float aligned = Mathf.Max(0f, Mathf.Cos(Mathf.DeltaAngle(moduleAngle * Mathf.Rad2Deg, want * Mathf.Rad2Deg) * Mathf.Deg2Rad));
            float speed = v.magnitude * Mathf.Abs(aligned);
            tgt.x = Mathf.Sin(want) * speed;
            tgt.y = Mathf.Cos(want) * speed;
            foreach (var mod in Rig.swerveModules) mod.localRotation = Quaternion.Euler(0f, moduleAngle * Mathf.Rad2Deg, 0f);
        }

        void AnimateWheels(float dt)
        {
            if (Rig == null || Rig.wheels.Count == 0) return;
            Vector3 vLocal = transform.InverseTransformDirection(Rig.body.linearVelocity);
            float spin = vLocal.z / (DriveParams.WheelDiameter * 0.5f) * Mathf.Rad2Deg * dt;
            foreach (var w in Rig.wheels) w.Rotate(Vector3.right, spin, Space.Self);
        }

        // ── Intake ──────────────────────────────────────────────────────────────────────────

        void RunIntake(DriverCommand cmd)
        {
            if (cmd.outtake) { Outtake(); return; }
            bool on = cmd.intake || (IsPlayer && Assists.autoIntake);
            if (!on || !HasSpace || Time.time < nextIntake) return;

            foreach (var mouth in Rig.mouths)
            {
                foreach (var e in GameElement.All)
                {
                    if (!e.IsFree || !Accepts(e.Kind)) continue;
                    if (e.State == ElementState.Launched && Time.time - e.LaunchedAt < 0.4f) continue;
                    Vector3 p = transform.InverseTransformPoint(e.Position) - mouth.center;
                    Vector3 h = mouth.halfSize;
                    float r = e.RadiusM * 0.5f;
                    if (Mathf.Abs(p.x) > h.x + r || Mathf.Abs(p.y) > h.y + r || Mathf.Abs(p.z) > h.z + r) continue;
                    if (Store(e)) { nextIntake = Time.time + IntakeInterval; Intaken?.Invoke(this, e); return; }
                }
                if (TryFlowerRetrieval(mouth)) return;
            }
        }

        /// <summary>Side rollers, or a deployed ramp, can pull the bottom POLLEN out of a FLOWER.</summary>
        bool TryFlowerRetrieval(IntakeMouth mouth)
        {
            if (Config.intakeKind == IntakeKind.Sweeper) return false;
            if (Config.intakeKind == IntakeKind.Ramp && !RampDeployed) return false;
            var field = SimWorld.Field;
            if (field == null) return false;
            Vector2 mouthTip = Units.ToFieldPlane(transform.TransformPoint(mouth.center + mouth.outward * (mouth.halfSize.z * 0.5f)));
            foreach (var f in field.flowers)
            {
                if ((mouthTip - f.centerIn).magnitude > 5f) continue;
                // The mouth must face the FLOWER's open side.
                Vector3 outWorld = transform.TransformDirection(mouth.outward);
                if (Vector2.Dot(new Vector2(outWorld.x, outWorld.z), -f.inward) < 0.7f) continue;
                var e = f.BottomPollen();
                if (e && Store(e)) { nextIntake = Time.time + IntakeInterval * 2f; Intaken?.Invoke(this, e); return true; }
            }
            return false;
        }

        void Outtake()
        {
            if (Time.time < nextIntake || Rig.mouths.Count == 0) return;
            var e = Take(_ => true);
            if (!e) return;
            var m = Rig.mouths[0];
            Vector3 pos = transform.TransformPoint(m.center + m.outward * Units.In(2f));
            e.Release(pos, transform.TransformDirection(m.outward) * 1.2f + Rig.body.linearVelocity, false); Detach(e);
            nextIntake = Time.time + IntakeInterval * 2f;
        }

        // ── Launcher ────────────────────────────────────────────────────────────────────────

        Vector3 HiveTarget()
        {
            var hive = SimWorld.Field?.HiveOf(Alliance);
            return hive ? hive.UpCellAimPoint() : transform.position + transform.forward * 2f + Vector3.up;
        }

        void AimTurrets(float dt)
        {
            hasAimSolution = false;
            if (Rig.turrets.Length == 0)
            {
                if (Rig.dumperExit)
                {
                    Vector3 dir = transform.TransformDirection(Rig.dumperDirLocal);
                    Vector3 to = HiveTarget() - Rig.dumperExit.position; to.y = 0f;
                    hasAimSolution = to.magnitude > 0.3f && Vector3.Angle(dir, to) < 6f;
                }
                return;
            }
            Vector3 target = HiveTarget();
            for (int i = 0; i < Rig.turrets.Length; i++)
            {
                var t = Rig.turrets[i];
                Vector3 to = target - t.position; to.y = 0f;
                Quaternion want = AimAssist && to.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(to)
                    : transform.rotation;
                t.rotation = Quaternion.RotateTowards(t.rotation, want, 540f * dt);
                if (i == 0) hasAimSolution = AimAssist && Quaternion.Angle(t.rotation, want) < 2.5f;
            }
        }

        void RunLauncher(DriverCommand cmd)
        {
            if (Time.time < dumpUntil && Time.time >= nextShot) { FireDumperOne(); return; }
            bool autoFire = IsPlayer && Assists.autoFire && hasAimSolution;
            if (!(cmd.fire || autoFire) || Time.time < nextShot || StoredCount == 0) return;
            if (Config.launcher == LauncherKind.Dumper)
            {
                dumpUntil = Time.time + DumperInterval * (StoredCount + 1);
                FireDumperOne();
                return;
            }
            FireTurret();
        }

        void FireTurret()
        {
            bool twin = Config.launcher == LauncherKind.DoubleTurret;
            // A single turret feeds POLLEN only; held NECTAR waits for the Box Tube.
            var e = Take(x => x.Kind == ElementKind.Pollen) ?? (twin ? Take(x => x.Kind.IsNectar()) : null);
            if (!e) return;
            int idx = twin && e.Kind.IsNectar() ? 1 : 0;
            var exit = Rig.turretExits[Mathf.Min(idx, Rig.turretExits.Length - 1)];
            Vector3 from = exit.position, target = HiveTarget();
            if (!AimAssist)
            {
                Vector3 flat = target - from; flat.y = 0f;
                Vector3 heading = exit.forward; heading.y = 0f; heading.Normalize();
                target = from + heading * flat.magnitude;
                target.y = HiveTarget().y;
            }
            Ballistics.ApexShot(from, target, Mathf.Max(target.y, from.y) + 0.25f, out var v);
            if (AimAssist) v -= Rig.body.linearVelocity; // shoot-on-the-move compensation
            v = Vector3.ClampMagnitude(Ballistics.Disperse(v, 0.02f, 0.8f), Ballistics.MaxLaunchSpeed);
            e.Release(from, v + Rig.body.linearVelocity, true); Detach(e);
            nextShot = Time.time + TurretInterval;
            Launched?.Invoke(this, e);
        }

        void FireDumperOne()
        {
            var e = Take(_ => true);
            if (!e) { dumpUntil = 0f; return; }
            Vector3 from = Rig.dumperExit.position;
            Vector3 dir = transform.TransformDirection(Rig.dumperDirLocal); dir.y = 0f; dir.Normalize();
            Vector3 to = HiveTarget() - from;
            float dist = Vector3.Dot(new Vector3(to.x, 0f, to.z), dir);
            float elev = Config.hoodDeg * Mathf.Deg2Rad;
            Ballistics.FixedAngleSpeed(Mathf.Max(dist, 0.2f), to.y, elev, out float speed);
            Vector3 v = (dir * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)) * speed;
            v = Ballistics.Disperse(v, 0.02f, 1f);
            e.Release(from + Vector3.up * Units.In(1f), v + Rig.body.linearVelocity, true); Detach(e);
            nextShot = Time.time + DumperInterval;
            Launched?.Invoke(this, e);
        }

        void Pass()
        {
            if (Time.time < nextShot) return;
            var e = Take(x => x.Kind == ElementKind.Pollen) ?? Take(_ => true);
            if (!e) return;
            Vector3 from = Rig.turretExits.Length > 0 ? Rig.turretExits[0].position
                : Rig.dumperExit ? Rig.dumperExit.position : transform.position + Vector3.up * Units.In(Config.heightIn);
            Vector3 target = SimWorld.AllianceToWorld(Alliance, Config.passTargetIn, e.RadiusIn);
            Ballistics.ApexShot(from, target, Mathf.Max(from.y, target.y) + 0.7f, out var v);
            e.Release(from, Vector3.ClampMagnitude(v, Ballistics.MaxLaunchSpeed), true); Detach(e);
            nextShot = Time.time + TurretInterval;
            Launched?.Invoke(this, e);
        }

        // ── Box Tube ────────────────────────────────────────────────────────────────────────

        public Vector3 BoxTubePlacePoint()
        {
            if (!Rig.boxTube) return transform.position;
            Vector3 outward = Rig.boxTube.forward;
            Vector3 p = Rig.boxTube.position + outward * Units.In(4f);
            p.y = Units.In(FieldSpec.FlowerTopRingTop + 2f);
            return p;
        }

        void PlaceWithBoxTube(bool nectar)
        {
            if (!Rig.boxTube || Time.time < placeBusyUntil) return;
            var e = nectar ? Take(x => x.Kind.IsNectar()) : Take(x => x.Kind == ElementKind.Pollen);
            if (!e) return;
            placeBusyUntil = Time.time + PlaceTime;
            Vector3 p = BoxTubePlacePoint();
            Vector2 pIn = Units.ToFieldPlane(p);
            var field = SimWorld.Field;
            if (field != null)
                foreach (var f in field.flowers)
                    if ((pIn - f.centerIn).magnitude <= 2.2f)
                    {
                        p = Units.Field(f.centerIn.x, f.centerIn.y, FieldSpec.FlowerTopRingTop + e.RadiusIn + 0.3f);
                        break;
                    }
            e.Release(p, Vector3.zero, false); Detach(e);
            Placed?.Invoke(this, e);
        }

        void UpdateBoxTubeVisual()
        {
            if (!Rig.boxTubeTip) return;
            bool extending = Time.time < placeBusyUntil;
            float stow = Config.stowHeightIn - RobotBuilder.ChassisHeightIn;
            float ext = FieldSpec.FlowerTopRingTop + 2f - RobotBuilder.ChassisHeightIn;
            float h = extending ? ext : stow;
            var tube = Rig.boxTube.Find("Tube");
            float cur = Rig.boxTubeTip.localPosition.y / Units.MetersPerInch;
            float next = Mathf.MoveTowards(cur, h, 60f * Time.fixedDeltaTime);
            Rig.boxTubeTip.localPosition = new Vector3(0f, Units.In(next), 0f);
            if (tube)
            {
                tube.localPosition = new Vector3(0f, Units.In(next * 0.5f), 0f);
                tube.localScale = new Vector3(Units.In(1.5f), Units.In(next), Units.In(1.5f));
            }
        }
    }
}
