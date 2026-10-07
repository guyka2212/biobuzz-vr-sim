using UnityEngine;

namespace VrFsim.Game
{
    /// <summary>
    /// Marks a field that was built in the editor and saved into the scene (so its static parts can
    /// have baked lighting). Holds the references the runtime needs.
    /// </summary>
    public class FieldRoot : MonoBehaviour
    {
        public Transform staticRoot;
        public Hive redHive, blueHive;
        public Flower[] flowers = new Flower[FieldSpec.FlowerCount];

        public void Capture(FieldBuilder.Result r)
        {
            staticRoot = r.staticRoot;
            redHive = r.redHive;
            blueHive = r.blueHive;
            flowers = r.flowers;
        }

        public FieldBuilder.Result Rebind() => new FieldBuilder.Result
        {
            root = transform,
            staticRoot = staticRoot,
            redHive = redHive,
            blueHive = blueHive,
            flowers = flowers,
        };
    }
}
