// ============================================================================
// PlayMode/MvpSmokeTests.cs — MVP 集成冒烟测试（F3 收紧版）
// 所有断言硬失败，不允许 skip/pass-through
// ============================================================================

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IronCrown.PlayMode.Tests
{
    public class MvpSmokeTests
    {
        [UnityTest]
        public IEnumerator MainScene_Loads_WithoutErrors()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            yield return null;
            yield return null;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Assert.AreEqual("Main", scene.name, "Main 场景应能加载");
        }

        [UnityTest]
        public IEnumerator HUD_TurnLabel_IsNotEmpty()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            // 等待场景加载 + VContainer DI + EntryPoint 执行
            for (int i = 0; i < 10; i++) yield return null;

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            Assert.IsNotNull(uiDoc, "UIDocument 必须存在于场景中");

            var turnLabel = uiDoc.rootVisualElement.Q<Label>("turn-label");
            Assert.IsNotNull(turnLabel, "turn-label 必须存在于 UXML 中");
            Assert.IsFalse(string.IsNullOrEmpty(turnLabel.text), $"turn-label 不应为空，实际值: '{turnLabel.text}'");
            Debug.Log($"[SmokeTest] turn-label: {turnLabel.text}");
        }

        [UnityTest]
        public IEnumerator HUD_AdvanceButton_Exists()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            for (int i = 0; i < 10; i++) yield return null;

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            Assert.IsNotNull(uiDoc, "UIDocument 必须存在于场景中");

            var advanceBtn = uiDoc.rootVisualElement.Q<Button>("advance-btn");
            Assert.IsNotNull(advanceBtn, "advance-btn 必须存在于 UXML 中");
        }

        [UnityTest]
        public IEnumerator HUD_CountryList_Has6Rows()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            for (int i = 0; i < 10; i++) yield return null;

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            Assert.IsNotNull(uiDoc, "UIDocument 必须存在于场景中");

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
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            for (int i = 0; i < 10; i++) yield return null;

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            Assert.IsNotNull(uiDoc, "UIDocument 必须存在于场景中");

            var turnLabel = uiDoc.rootVisualElement.Q<Label>("turn-label");
            Assert.IsNotNull(turnLabel, "turn-label 必须存在");

            var advanceBtn = uiDoc.rootVisualElement.Q<Button>("advance-btn");
            Assert.IsNotNull(advanceBtn, "advance-btn 必须存在");

            string before = turnLabel.text;
            Debug.Log($"[SmokeTest] Before advance: {before}");

            // 模拟点击推进按钮
            advanceBtn.SendEvent(new ClickEvent());
            yield return null;
            yield return null;

            string after = turnLabel.text;
            Debug.Log($"[SmokeTest] After advance: {after}");

            Assert.AreNotEqual(before, after, $"点击推进后 turn-label 应变化。before='{before}', after='{after}'");
        }
    }
}
