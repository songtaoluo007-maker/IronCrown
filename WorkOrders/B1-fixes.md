# 工作单 B1-FIX — 让玩家真正看到 HUD + 修测试

| 项 | 值 |
|---|---|
| 执行者 | OpenClaw（DeepSeek V4-Pro） |
| 出单/审查 | Claude（规则 13） |
| 分支 | `feature/b1-fixes` |
| 前置 | B1 已提交（核心实现已审查通过） |
| 角色边界 | 规则 12：只实现本单；勿改经济数值/公式；CHANGELOG 写 PR 描述不直接编辑 |

## 背景（Claude 审查 B1）
B1 命令管线/建厂/存档实现**通过**。但用户实测"看不到任何画面"，且 1 个测试红。本单收尾。

## 状态：F1 已由 Claude 手修（勿回退）
- `SetupScene.cs`：① `panelSettings.themeStyleSheet` 改为加载 `UnityDefaultRuntimeTheme.tss`（原 null → 不渲染）；② **新增一台正交相机**（`clearFlags=SolidColor`）——原场景无相机 → 画面不清屏 → UI 文字逐次叠加成重叠乱码（用户实测复现）。**保留这两处修改**。

## 附加发现（B1 代码）
- `MainHudController.Unbind()` 的 `UnregisterCallback(_ => Advance())` 用的是**新 lambda**，与 `Bind` 里 `RegisterCallback` 不是同一引用 → 实际没注销。MVP 单场景影响小，但 OpenClaw 应修：把回调存成字段（如 `EventCallback<ClickEvent> _onAdvance`）再 Register/Unregister 同一引用。

## 必办项

### F2 —[核心] 让玩家真正看到 HUD
根因：场景 UIDocument 的 `PanelSettings = None` + 原 theme 为 null → 黑屏。
- 重新执行菜单 **`IronCrown > Setup Main Scene`**（用已修好的脚本重建 `Main.unity` + 带 theme 的 `MainPanelSettings`，并正确赋给 UIDocument）。
- 若 `Assets/UI/Settings/MainPanelSettings.asset` 有重复/旧的（themeStyleSheet=null），删除旧的，确保场景引用的是带 theme 的那个。
- **手动 Play `Main` 场景**，确认屏幕真实显示：顶栏「回合 1 · TurnStart」+「推进」按钮 + 6 国列表 + B1 的**选国控件 + 建民用厂/建军用厂按钮**。**截图为证**（这是验收硬条件，不接受"测试绿"代替截图）。

### F3 —[防回归] 冒烟测试补"真渲染"前提断言
现有 PlayMode 冒烟只查内存 VisualElement 树，没查渲染前提 → 黑屏却测试绿。
- 在 `MvpSmokeTests`（或新测试）加断言：`uiDoc.panelSettings != null`、`uiDoc.panelSettings.themeStyleSheet != null`、`uiDoc.visualTreeAsset != null`。任一为空即失败。

### F4 —[测试 bug] 修 SetPlayerCountry_ChangesPlayer
现状：测试用空 `StubConfigRepository` 建世界（无任何国家），却 `SetPlayerCountry("republic_west")`；产品代码 `SetPlayerCountry` 正确校验"国家须存在"→ 空世界不设置 → 断言失败。**产品代码正确，勿改产品**，改测试：
- 让该测试用能提供国家的配置（参考 `SaveLoadEquivalenceTests` 里的 `TestConfigRegistry`，`Register` 两个 `CountryConfig`：`empire_north`、`republic_west`，`ideology` 给合法枚举名、`resources` 给非 null 空字典），独立构造 `GameSessionService`，`NewGame(playerCountryId:"empire_north")` 后 `SetPlayerCountry("republic_west")`，断言 `PlayerCountryId == "republic_west"`。
- 不破坏同文件其他测试（共享 SetUp 的 `StubConfigRepository` 保持原样，仅此测试单独构造）。

### F5 —[验证] 真跑 + 证据
- 重跑全部 EditMode（应回到 0 失败，计数随 F4 不变或 +0），导出**本次新** `artifacts/editmode-results.xml`。
- 重跑 PlayMode（含 F3 新断言），导出 `artifacts/playmode-results.xml`，全绿。
- F2 的 Play 截图附 PR。

## 验收门禁（DoD）
- [ ] 手动 Play `Main`：屏幕真实显示 HUD（顶栏+推进+6国+选国+建造按钮）——**截图为证**。
- [ ] 点选国 + 建民用厂 → 资本扣减、"在建"+1 → 推进 3 回合 → 民用厂 +1（截图或日志）。
- [ ] EditMode 全绿（SetPlayerCountry 转绿）；PlayMode 全绿（含渲染前提断言）。
- [ ] 未改 `SetupScene` theme 修复；未改产品 `SetPlayerCountry` 逻辑；未改经济数值。
- [ ] PR 在 `feature/b1-fixes`；changelog 写 PR 描述。

## 歧义处理
PanelSettings 重建/引用方式若有多解 → 选"Play 后屏幕真显示 HUD"的那种，以截图为准。其它 → `[需 Claude 决策]`。
