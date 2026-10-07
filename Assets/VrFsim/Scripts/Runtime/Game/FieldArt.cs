using UnityEngine;

namespace VrFsim.Game
{
    /// <summary>
    /// Optional art (Blender FBX prefabs) that replaces greybox visuals. Each slot is independent:
    /// an empty slot keeps the greybox for that part. Art never supplies colliders.
    /// Modelling convention: metres, field frame already applied (X right, Y up, Z away from the
    /// audience), origin as noted per slot.
    /// </summary>
    [CreateAssetMenu(menuName = "VrFsim/Field Art")]
    public class FieldArt : ScriptableObject
    {
        public const string ResourceName = "VrFsimFieldArt";

        [Tooltip("Origin at field centre, top of tiles at y = 0.")] public GameObject tiles;
        [Tooltip("Origin at field centre.")] public GameObject perimeter;
        [Tooltip("Origin at field centre.")] public GameObject venue;
        [Tooltip("Origin at field centre.")] public GameObject hiveFrame;
        [Tooltip("Origin at the pivot, bar along +Z, level.")] public GameObject hiveTrayRed;
        [Tooltip("Origin at the pivot, bar along +Z, level.")] public GameObject hiveTrayBlue;
        [Tooltip("Origin at the bore centre on the tiles, +Z into the field.")] public GameObject flower;

        [Header("Scoring elements (unit diameter, origin at centre)")]
        public Mesh pollenMesh;
        public Mesh nectarMesh;

        public static FieldArt Load() => Resources.Load<FieldArt>(ResourceName);
    }
}
