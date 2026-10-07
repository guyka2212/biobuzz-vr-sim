using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace VrFsim.EditorTools
{
    /// <summary>
    /// A fresh clone (or a project whose last scene was deleted) opens on an empty untitled scene.
    /// When that happens, open the game's Main scene instead. (It deliberately does not set the
    /// play-mode start scene: that would also hijack the test runner's own scene.)
    /// </summary>
    [InitializeOnLoad]
    static class OpenMainScene
    {
        static OpenMainScene()
        {
            EditorApplication.delayCall += () =>
            {
                var main = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.MainScenePath);
                if (!main) return;

                if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Application.isBatchMode) return;
                var active = SceneManager.GetActiveScene();
                bool emptyUntitled = string.IsNullOrEmpty(active.path) && !active.isDirty && SceneManager.sceneCount == 1;
                if (emptyUntitled) EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Single);
            };
        }

        [MenuItem("VrFsim/Open Main Scene", priority = 0)]
        static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Single);
        }
    }
}
