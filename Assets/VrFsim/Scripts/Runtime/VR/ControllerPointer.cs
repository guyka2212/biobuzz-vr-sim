using UnityEngine;

namespace VrFsim.VR
{
    /// <summary>
    /// A thin laser from a tracked VR controller, shown only while a menu is open, so the player can
    /// point at menu items. (The gamepad can drive every menu as well.)
    /// </summary>
    public class ControllerPointer : MonoBehaviour
    {
        public static bool Visible { get; set; }

        LineRenderer line;

        void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * 2.5f);
            line.startWidth = 0.004f;
            line.endWidth = 0.001f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(0.4f, 0.85f, 1f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mat.color);
            line.sharedMaterial = mat;
        }

        void LateUpdate()
        {
            // Hide when the controller is not tracked (it sits at the origin).
            line.enabled = Visible && transform.localPosition.sqrMagnitude > 1e-6f;
        }
    }
}
