using UnityEngine;
using VrFsim.Audio;
using VrFsim.Game;
using VrFsim.Input;
using VrFsim.Match;
using VrFsim.Settings;
using VrFsim.UI;
using VrFsim.VR;

namespace VrFsim
{
    /// <summary>
    /// The one object the Main scene needs besides the baked field. Creates the systems in
    /// dependency order: input → view (XR rig) → match → audio → displays → menu.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        void Awake()
        {
            if (Benchmark.Requested) SettingsStore.UseTransient(Benchmark.Settings());
            Time.fixedDeltaTime = 1f / 90f;
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 2;
            QualitySettings.vSyncCount = 0;

            var fieldRoot = FindAnyObjectByType<FieldRoot>();
            SimWorld.Field = fieldRoot ? fieldRoot.Rebind() : FieldBuilder.Build(null, MaterialLibrary.Get(), FieldArt.Load());

            Make<InputHub>("Input");
            var view = Make<ViewManager>("View");
            UiKit.EnsureEventSystem(view.Origin.transform);
            Make<MatchController>("Match");
            Make<AudioManager>("Audio");
            Make<GraphicsApplier>("Graphics");
            Make<Hud>("Displays");
            Make<SettingsMenu>("Menu");
            if (Benchmark.Requested) Make<Benchmark>("Benchmark");
        }

        T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void OnApplicationQuit() => SettingsStore.Commit();
    }
}
