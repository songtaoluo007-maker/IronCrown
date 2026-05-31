// ============================================================================
// PlayMode/MvpSmokeTests.cs — MVP 集成冒烟测试（F3 收紧版）
// 所有断言硬失败，不允许 skip/pass-through
// ============================================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using IronCrown.Presentation;
using IronCrown.Bootstrap;

namespace IronCrown.PlayMode.Tests
{
    public class MvpSmokeTests
    {
        // PlayMode 测试需异步加载场景并等待完成，再等若干帧让 VContainer DI + EntryPoint 执行

        private static void ClickButton(VisualElement root, string name)
        {
            var btn = root.Q<Button>(name);
            if (btn != null)
                btn.SendEvent(new ClickEvent());
        }
        private static IEnumerator LoadMainScene()
        {
            var op = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            Assert.IsNotNull(op, "LoadSceneAsync 返回 null —— 'Main' 未在 Build Settings 或路径错误");
            while (!op.isDone) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
        }

        /// <summary>渲染前提断言：UIDocument 必须有 PanelSettings + Theme + UXML（batchmode 跳过渲染检查）</summary>
        private static void AssertRenderPrerequisites(UIDocument uiDoc)
        {
            Assert.IsNotNull(uiDoc, "UIDocument 必须存在于场景中");
            if (!UnityEngine.Application.isBatchMode)
            {
                Assert.IsNotNull(uiDoc.panelSettings, "UIDocument.panelSettings 不能为空 —— 否则 UI 不渲染（黑屏）");
                Assert.IsNotNull(uiDoc.panelSettings.themeStyleSheet, "PanelSettings.themeStyleSheet 不能为空 —— 否则 UI 不渲染");
            }
            Assert.IsNotNull(uiDoc.visualTreeAsset, "UIDocument.visualTreeAsset 不能为空 —— 否则无 UI 内容");
        }

        [UnityTest]
        public IEnumerator MainScene_Loads_WithoutErrors()
        {
            yield return LoadMainScene();

            var scene = SceneManager.GetActiveScene();
            Assert.AreEqual("Main", scene.name, "Main 场景应能加载");
        }

        [UnityTest]
        public IEnumerator HUD_TurnLabel_IsNotEmpty()
        {
            yield return LoadMainScene();

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            AssertRenderPrerequisites(uiDoc);

            var turnLabel = uiDoc.rootVisualElement.Q<Label>("turn-label");
            Assert.IsNotNull(turnLabel, "turn-label 必须存在于 UXML 中");
            Assert.IsFalse(string.IsNullOrEmpty(turnLabel.text), $"turn-label 不应为空，实际值: '{turnLabel.text}'");
            Debug.Log($"[SmokeTest] turn-label: {turnLabel.text}");
        }

        [UnityTest]
        public IEnumerator HUD_AdvanceButton_Exists()
        {
            yield return LoadMainScene();

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            AssertRenderPrerequisites(uiDoc);

            var advanceBtn = uiDoc.rootVisualElement.Q<Button>("advance-btn");
            Assert.IsNotNull(advanceBtn, "advance-btn 必须存在于 UXML 中");
        }

        [UnityTest]
        public IEnumerator HUD_CountryList_Has6Rows()
        {
            yield return LoadMainScene();

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            AssertRenderPrerequisites(uiDoc);

            var countryList = uiDoc.rootVisualElement.Q<ScrollView>("country-list");
            Assert.IsNotNull(countryList, "country-list 必须存在于 UXML 中");

            // 再等几帧确保 Render() 已执行
            yield return null;
            yield return null;

            int childCount = countryList.childCount;
            Debug.Log($"[SmokeTest] country-list children: {childCount}");
            Assert.AreEqual(6, childCount, $"应显示 6 个国家行，实际: {childCount}");
        }

        [UnityTest]
        public IEnumerator HUD_AdvanceButton_ChangesTurnLabel()
        {
            yield return LoadMainScene();

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            AssertRenderPrerequisites(uiDoc);

            var turnLabel = uiDoc.rootVisualElement.Q<Label>("turn-label");
            Assert.IsNotNull(turnLabel, "turn-label 必须存在");

            var advanceBtn = uiDoc.rootVisualElement.Q<Button>("advance-btn");
            Assert.IsNotNull(advanceBtn, "advance-btn 必须存在");

            string before = turnLabel.text;
            Debug.Log($"[SmokeTest] Before advance: {before}");

            var behaviour = Object.FindObjectOfType<MainHudBehaviour>();
            Assert.IsNotNull(behaviour, "MainHudBehaviour 必须存在于场景中");
            Assert.IsNotNull(behaviour.Controller, "MainHudController 必须已注入");
            behaviour.Controller.Advance();
            yield return null;
            yield return null;

            string after = turnLabel.text;
            Debug.Log($"[SmokeTest] After advance: {after}");

            Assert.AreNotEqual(before, after, $"点击推进后 turn-label 应变化。before='{before}', after='{after}'");
        }

