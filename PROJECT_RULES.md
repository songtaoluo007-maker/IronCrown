# PROJECT_RULES.md — Project Iron Crown 项目宪法

> 本文件是 **Project Iron Crown（铁冠计划）** 的最高约束。所有代码、配置、文档、分支操作与人/AI 协作行为都必须遵守。
> 当任何方案、文档或实现与本文件冲突时，**一律以本文件为准**。本文件为"写死"约束，非经**人类**批准不得修改本节 14 条。

---

## 一、产品与边界

1. 本项目是轻量化移动端国家战争经营回合制手游。
2. 不得照搬任何现有游戏的 UI、美术、文本、数值和系统表达。

## 二、架构与代码

3. 所有核心玩法逻辑必须位于 Simulation 层。
4. UI 只能读取 ViewModel 或调用 Application Service，不得直接修改 Domain。
5. 所有数值必须来自 Config，不得硬编码。
6. 每个核心模块必须有单元测试。

## 三、流程与变更

7. 每次修改必须更新 CHANGELOG.md。
8. 不得为了修复 bug 新建重复系统。
9. 不得未经允许大范围重构。
10. 不得直接修改 main 分支。

## 四、协作分工

11. Codex 负责实现和测试，不负责最终架构决策。
12. OpenClaw 负责批量任务和流程自动化，不负责核心玩法决策。
13. Claude 负责架构设计和最终审查。
14. 人类拥有最终产品方向、数值取舍、体验验收权。

---

## 执行约定（对上述条款的落地说明，可随实践补充，但不得削弱 14 条）

- **架构落地**：本宪法的工程实现见 [`ARCHITECTURE.md`](ARCHITECTURE.md)。规则 3/4/5 由 **Assembly Definition（asmdef）依赖图在编译期强制**，而非仅靠口头约定。
- **变更记录**：规则 7 要求每次改动在 [`CHANGELOG.md`](CHANGELOG.md) 追加条目（日期 + 改动 + 关联规则）。无 CHANGELOG 更新的提交视为不合规。
- **分支纪律**：规则 10——所有改动走特性分支 + PR，合入 `main` 必须通过 Claude 审查与 CI（EditMode 测试 + 配置校验）。
- **重构边界**：规则 9——"大范围重构"指跨层、跨模块或改动公共契约（Contracts / Ports / Config schema）的改动，须先经 Claude 出方案、人类批准。局部实现优化不受限。
- **去重原则**：规则 8——修 bug 前先定位**既有归属模块**（见 ARCHITECTURE.md §3 模块归属表），在原系统内修复，禁止新建平行系统绕过。
- **审查门禁**：每个 PR 的验收清单见 [`ARCHITECTURE.md` 附录 B](ARCHITECTURE.md)。
- **记忆/恢复机制**：[`PROJECT_STATE.md`](PROJECT_STATE.md) 是项目恢复入口——**任何新会话(含换人/换 AI)第一步读它**即可重建全局;**每个工作单审查通过后必须更新它**的当前状态与进度。真相源在仓库 git 跟踪文件(非任何人的私有记忆):宪法见本文件、架构见 ARCHITECTURE、完整时间线见 CHANGELOG、各阶段细节见 WorkOrders/。

## 已确认技术栈决策（人类 2026-05-28 批准）

| 维度 | 决策 |
|------|------|
| 确定性 | Simulation 整数/定标整数优先 + 自定义种子 PRNG，`float` 仅用于表现层 |
| 依赖注入 | VContainer |
| UI 框架 | UI Toolkit |
| 序列化 | Newtonsoft.Json（`com.unity.nuget.newtonsoft-json`） |
