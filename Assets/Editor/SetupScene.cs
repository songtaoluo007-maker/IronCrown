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

            // 3b. 创建相机：负责每帧清屏。无相机时画面不被清除，UI Toolkit 文字会逐次叠加成重叠乱码。
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            cam.orthographic = true;   // 2D 游戏，正交相机
            cam.orthographicSize = 6f; // P2.3: 24 格(6省×4)铺满 16:9 视口
            cam.transform.position = new Vector3(3f, 2.5f, -10f); // 居中于地图

            // P2.3: 创建 Grid + Tilemap 用于世界空间地图渲染
            var gridGo = new GameObject("MapGrid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f); // 每格 1 单位
            grid.cellLayout = GridLayout.CellLayout.Rectangle;

            var tilemapGo = new GameObject("Tilemap");
            tilemapGo.transform.SetParent(gridGo.transform);
            var tilemap = tilemapGo.AddComponent<Tilemap>();
            var tilemapRenderer = tilemapGo.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = -1; // 地图在 HUD 之下

            // 创建纯白 tile asset 用于运行时着色
            var whiteTile = ScriptableObject.CreateInstance<Tile>();
            whiteTile.name = "WhiteTile";
            // 使用内置白色方块 sprite
            var whiteSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.pict");
            if (whiteSprite != null)
                whiteTile.sprite = whiteSprite;
            else
                Debug.LogWarning("[SetupScene] 未找到内置 UISprite —— tile 可能不显示。");

            // 挂 MapRenderer
            var mapRenderer = tilemapGo.AddComponent<IronCrown.Presentation.MapRenderer>();
            mapRenderer.SetTilemap(tilemap);
            mapRenderer.SetTile(whiteTile);

            // 挂 MapInputController
            var mapInput = camGo.AddComponent<IronCrown.Presentation.MapInputController>();
            // MapInputController 需要运行时初始化

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
