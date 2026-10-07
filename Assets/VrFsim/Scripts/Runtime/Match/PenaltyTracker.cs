using System.Collections.Generic;
using UnityEngine;
using VrFsim.Game;
using VrFsim.Robot;

namespace VrFsim.Match
{
    /// <summary>
    /// Rules the simulator can judge automatically. Each foul is credited to the opponent of the
    /// violating alliance. Rules that are prevented structurally (G403/G404 movement, G407 storage
    /// cap, G408 opponent NECTAR, G426/G427 human entry) never reach this class.
    /// </summary>
    public class PenaltyTracker
    {
        public delegate void FoulHandler(Alliance violator, bool major, string rule, string detail);
        public event FoulHandler Foul;

        readonly HashSet<RobotController> g402Called = new HashSet<RobotController>();
        readonly Dictionary<(RobotController, RobotController), PinState> pins = new Dictionary<(RobotController, RobotController), PinState>();

        class PinState
        {
            public float count;      // seconds of active pin
            public int majorsCalled;
            public float separatedFor;
            public Vector3 victimStart;
        }

        public void Reset()
        {
            g402Called.Clear();
            pins.Clear();
        }

        /// <summary>G410: a NECTAR entered a FLOWER before the last 60 s.</summary>
        public void OnNectarEnteredFlower(GameElement e, Flower f, float secondsRemaining, bool flowersOpen)
        {
            if (flowersOpen || !e.Kind.IsNectar() || e.LastController == null) return;
            Foul?.Invoke(e.LastController.Value, true, "G410", $"NECTAR into FLOWER F{f.index + 1} with {secondsRemaining:0}s left");
        }

        /// <summary>G402: during AUTO a robot fully on the opponent's half that touches an opponent robot.</summary>
        public void TickAuto(IReadOnlyList<RobotController> robots)
        {
            foreach (var r in robots)
            {
                if (g402Called.Contains(r) || !r.Footprint().FullyOnOpponentSide(r.Alliance)) continue;
                foreach (var o in robots)
                {
                    if (o.Alliance == r.Alliance || !Touching(r, o)) continue;
                    g402Called.Add(r);
                    Foul?.Invoke(r.Alliance, true, "G402", "disrupted the opponent's AUTO");
                    break;
                }
            }
        }

        /// <summary>
        /// G421 (pins): a robot pressing an opponent that is not moving, for more than 3 s, earns a
        /// MAJOR, plus another for every further 3 s. The count pauses while the robots are 2 ft apart
        /// and ends after 3 s of separation.
        /// </summary>
        public void TickPins(IReadOnlyList<RobotController> robots, float dt)
        {
            foreach (var a in robots)
                foreach (var b in robots)
                {
                    if (a == b || a.Alliance == b.Alliance) continue;
                    var key = (a, b);
                    bool touching = Touching(a, b);
                    if (!pins.TryGetValue(key, out var st))
                    {
                        if (!touching || !Pressing(a, b)) continue;
                        st = new PinState { victimStart = b.transform.position };
                        pins[key] = st;
                    }

                    float sep = Vector3.Distance(a.transform.position, b.transform.position) - Units.In((a.Config.lengthIn + b.Config.lengthIn) * 0.5f);
                    bool victimMovedAway = Vector3.Distance(b.transform.position, st.victimStart) > Units.In(24f);
                    if (sep > Units.In(24f) || victimMovedAway || !touching)
                    {
                        st.separatedFor += dt;
                        if (st.separatedFor > 3f) pins.Remove(key);
                        continue;
                    }
                    st.separatedFor = 0f;
                    if (!Pressing(a, b)) continue;
                    st.count += dt;
                    int due = st.count > 3f ? 1 + Mathf.FloorToInt((st.count - 3f) / 3f) : 0;
                    while (st.majorsCalled < due)
                    {
                        st.majorsCalled++;
                        Foul?.Invoke(a.Alliance, true, "G421", st.majorsCalled == 1 ? "PIN longer than 3 s" : "PIN continued");
                    }
                }
        }

        static bool Touching(RobotController a, RobotController b)
        {
            float reach = Units.In((Mathf.Max(a.Config.lengthIn, a.Config.widthIn) + Mathf.Max(b.Config.lengthIn, b.Config.widthIn)) * 0.5f * 1.05f);
            if (Vector3.Distance(a.transform.position, b.transform.position) > reach) return false;
            var ca = a.Rig.body.GetComponent<Collider>();
            var cb = b.Rig.body.GetComponent<Collider>();
            return Physics.ComputePenetration(ca, ca.transform.position, ca.transform.rotation,
                       cb, cb.transform.position, cb.transform.rotation, out _, out _) ||
                   Vector3.Distance(ca.ClosestPoint(cb.transform.position), cb.ClosestPoint(ca.transform.position)) < Units.In(0.5f);
        }

        /// <summary>Is <paramref name="a"/> driving into a nearly stationary <paramref name="b"/>?</summary>
        static bool Pressing(RobotController a, RobotController b)
        {
            Vector3 to = b.transform.position - a.transform.position; to.y = 0f;
            var cmd = a.LastCommand;
            Vector3 drive = a.transform.TransformDirection(new Vector3(cmd.translate.x, 0f, cmd.translate.y));
            if (!cmd.robotCentric && a.Assists != null && a.Assists.fieldCentric)
                drive = SimWorld.DriverRight(a.Alliance) * cmd.translate.x + SimWorld.DriverForward(a.Alliance) * cmd.translate.y;
            bool pushing = drive.magnitude > 0.2f && Vector3.Dot(drive.normalized, to.normalized) > 0.5f;
            return pushing && b.Velocity.magnitude < Units.In(3f);
        }
    }
}
