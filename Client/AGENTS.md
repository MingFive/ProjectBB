# ProjectBB 项目协作规则

## 作用范围与路径

本文件是 ProjectBB 客户端协作入口，适用于本目录及其子目录。用户当前明确要求优先于项目中的历史说明；本文不覆盖上级系统或工具权限。

- WorkspaceRoot：D:/ProjectBB/Client，本文件所在工作根。
- ContextRoot：WorkspaceRoot/.ProjectBB_context，唯一项目上下文目录。
- ProjectRoot：D:/ProjectBB/Client/ProjectBB，已核对存在 Assets、Packages、ProjectSettings；实际版本与验证状态见 99_Current_State.md。
- RepositoryRoot：按用户指定及明确授权核对；不根据 Client 名称或工作根猜测 Git 仓库位置。
- 工程建立后在 99_Current_State.md 记录实际 ProjectRoot。若建于子目录，其 AGENTS 仅补充工程约束并引用本文件，不复制整套上下文。
- 本文链接相对 WorkspaceRoot；主题文档中的链接相对各自所在目录。Assets/... 在工程建立后才解释为 ProjectRoot 下的路径。

## 工作方式

- 先判断任务是解释、方案、诊断、文档还是实现。方案讨论不自动授权编写实现；已经授权的常规修改完整执行，不重复确认。
- 实现前先分析问题，说明设计、受影响系统和修改影响，再提供或修改代码；说明长短与任务复杂度匹配。
- 优先读取可访问的相关文件，不要求用户重复上传。确实缺失时列出最少必要文件与影响，不编造 API、方法、字段、事件、配置或运行行为。
- 已确定设计与当前源码不一致时，检查差异并记录。普通文档过时可直接修正；改变玩法、破坏数据兼容或扩大任务范围时先明确所需决策。
- 尊重用户已有修改，最小化无关变化；既有大文件局部补丁，不顺带大规模改名、格式化或重构。
- 用户要求代码示例时，小改动给相关片段，大改动给完整实现。直接编辑文件后给出变化、验证与链接，不重复粘贴整份源码。
- 不因整阶段方案获得认可就一次性安装插件、复制旧工程或完成所有后续阶段；以当前任务授权范围执行。

## 上下文阅读

涉及项目架构、代码或研发时，先读以下基础文档；同一任务内已读且未变化可复用：

