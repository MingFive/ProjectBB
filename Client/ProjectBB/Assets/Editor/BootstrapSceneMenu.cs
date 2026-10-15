using UnityEditor;
using UnityEditor.SceneManagement;

namespace ProjectBB.Editor
{
    /// <summary>
    /// 提供启动场景导航，仅在编辑模式下执行，不自动进入运行模式。
    /// </summary>
    internal static class BootstrapSceneMenu
    {
        private const string MenuPath = "ProjectBB/打开启动场景";
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        [MenuItem(MenuPath)]
        private static void OpenBootstrapScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanOpenBootstrapScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
