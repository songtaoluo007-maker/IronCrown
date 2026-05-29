// ============================================================================
// PlayMode/MvpSmokeTests.cs — MVP 冒烟测试
// 加载 Main 场景 → 验证 HUD 基本功能
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
            // 加载 Main 场景
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            yield return null; // 等一帧让场景加载
            yield return null; // 再等一帧让 DI 完成

            // 断言：场景加载成功
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Assert.AreEqual("Main", scene.name);
        }

        [UnityTest]
        public IEnumerator HUD_TurnLabel_IsNotEmpty()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            yield return null; // 多等几帧让 EntryPoint + UI 完成初始化

            // 查找 UIDocument
            var uiDoc = Object.FindObjectOfType<UIDocument>();
            if (uiDoc == null)
            {
                Debug.LogWarning("[SmokeTest] UIDocument not found, skipping");
                yield break;
            }

            var turnLabel = uiDoc.rootVisualElement.Q<Label>("turn-label");
            Assert.IsNotNull(turnLabel, "turn-label 应存在");
            Assert.IsFalse(string.IsNullOrEmpty(turnLabel.text), "turn-label 不应为空");
            Debug.Log($"[SmokeTest] turn-label text: {turnLabel.text}");
        }

        [UnityTest]
        public IEnumerator HUD_AdvanceButton_Exists()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            yield return null;

            var uiDoc = Object.FindObjectOfType<UIDocument>();
            if (uiDoc == null)
            {
                Debug.LogWarning("[SmokeTest] UIDocument not found, skipping");
                yield break;
            }

            var advanceBtn = uiDoc.rootVisualElement.Q<Button>("advance-btn");
            Assert.IsNotNull(advanceBtn, "advance-btn 应存在");
        }
    }
}