- [00 项目概述](.ProjectBB_context/00_Project_Summary.md)
- [10 架构](.ProjectBB_context/10_Architecture.md)
- [20 Unity 与 C# 规范](.ProjectBB_context/20_Unity_Convention.md)
- [80 禁止项](.ProjectBB_context/80_Do_Not_Do.md)
- [99 当前状态](.ProjectBB_context/99_Current_State.md)

按任务补读相关主题完整章节，工具输出截断时继续读取必要部分：

| 领域 | 文档 |
| --- | --- |
| 协议、本地服务、账号、存档 | [15_GameServer_System.md](.ProjectBB_context/15_GameServer_System.md) |
| 相机、二维表现、渲染、平台性能 | [25_Render_Foundation.md](.ProjectBB_context/25_Render_Foundation.md) |
| 技能、Runner、技能物、技能编辑器 | [30_Skill_System.md](.ProjectBB_context/30_Skill_System.md) |
| 小球运动、反弹、接触、贴附、时钟 | [35_Ball_Movement_And_Collision.md](.ProjectBB_context/35_Ball_Movement_And_Collision.md) |
| 配置、JSON、SO、单位、导表 | [40_Content_Config.md](.ProjectBB_context/40_Content_Config.md) |
| Buff、临时状态、持续伤害 | [50_Buff_System.md](.ProjectBB_context/50_Buff_System.md) |
| Trait、卡牌、构筑 | [60_Card_Trait_System.md](.ProjectBB_context/60_Card_Trait_System.md) |
| 候选、选择、抽卡业务 | [61_Card_Draw_System.md](.ProjectBB_context/61_Card_Draw_System.md) |
| 命中、互伤批次、承伤、吸血、死亡 | [70_Damage_Pipeline.md](.ProjectBB_context/70_Damage_Pipeline.md) |
| 研发计划、改造决策、执行历史 | [96_Development_Record.md](.ProjectBB_context/96_Development_Record.md) |
| 验收、回归、运行缺口 | [98_Acceptance_Plan.md](.ProjectBB_context/98_Acceptance_Plan.md) |

上下文表达有效设计，实际 API 和实现状态以当前源码与验证证据为准。上下文中的计划类型不代表已经存在。

ContextRoot/Project Instructions.md 仅作兼容导航；ContextRoot/AGENTS.md 仅引导本文件，不形成重复规则源。

## 文件与外部参考

- 搜索优先使用 rg，并显式指定与任务相关的目录，不默认递归扫描整个磁盘或其他项目。
- 默认工作范围为 WorkspaceRoot 和用户明确指定的材料/输出目录，修改仍须符合工具权限。
- 当前参考材料包括 D:/ProjectBB/AIContext/简介.md、D:/ProjectBB/AIContext/ARPG context，以及用户提供的 D:/rogueblade/Client/AGENTS.md；它们是只读参考，不是 ProjectBB 的另一个生效指令入口。
- 参考文档中的其他路径和执行命令不自动授权跨项目读取、修改或运行。需要源工程实现时，按用户当前指定的范围读取，不猜测文件位置或接口。
- 不修改 ARPG 原工程和原上下文来完成 ProjectBB 的任务，不沿用其框架迁移执行清单。
- 生成的临时产物放到明确输出位置，不混入长期上下文；不为每次对话新建零散总结。

## Git 与资产

- 用户未明确要求时不执行 Git 命令，不读取或修改 .git，也不使用 Git GUI 代替。
- 查询授权不等于提交、切分支、合并、推送或删除授权；操作限定于明确仓库和范围。
- 不因整理文档顺带移动工程、删除目录、创建 worktree 或导入旧资产。
- 实际资产迁入保留 .meta 和 GUID，不无故重建 Prefab/Scene/SO 引用，不批量修改序列化字段或枚举序号。

## 核心架构约束

- LogicCore 纯 C#；几何与公式不接收 Unity 或 GamePlay 对象。GamePlay 管战斗规则，View 管表现，SceneInit 管装配。
- BattleSession 统一战斗时钟和生命周期；不能让多个 Update/Tick 重复驱动同一状态。
- 运动与接触是战斗逻辑事实，Transform 是投影。球球碰撞统一裁决，不由双方分别重复结算。
- CombatManager 管承伤、治疗、状态与死亡；SkillManager 管技能和命中转发，CombatManager 不反向依赖 SkillManager。
- 同次球球互伤必须完成双方既定请求后再提交死亡；具体规则按 70，不使用回调先后决定胜负。
- Buff 是临时状态，Trait 是长期能力。Trait 不重写技能内部碰撞、索敌和伤害。
- JSON 数值与 SO 时间线职责分开；SkillEditor 不写回数值配置。
- 正式编码前核对命名、Unity 对象判断、集合修改、生命周期与中文注释规则，详见 20。

## 文档维护

- 稳定设计写回对应主题；禁止项写 80；研发与重构决策、实施和证据写 96；验收写 98；当前事实写 99。
- 只有 ContextRoot 一份上下文，不复制 ARPG 主题全文，不新建重复的迁移上下文。
- 明确区分设计已确定、待实施、已实现待验证、已验证、待决策。历史完成记录不替代当前验证。
- 修改相关系统时同步必要文档，不机械刷新全部主题。
- 文档使用 UTF-8，中文读取显式指定 -Encoding UTF8。

## 验证与交付

- 选择与改动相称的验证：文档检查路径和一致性；几何、去重、批次、生命周期等核心逻辑要有针对性验证。
- 不执行 dotnet build 或寻找 .sln 代替 Unity 编译。Unity 编译与运行以实际 Editor 结果为准；无法执行时明确留待验证。
- 静态检查、编译、Play Mode、平台构建与真机性能分别记录，不夸大通过范围。
- 文档变更不启动 Unity；当前项目尚无工程时不宣称测试场景运行通过。
- 交付说明实际变化、验证结果、必要的未决事项，并链接主要文件。
