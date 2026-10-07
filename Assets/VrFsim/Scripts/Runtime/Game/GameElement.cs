using System.Collections.Generic;
using UnityEngine;

namespace VrFsim.Game
{
    public enum ElementState { Free, Held, Launched }

    /// <summary>
    /// A POLLEN or NECTAR ball. Free elements are ordinary PhysX spheres. A held element is parked
    /// inside a robot (physics off, hidden collider) until it is launched or placed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class GameElement : MonoBehaviour
    {
        public static readonly List<GameElement> All = new List<GameElement>(64);

        public ElementKind Kind { get; private set; }
        public ElementState State { get; private set; }
        public Rigidbody Body { get; private set; }
        public SphereCollider Collider { get; private set; }
        public float RadiusM { get; private set; }

        /// <summary>Alliance of the robot that last held or launched this element (for fouls).</summary>
        public Alliance? LastController { get; private set; }
        public Object Holder { get; private set; }
        public float LaunchedAt { get; private set; }

        Renderer[] renderers;

        public void Init(ElementKind kind, float massKg, PhysicsMaterial physics)
        {
            Kind = kind;
            Body = GetComponent<Rigidbody>();
            Collider = GetComponent<SphereCollider>();
            RadiusM = Units.In(kind.DiameterIn() * 0.5f);
            Collider.radius = 0.5f;
            transform.localScale = Vector3.one * RadiusM * 2f;
            Collider.sharedMaterial = physics;

            Body.mass = massKg;
            // Foam tiles and the perforated shell bleed speed: modest drag, strong rolling resistance.
            Body.linearDamping = 0.15f;
            Body.angularDamping = 1.2f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.maxAngularVelocity = 60f;
            renderers = GetComponentsInChildren<Renderer>();
            State = ElementState.Free;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public Vector3 Position => transform.position;
        public Vector2 FieldPlaneIn => Units.ToFieldPlane(transform.position);
        public float HeightIn => Units.ToIn(transform.position.y);
        public float RadiusIn => Units.ToIn(RadiusM);

        public bool IsFree => State != ElementState.Held;

        /// <summary>Take the element into a robot. It stops simulating and follows <paramref name="slot"/>.</summary>
        public void Grab(Object holder, Alliance alliance, Transform slot)
        {
            State = ElementState.Held;
            Holder = holder;
            LastController = alliance;
            Body.isKinematic = true;
            Collider.enabled = false;
            transform.SetParent(slot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Release from a robot with an initial world velocity.</summary>
        public void Release(Vector3 worldPos, Vector3 velocity, bool launched)
        {
            transform.SetParent(null, true);
            transform.position = worldPos;
            transform.localScale = Vector3.one * RadiusM * 2f;
            Collider.enabled = true;
            Body.isKinematic = false;
            Body.position = worldPos;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.insideUnitSphere * 5f;
            State = launched ? ElementState.Launched : ElementState.Free;
            Holder = null;
            LaunchedAt = Time.time;
        }

        public void SetVisible(bool v)
        {
            foreach (var r in renderers) if (r) r.enabled = v;
        }

        /// <summary>Hard reset for match setup.</summary>
        public void PlaceAt(Vector3 worldPos)
        {
            transform.SetParent(null, true);
            var rot = Random.rotation;
            transform.SetPositionAndRotation(worldPos, rot);
            transform.localScale = Vector3.one * RadiusM * 2f;
            Collider.enabled = true;
            Body.isKinematic = false;
            // With interpolation on, the rigidbody pose is authoritative; set it too or the move is undone.
            Body.position = worldPos;
            Body.rotation = rot;
            Body.WakeUp();
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            State = ElementState.Free;
            Holder = null;
            LastController = null;
            SetVisible(true);
        }

        void OnCollisionEnter(Collision c)
        {
            // A launched element is just another free element once it touches something.
            if (State == ElementState.Launched) State = ElementState.Free;
        }
    }
}
