#if UNITY_EDITOR
// ============================================================================
// Editor/SetupScene.cs — 一键创建 Main 场景 + PanelSettings
// Unity 菜单: IronCrown > Setup Main Scene
// ============================================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace IronCrown.Editor
{
    public static class SetupScene
    {
        [MenuItem("IronCrown/Setup Main Scene")]
        public static void CreateMainScene()
        {
            // 1. 创建 PanelSettings
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "MainPanelSettings";
            // 必须赋主题样式表，否则运行时 UI 不渲染（黑屏）。用 Unity 默认运行时主题。
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            if (theme != null)
                panelSettings.themeStyleSheet = theme;
            else
                Debug.LogError("[SetupScene] 未找到 UnityDefaultRuntimeTheme.tss —— UI 将不渲染。");

            if (!AssetDatabase.IsValidFolder("Assets/UI/Settings"))
                AssetDatabase.CreateFolder("Assets/UI", "Settings");

            AssetDatabase.CreateAsset(panelSettings, "Assets/UI/Settings/MainPanelSettings.asset");

            // 2. 创建新场景
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            // 3. 创建 Bootstrap GameObject + GameLifetimeScope
            var bootstrap = new GameObject("Bootstrap");
            var scope = bootstrap.AddComponent<IronCrown.Bootstrap.GameLifetimeScope>();

            // 4. 创建 UIDocument
            var uiRoot = new GameObject("UIDocument");
            var uiDoc = uiRoot.AddComponent<UIDocument>();
            uiDoc.panelSettings = panelSettings;

            // 加载 UXML
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainHud.uxml");
            if (uxml != null)
                uiDoc.visualTreeAsset = uxml;
            else
                Debug.LogWarning("[SetupScene] MainHud.uxml not found at Assets/UI/MainHud.uxml");

            // 5. 挂 MainHudBehaviour
            var hudBehaviour = uiRoot.AddComponent<IronCrown.Presentation.MainHudBehaviour>();

            // 6. 保存场景
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");

            // 7. 添加到 Build Settings（可选）
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true)
            };
            EditorBuildSettings.scenes = scenes;

            AssetDatabase.SaveAssets();
            Debug.Log("[SetupScene] Main.unity 场景创建完成。请手动 Play 测试。");
        }
    }
}
#endif