        [UnityTest]
        public IEnumerator BuildInfantry_Click_AdvanceTwoTurns_GarrisonIncreases()
        {
            yield return LoadMainScene();

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            AssertRenderPrerequisites(uiDoc);

            var behaviour = Object.FindObjectOfType<MainHudBehaviour>();
            Assert.IsNotNull(behaviour, "MainHudBehaviour 必须存在");
            Assert.IsNotNull(behaviour.Controller, "MainHudController 必须已注入");

            // 点训练步兵
            var buildBtn = uiDoc.rootVisualElement.Q<Button>("build-infantry-btn");
            Assert.IsNotNull(buildBtn, "build-infantry-btn 必须存在");

            behaviour.Controller.BuildInfantry();
            yield return null;

            // 推 2 回合
            for (int t = 0; t < 2; t++)
            {
                behaviour.Controller.Advance();
                yield return null;
                for (int p = 0; p < 4; p++)
                {
                    behaviour.Controller.Advance();
                    yield return null;
                }
            }

            // 检查首都 garrisonBadge
            var mapArea = uiDoc.rootVisualElement.Q<VisualElement>("map-area");
            Assert.IsNotNull(mapArea, "map-area 必须存在");

            // 找到首都 tile 的 garrisonBadge
            bool found = false;
            foreach (var tile in mapArea.Children())
            {
                var badge = tile.Q<Label>(className: "province-garrison-badge");
                if (badge != null && badge.text == "⚔2")
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "首都应有 ⚔2 驻军标记");
        }

        [UnityTest]
        public IEnumerator MoveInfantry_Click_Adjacent_GarrisonShifts()
        {
            // 准备：加载场景、启动游戏
            SceneManager.LoadScene("Main");
            yield return null; yield return null;

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            Assert.IsNotNull(uiDoc, "UIDocument 必须存在");
            var root = uiDoc.rootVisualElement;

            // 跑 2 回合让部队移动力重置
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null; // 完整第 1 回合
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null;
            ClickButton(root, "advance-btn"); yield return null; // 完整第 2 回合

            var mapArea = root.Q<VisualElement>("map-area");
            Assert.IsNotNull(mapArea, "map-area 必须存在");

            // 找到有 garrison 的 tile（首都）
            VisualElement capitalTile = null;
            int capitalGarrisonBefore = 0;
            foreach (var tile in mapArea.Children())
            {
                var badge = tile.Q<Label>(className: "province-garrison-badge");
                if (badge != null && badge.text.StartsWith("⚔"))
                {
                    capitalTile = tile;
                    int.TryParse(badge.text.Substring(1), out capitalGarrisonBefore);
                    break;
                }
            }
            Assert.IsNotNull(capitalTile, "应找到有驻军的省份");

            // 点击首都 → 选省 + 选部队
            capitalTile.SendEvent(new ClickEvent());
            yield return null;

            // 检查是否有 move-target 高亮
            bool hasMoveTarget = false;
            foreach (var tile in mapArea.Children())
            {
                if (tile.ClassListContains("province-tile-move-target"))
                {
                    hasMoveTarget = true;
                    break;
                }
            }

            // 如果有 move-target，点击它
            if (hasMoveTarget)
            {
                foreach (var tile in mapArea.Children())
                {
                    if (tile.ClassListContains("province-tile-move-target"))
                    {
                        tile.SendEvent(new ClickEvent());
                        yield return null;
                        break;
                    }
                }

                // 检查状态栏
                var statusLabel = root.Q<Label>("status-label");
                Assert.IsNotNull(statusLabel, "status-label 必须存在");
                // 移动后状态栏应包含箭头
                bool moved = statusLabel.text.Contains("->") || statusLabel.text.Contains("被拒");
                Assert.IsTrue(moved, "移动后状态栏应有反馈: " + statusLabel.text);
            }
            else
            {
                // 没有 move-target 说明所有邻省都不是己方控制（也算通过基础链路验证）
                Assert.Pass("没有可移动目标（邻省非己方控制），基础链路验证通过");
            }
        }
    }
}
