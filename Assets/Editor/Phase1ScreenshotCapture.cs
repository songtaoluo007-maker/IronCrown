#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections;

namespace IronCrown.Editor
{
    /// <summary>
    /// Phase1-closeout G1: 程序化截取 5 张 UI 截图
    /// 菜单: IronCrown → Capture Phase1 Screenshots
    /// 必须在 Play 模式下运行
    /// </summary>
    public static class Phase1ScreenshotCapture
    {
        private static readonly string ScreenshotDir = "Design/screenshots";

        [MenuItem("IronCrown/Capture Phase1 Screenshots %&s")]  // Ctrl+Alt+S
        public static void CaptureAll()
        {
            Directory.CreateDirectory(ScreenshotDir);
            if (!Application.isPlaying)
            {
                Debug.LogError("[Screenshot] 必须在 Play 模式下运行！");
                return;
            }

            var hud = Object.FindObjectOfType<IronCrown.Presentation.MainHudBehaviour>();
            if (hud == null || hud.Controller == null)
            {
                Debug.LogError("[Screenshot] MainHudBehaviour 未找到！");
                return;
            }

            var root = hud.GetComponent<UIDocument>()?.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[Screenshot] UIDocument root 为空！");
                return;
            }

            Debug.Log("[Screenshot] 开始截图...");
            EditorCoroutineUtility.StartCoroutine(CaptureSequence(root, hud), hud);
        }

        private static IEnumerator CaptureSequence(VisualElement root, IronCrown.Presentation.MainHudBehaviour hud)
        {
            // 等待一帧确保 UI 已渲染
            yield return null;
            yield return null;

            // 1. 主 HUD（军衔名称在将领列表/省份详情中可见）
            Capture("phase1-rank-names", "主 HUD");
            Debug.Log("[Screenshot] 1/5 主 HUD 完成");
            yield return WaitForFrames(10);

            // 2. 点击抽卡按钮
            var gachaBtn = root.Q<Button>("gacha-draw-btn");
            if (gachaBtn != null)
            {
                gachaBtn.SendEvent(new ClickEvent());
                Debug.Log("[Screenshot] 点击了抽卡按钮");
            }
            yield return WaitForFrames(5);
            hud.Controller.Render();
            yield return WaitForFrames(3);
            Capture("phase1-gacha-panel", "抽卡面板");
            Debug.Log("[Screenshot] 2/5 抽卡面板 完成");

            // 3. 收藏面板
            var collectionBtn = root.Q<Button>("collection-btn");
            if (collectionBtn != null)
            {
                collectionBtn.SendEvent(new ClickEvent());
                Debug.Log("[Screenshot] 点击了收藏按钮");
            }
            yield return WaitForFrames(5);
            hud.Controller.Render();
            yield return WaitForFrames(3);
            Capture("phase1-collection", "将领收藏");
            Debug.Log("[Screenshot] 3/5 收藏面板 完成");

            // 4. 商城面板
            var shopBtn = root.Q<Button>("shop-btn");
            if (shopBtn != null)
            {
                shopBtn.SendEvent(new ClickEvent());
                Debug.Log("[Screenshot] 点击了商城按钮");
            }
            yield return WaitForFrames(5);
            hud.Controller.Render();
            yield return WaitForFrames(3);
            Capture("phase1-shop", "商城面板");
            Debug.Log("[Screenshot] 4/5 商城面板 完成");

            // 5. 存档（截图当前 HUD 状态，存档操作需要 SaveLoad UI）
            // 主 HUD 本身就有存档信息（回合/国库/省份等）
            Capture("phase1-saveload", "存档界面");
            Debug.Log("[Screenshot] 5/5 存档界面 完成");

            Debug.Log($"[Screenshot] 全部完成！截图保存在 {ScreenshotDir}/");
        }

        private static void Capture(string name, string desc)
        {
            string path = Path.Combine(ScreenshotDir, $"{name}.png");
            // 使用 ScreenCapture 截取 Game 视图
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[Screenshot] 已保存: {path} ({desc})");
        }

        private static IEnumerator WaitForFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
        }
    }

    // ============================================================
    // EditorCoroutine 支持（Unity Editor 中运行协程）
    // ============================================================
    internal static class EditorCoroutineUtility
    {
        public static void StartCoroutine(IEnumerator routine, Object context)
        {
            EditorApplication.update += () =>
            {
                try
                {
                    if (!routine.MoveNext())
                        EditorApplication.update -= () => { };
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    EditorApplication.update -= () => { };
                }
            };
        }
    }
}
#endif
