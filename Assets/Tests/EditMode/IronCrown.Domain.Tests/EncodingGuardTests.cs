// ============================================================================
// EncodingGuardTests.cs — UTF-8 编码守卫测试
// T5 Phase 0: 确保关键文件为严格 UTF-8 编码
// ============================================================================

using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace IronCrown.Domain.Tests
{
    [TestFixture]
    public class EncodingGuardTests
    {
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [Test]
        public void ChangeLog_IsValidUtf8()
        {
            AssertFileIsValidUtf8(Path.Combine(ProjectRoot, "CHANGELOG.md"));
        }

        [Test]
        public void ProjectRules_IsValidUtf8()
        {
            AssertFileIsValidUtf8(Path.Combine(ProjectRoot, "PROJECT_RULES.md"));
        }

        [Test]
        public void Architecture_IsValidUtf8()
        {
            AssertFileIsValidUtf8(Path.Combine(ProjectRoot, "ARCHITECTURE.md"));
        }

        [Test]
        public void AllJsonConfigs_AreValidUtf8()
        {
            var jsonDir = Path.Combine(Application.dataPath, "StreamingAssets", "Configs", "Json");
            Assert.IsTrue(Directory.Exists(jsonDir), $"配置目录不存在: {jsonDir}");
            foreach (var file in Directory.GetFiles(jsonDir, "*.json"))
            {
                AssertFileIsValidUtf8(file);
            }
        }

        private static void AssertFileIsValidUtf8(string path)
        {
            Assert.IsTrue(File.Exists(path), $"文件不存在: {path}");
            var bytes = File.ReadAllBytes(path);
            Assert.DoesNotThrow(() =>
            {
                // 尝试用严格 UTF-8 解码，任何无效字节都会抛 DecoderFallbackException
                StrictUtf8.GetString(bytes);
            }, $"文件不是有效 UTF-8 编码: {Path.GetFileName(path)}");
        }
    }
}
