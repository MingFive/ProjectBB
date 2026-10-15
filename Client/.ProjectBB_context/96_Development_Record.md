# ProjectBB 研发与重构记录

## 文档职责

记录从零研发的阶段、已确认决策、实施批次、重构原因与验证证据。稳定规则回写对应主题，当前状态汇总到 99，具体验收项维护在 98。

不承接 ARPG 向其他宿主迁移的任务、版本号或历史完成状态。

## 状态口径

- 设计已确定：用户认可方向，可以作为后续实现基线，不代表已经授权一次性完成所有研发阶段。
- 待实施：尚无对应实现。
- 已实现待验证：代码/配置已落地，所需验证未全部完成。
- 已验证：注明具体证据、环境和范围。
- 待决策：存在影响后续实现的具体选项，不能静默假定已确认。

一个批次可通过静态检查但仍待 Unity 编译或真机验证，不能合并写成“全部通过”。

## 阶段路线

| 阶段 | 目标 | 核心产物 | 当前状态 |
| --- | --- | --- | --- |
| 0 上下文与工程基线 | 建立规则、确认工程宿主、建立最小启动 | 上下文、根 AGENTS、Unity 空工程基线 | 原空场景用户反馈运行通过；Bootstrap 已补齐 Unity 编译与 Play Mode 集成，Rider/平台待验证 |
| 1 基础对战 | 两球能够公平完成一场战斗 | 运动、接触、基础互伤、死亡、暂停和重开 | 旧基础对战 38 项 Unity 基线通过；双球 Dynamic 与圆形弹道通过 Unity 集成 |
| 1 扩展：物理基础 | 承接改变物理运动的技能 | Dynamic 迁移、子步接触归并、圆形高速弹道 | DEV-004/005 已验证；DEV-009 多实体召唤已实现待 Unity 验证 |
| 2 四种能力 | 通过配置验证技能/Buff/Trait 组合 | 最小配置加载与引用校验、吸血鬼、一拳超人、提莫、狂战士 | DEV-006 已验证；DEV-009 四种能力最小模块已实现待验证，蘑菇中毒未接入 |
| 3 内容工具 | 完善同类能力的配置制作流程 | 完整校验、导表、必要二维技能编辑器、调试入口 | DEV-009 生成组编辑器最小版本已实现待验证；完整时间线/导表待实施 |
| 4 肉鸽循环 | 构筑跨多场对战 | 候选、选卡、升级、整次尝试与单场边界 | 待玩法细化、待实施 |
| 5 平台与局外业务 | 完成目标平台与产品闭环 | 真机性能、资源生命周期、存档及按需服务端 | 待平台细化、待实施 |

平台早期兼容检查应在工程建立后尽早做，不等到阶段 5 才首次尝试构建；阶段 5 是完整验收与收口。

## 决策记录

### 2026-10-13 用户安装插件后的状态核对

- 用户反馈插件已手动安装并询问下一步。文件核对确认 Newtonsoft JSON 3.2.2，Odin Inspector/Validator 文件和 NodeCanvas（框架常量 3.41）；Spine PackageCache 中仅 spine-csharp 4.3.41，未发现 Unity 集成包/组件。Odin DLL 1.0.0.0 不是已确认的产品版本。
- 插件导入编译未独立验证，未删除或调整用户导入的插件。本次同步 20/40/99 的过时安装状态，建议下一批配置驱动周期弹道闭环，再扩展实体与编辑器；没有实施功能或执行 Unity。

### 2026-10-13 插件准备与大量弹道/召唤物需求

- 用户要求旧插件安装清单并强化 SkillEditor 召唤物编辑，进一步明确联合规模起点约 300 弹道 + 20 个有碰撞/血量的实体召唤物；游戏继续以 60 FPS 为目标，尚无容量达标结论。
- 核对 ProjectBB manifest/lock/Assets 和旧 SkillEditor/Skill 核心直接依赖：Newtonsoft JSON（包名 com.unity.nuget.newtonsoft-json）尚未安装；Odin/NodeCanvas 无核心直接引用。安装建议为先官方 JSON 包、Odin 可选、Spine 按制作版本后接，Navigation/NodeCanvas 等暂无本批必需理由。清单与官方来源记入 20，本次不安装任何包。
- 增补 30 的生成器工作区、三类对象面板、空间排布/分时波次区分、事件子技能链、实体定义、生命周期、只读配置来源和联合压力诊断方案；具体 UI/模块实现仍为提案。
- 35/98 记录多实体、容量、全场物理细分成本及 300+20 联合场景要求；当前双球寻址/128 槽能力未满足新目标，不能只改常量宣称完成。
- 验证仅为本地源码/配置核对、官方安装资料查询与文档检查；未修改运行代码或旧工程，未执行 Unity 编译/Play Mode/平台测试。

### 2026-10-13 旧技能代码审查与编辑器改造提案

- 用户指定只读源码范围：D:/rogueblade/Client/RogueBlade/Assets/Scripts 与 D:/rogueblade/Client/RogueBlade/Assets/Editor，重点 GamePlay。用户确定近期逐份手写配置实现功能，Excel 导表后补；本次要求先提供编辑器方案，没有授权迁入实现。
- 针对性审查：Skill/Data、SkillRunner、SkillManager、CollisionDetector、Trait 配置/触发/实例、Buff 配置和管理入口；Editor/SkillEditor 的窗口、时间轴、注册、撤销、序列化、JSON 加载、预览、动画解析和技能物配置同步。追踪了 Scripts/Common/Config 中 ConfigManager、GameConst、SkillLineSO；并非旧工程全量依赖审计。
- 已核对证据：SkillEditorSerializer.SaveToDisk 保存时间线 SO；JsonLoader 只读 SkillConfig JSON。ClipInspector 还可内联修改 SkillObjConfigSO，Serializer 保存时调用 SyncTrajectoryFromConfig，因此需统一技能物配置来源。
- 已核对证据：EditorConst、GameConst 和 AnimationResolver 使用 30 帧基准；TraitProcessor 使用 Time.time / Time.frameCount；旧 SkillManager.Update 自行 Tick Runner。不能直接复制到 ProjectBB 的 BattleSession 单一时钟。
- 已核对证据：CollisionDetector 使用 3D Collider 与 Physics.OverlapSphereNonAlloc，Runner 持续检测最小间隔 0.1 秒；TraitTiming 当前没有专用球球新接触、撞墙、战斗周期时机。旧预览使用 Animator/Transform，不能作为 ProjectBB 动态贴附与二维高速命中的通过证据。
- 已核对证据：旧 Common 下的 SkillLineSO 直接持有 GamePlay.SkillLineConfig，旧 ConfigManager 依赖 Singleton/ResourceManager；新工程不能照搬为 Common 对业务层的反向依赖。
- 建议方案与分批验收写入 30，手写配置工作流写入 40；除用户已确定要求外，具体结构、编辑时间基准和实现拆分仍为待确认提案。
- 验证范围：源码静态阅读与文档一致性/路径检查；未修改旧工程，未迁入代码或资产，未启动 Unity，未新增运行验收结论。

### 2026-10-13 60 FPS 与技能、词条配置化要求

- 用户确定整个游戏以 60 FPS 为目标；帧率设置、目标设备预算和真机性能验证待实施。已有默认 60Hz 战斗时钟不等于已经实现稳定 60 FPS。
- 用户明确技能与词条配置化；阶段 2 即建立所需最小配置闭环，四种能力通过可复用机制组合，阶段 3 完善制作工具。JSON 数值/规则与 SO 时间线边界保持不变。
- 源码核对：SkillManager 当前仅有基础碰撞与圆形弹道命中转发；四种能力、Trait、Buff、完整 Runner 与正式 JSON 表尚未实现。Bootstrap 使用 Inspector 原型参数，弹道测试按钮和请求使用代码参数/默认值。
- 旧工程运行时代码、配置样例与编辑器需用户指定可读取目录后审查依赖；本次未读取或迁入旧源码。当前仅更新需求与计划文档，没有实施能力或修改运行代码。

### 2026-10-10 首版架构基线

历史说明：本条第 2 项的自管运动方向于 2026-10-12 被 DOC-004 的实体 Dynamic 方案替代；固定时钟、分层及互伤批次规则继续保留。

依据：用户提供游戏简介与 ARPG 框架上下文，认可小球适配方案，并要求建立新项目上下文和根 AGENTS。

已确定：

1. 保留 LogicCore / GamePlay / View / Common / SceneInit 分层和 Combat / Skill / Buff / Trait 职责。
2. 新建轻量竞技场会话；首版矩形场地、两球、自管 XY 运动和固定逻辑步长。
3. 新接触基础双向伤害按同批次处理，延迟死亡提交，同时死亡平局。
4. 吸血鬼使用短暂贴附，吸血基于实际 HP 损失。
5. 技能物不自行结算伤害，技能命中走统一流水线。
6. JSON 数值配置 + SO 时间线，必要模块渐进适配二维编辑器。
7. 首版不迁入 ARPG 操作、寻路、AI、房间与完整账号体系。
8. 四种小球作为架构验证集，后续再扩展肉鸽构筑和局外系统。
9. 旧框架迁移记录不纳入本上下文，由本文件记录新研发和未来重构。

设计默认值与待调参数：逻辑频率初始建议 60Hz、移速换算、贴附时长、周期、蘑菇上限、斧头重复命中保护等。详细状态见 99。

### 2026-10-10 DOC-001 上下文初始化

- 范围：D:/ProjectBB/Client/AGENTS.md 与唯一 .ProjectBB_context。
- 新建项目概述、架构、运动碰撞、技能、配置、Buff、Trait、选卡预留、服务端预留、表现、规范、禁止项、研发记录、验收和当前状态。
- 更新原 Project Instructions.md 为兼容导航；上下文内 AGENTS 仅引导根规则。
- 旧 ARPG 文档与根 AGENTS 只作参考，未修改源文件、搬动资产或复制实现状态。
- 本批是文档工作；未创建 Assets/Packages/ProjectSettings，未导入任何源码或插件。
- 验证：19 份 Markdown（根 AGENTS 1 份、上下文内 18 份）通过严格 UTF-8 读取检查；38 个本地链接全部可解析，无失效目标。已检查旧项目名、旧宿主与迁移记录引用，没有带入旧工程执行指令。
- 一致性检查：碰撞接触、基础互伤批次、贴附建立、吸血与死亡提交的顺序在 10/30/35/50/60/70 间一致；所有代码与运行验收仍保持未实现/未通过状态。
- 本批未运行 Unity、未执行 Git、未修改源参考文件；纯文档验证范围已完成。

### 2026-10-10 DOC-002 动作与动画方案确认

- 历史状态：本条关于序列帧分工与骨骼非首版依赖的决定已由 DOC-003 替代；保留原始决策与当时验证记录。
- 依据与授权：用户认可动作方案，明确首版采用“分层 + 序列帧”，要求补充项目上下文。
- 设计已确定：分层图片承载基础动作反馈，序列帧补充局部复杂动作与特效，骨骼动画不作为首版基础依赖。
- 稳定设计写入 [25_Render_Foundation.md](25_Render_Foundation.md)，涵盖资源拆分、制作参考、表现与逻辑隔离、统一时间和池化清理；四种能力的具体画法与动作参数仍为制作参考。
- 同步 [98_Acceptance_Plan.md](98_Acceptance_Plan.md) 的表现验收项与 [99_Current_State.md](99_Current_State.md) 的已确定设计。
- 影响范围：仅更新上述四份上下文文档；未创建 Unity 工程、资源、播放器或插件依赖，战斗运动与伤害职责不变。
- 验证：四份修改文档通过严格 UTF-8 读取检查，6 个本地链接目标均存在；已核对方案、当前状态与未勾选验收项一致，均未将设计写成已实现。Unity 编译、Play Mode 与平台性能均未验证。

### 2026-10-10 DEV-001 工程创建与文件核对（进行中）

- 依据：用户要求指导新建工程与环境搭建，并反馈 Unity、C# 编辑器、WebGL/Android/iOS 模块及新工程均已准备好；目标平台尚未确定，本轮先跑通编辑器。
- 文件证据：ProjectRoot 为 D:/ProjectBB/Client/ProjectBB，存在 Assets、Packages、ProjectSettings；ProjectVersion.txt 为 2022.3.62f3c1（1623fc0bbb97）。
- 基础设置：默认 2D、Built-in、Linear；Force Text；Enter Play Mode Options 关闭。SampleScene 含正交相机，并已进入构建场景列表。
- 初次核对时：Assets 下未发现 C# 或 asmdef 文件。manifest 包含模板二维功能包、IDE 集成、Test Framework、TMP、UGUI、Timeline、Visual Scripting 等；初次仅同步上下文，未修改 Unity 设置或场景。
- 用户追加反馈与授权：原 SampleScene 按指导重复运行无报错，代码编辑器使用 Rider 2026.1，要求继续下一步。该反馈作为原空场景运行证据，不覆盖后续新增代码。
- 骨架实施：建立 LogicCore、Common、GamePlay、View、SceneInit 五个运行时程序集和独立 Editor 程序集，依赖方向见 10；LogicCore 禁止引擎与隐式插件引用，其余四个空业务层以程序集说明文件生成可引用程序集。
- 启动实现：新增 Bootstrap 场景与 ProjectBootstrap 组件，在 OnEnable/OnDisable 输出启用、停用日志；新增仅编辑模式可用的打开场景菜单，尊重未保存场景的保存/取消选择。入口不持有静态状态、不跨场景常驻、不创建会话或推进战斗。
- 资产影响：新资产配套 meta；保留 SampleScene 及原 GUID，构建列表仅启用 Bootstrap。未安装、移除或升级包，未导入旧工程代码或 Spine 运行时。
- 静态验证：outputs/bootstrap_setup/validate.ps1 检查通过，覆盖 6 个 asmdef 的预期依赖、LogicCore 引用限制、Editor 平台限制、运行时无 UnityEditor 引用、22 个唯一资产 GUID、Bootstrap 内部 fileID 与脚本 GUID、构建场景引用及文档 UTF-8/链接；结果保存于同目录 validation.json。
- 验证限制：本次尚无新增代码的 Unity 编译和 Bootstrap Play Mode 通过证据；Rider 打开脚本、补全、断点与模块安装状态未独立验证，平台构建和真机性能未验证。
- 下一步：回到 Unity 完成刷新/编译，通过菜单打开 Bootstrap，验证两轮启用/停用日志与 Rider 联动；平台选定后进行最小构建，本批保持进行中。

### 2026-10-10 DOC-003 角色动画调整为 Spine

- 依据与授权：用户提出减少序列帧图片量，认可“分层图片 + Spine 角色动画，少量序列帧辅助特效”的方案。
- 设计已确定：Spine 承担角色常规动作，兼容结构通过附件与 Skin 复用骨骼和动画；材质、粒子、简单图片动画及少量序列帧补充特效，替代 DOC-002 的原动画分工。
- 逻辑边界：移动、反弹、贴附、武器轨道与伤害继续由战斗逻辑决定；Spine 仅在 View 层消费事实，统一暂停、倍速、时间推进入口和池化清理。
- 文档范围：更新 25 的有效设计、30 的旧骨骼模块迁入边界、98 的验收项、99 的当前状态及本记录；保留已有工程创建与环境搭建记录。
- 实施状态：本批仅维护上下文，未安装 Spine 或 spine-unity、未制作资源、未修改 Unity 工程；版本选定、单球动作与换皮样例、平台性能均待实施或验证。
- 文档验证：五份修改文档通过严格 UTF-8 读取检查，8 个本地链接目标均存在；有效设计、技能边界、当前状态与未勾选验收项一致，旧决定仅保留为已标明替代关系的历史。未执行 Unity 编译、Play Mode 或平台验证。

### 2026-10-10 DEV-002 矩形场地与双球运动

- 授权范围：用户在工程骨架交付后要求“继续下一步”，本批实现矩形场地和双球运动；基础伤害、死亡、技能与 Spine 不在本批实现。
- 实施前证据：ProjectBB/Library/ScriptAssemblies 已有六个 ProjectBB.* DLL，时间为 2026-10-10 17:20:01–17:20:02，证明上一批程序集已由 Unity 生成；不据此推断场景运行或本批编译通过。
- 设计：从零实现双精度纯几何、统一连续碰撞裁决、接触轮次、固定会话时钟；同刻墙面优先于球球，并对同一球同一时刻的重复墙面约束合并计数。预算耗尽时丢弃未检测路程或积压时间并保留诊断。
- 文件：LogicCore/Geometry、GamePlay 的 Arena/Character/Battle、View/Battle/BattlePrototypeView、SceneInit/ProjectBootstrap、Bootstrap 场景与 Assets/Tests/EditMode；未读取或迁入 ARPG 源码，未增加第三方依赖。
- 运行范围：10×10 场地、红蓝双球、标准约 3.54 格/秒、朝内 ±45° 随机方向、1/60 秒时钟、暂停/后台暂停、1×/2×、同种子重开和视口适配。Inspector 原型参数经过 BattleSettings 校验，正式 JSON 配置待接入。
- 生命周期：会话拥有运动状态与随机源；View 只投影位置并持有运行时图形资源，SceneInit 统一创建、推进和释放。重开创建新会话身份并释放旧资源，不保留全局 Tick 或场景常驻对象。
- 验证：outputs/movement_validation/run_logic_checks.ps1 以 PowerShell 7.6.5 Add-Type 编译并执行同一套无 Unity 依赖用例，19 项全部通过。覆盖高速检测、正面与擦边、不同速度、零速、减速后反弹、角落、同刻墙球竞争、碰撞预算、初始重叠、持续接触和重新接触、固定种子、30/60/144fps、暂停/后台/释放、倍速/补步预算、非法参数。长运行包含 16 个种子、标准与十倍速度、合计 96,000 步。
- 静态验证：7 个 asmdef 的依赖与平台边界、42 个唯一资产 GUID、场景相机/脚本引用、构建入口、文档 UTF-8 与本地链接通过；脚本及结果位于 outputs/bootstrap_setup 和 outputs/movement_validation。
- 验证限制：纯 C# 测试不替代 Unity 编译；新增代码的 Unity 编译、EditMode Test Runner、Play Mode 视觉/输入/前后台实际回调/资源销毁、平台构建与真机性能均待验证。无 HP、伤害、胜负和自动结束；接触当前只有运动状态与计数，正式事件载荷及互伤流水线后续接入。
- 文档：同步 10、25、35、40、98、99，保留 DOC-003 的 Spine 决策与既有工程证据。

### 2026-10-12 DEV-003 基础互伤与胜负闭环

- 授权范围：用户要求继续“下一步”，本批接基础承伤、延迟死亡、胜负和反馈；用户明确超时按剩余生命比例判定。时限先采用可配置的 180 秒，比例相同平局，尚未进行时长和平衡调参。
- 设计与实施：LogicCore 增加纯 HP 损失公式；BallCharacter 组合 CombatManager 与基础 SkillManager，统一经 DamagePipeline 承伤。BattleSettlementSystem 在接触开始捕获双方请求，双方扣血结束才提交死亡，同死平局，死亡与结果各提交一次。没有同步表现回调插入批次。
- 会话影响：接触结束战斗时立即停在接触点，Step 返回实际消耗时长，末步可截短；截止时刻先完成碰撞，再按比例处理超时。暂停/后台不积累伤害。最多 64 个结果批次供表现读取，预算外仅丢旧反馈；释放关闭结算器并清空队列。
- 表现与配置：Bootstrap 增加双方 HP/伤害/免疫及时限，默认 100/10/不免疫/180 秒。View 增加血条、实际扣血白字、闪白、死亡变暗及结果；飘字上限 32、寿命 0.8 秒。结束后仅表现时间淡出，暂停与倍速仍由会话控制；正式 JSON、Spine、技能时间线、Buff、Trait、治疗和四种能力不在本批范围。
- 逻辑证据：outputs/combat_validation/run_logic_checks.ps1 通过 37 项纯 C# 用例（19 运动 + 18 战斗），包含 5/10 双死、异伤害交换注册顺序、免疫不取消反弹、过量伤害、重复/过期/旧会话、持续接触/重撞、墙面、暂停/后台、时限边界、比例相等与大整数、反馈预算。运动长运行使用零伤害并断言每种子真实运行 6,000 步，共 96,000 步。
- 资产静态检查：7 个 asmdef、55 个唯一资产 GUID、Bootstrap 脚本/相机及构建引用、相关文档 UTF-8 和本地链接通过。保留已有 GUID，未修改包、安装插件、读取旧源码或执行 Git。
- Unity 证据：已观察到 10:51:43 的 LogicCore、GamePlay、View、SceneInit、Tests 编译节点退出码均为 0；编译后的命名/注释收尾修改须以最终产物时间复核。最终快照写入 outputs/combat_validation/unity-compilation.json。独立逻辑测试不冒充 Unity EditMode 运行，Play Mode、实际前后台回调、视觉、资源销毁及平台构建/性能仍未验证。
- 同步文档：10、25、35、40、70、98、99 和本记录。下一步在 Unity 实测默认、双死、单方胜利、免疫和缩短时限，以及暂停/重开/退出。

### 2026-10-12 DOC-004 Dynamic 物理与弹道方案确认

- 依据与授权：用户明确后续技能需要改变物理运动，选择 Dynamic，并认可其余建议。本批将已确认设计写回上下文，不将方案认可扩展成一次性完成后续技能框架。
- 设计已确定：实体小球、分身和物理召唤物采用 Unity Physics2D Dynamic；普通大量弹道采用逻辑轨迹与主动扫掠，需真实物理交互的投射物按需 Dynamic。形状、运动、响应、命中与承伤职责分开，物理适配在 GamePlay，LogicCore 保持纯 C#。
- 时序变化：BattleSession 唯一驱动物理时钟，回调只复制接触值数据，子步边界归并并逐批结算。保留每次接触的双向请求与延迟死亡，不将整步伤害合并；不承诺动态实体在 Unity 内部精确致死时刻停步，不使用显示回拉冒充回滚。同边界来源排序、子步精度与超时对齐策略在实现时具体化并验证。
- 运动变化：原 BallMotor 积分/反弹职责待移交物理适配；巡航恢复与击退、冲刺、贴附等状态协调，不每步强制恒速。贴附在结算边界提交并在下一子步前建立约束，替代旧设计“在反弹前同步插入”的假设。
- 后续实施顺序：两个圆形 Dynamic 实体与矩形墙 → 圆形高速弹道命中闭环 → 一个具备质量的圆形分身/召唤物 → 四种能力、精细形状与性能容量。首次闭环必须覆盖移动目标交叉及墙边反弹，单次 Cast 不能冒充完整动态连续检测。
- 当前事实：DEV-002/003 源码仍为纯 C# 自管双球；37 项逻辑结果保留为旧实现证据。尚未迁移代码、修改物理设置、制作弹道或完成新方案 Unity/平台验证。现有双球寻址与生命周期需要在后续多实体接入时扩展。
- 文档范围：根 AGENTS、00、10、25、30、35、50、70、80、98、99 与本记录；修正自管运动作为有效目标的旧措辞，补充 Dynamic 的独立验收，不机械勾选历史测试。
- 验证：12 份修改文档通过严格 UTF-8 读取检查，33 个本地链接目标全部存在；检索核对旧自管/反弹前结算措辞仅作为已标注的历史或替代说明保留，Dynamic 验收项均未勾选。本批未执行 Unity、Git、插件安装或源码迁移。

### 2026-10-13 DEV-004 Unity 基线与双球 Dynamic 迁移

- 授权：用户明确先补齐现有 Unity 基线，再完成两个圆球与矩形墙的 Dynamic 迁移。未扩展到弹道、分身/召唤、完整技能框架或平台构建。
- 迁移前：保存旧运行源码、meta、旧测试和关键源码哈希至 outputs/dynamic_validation/baseline。Unity 2022.3.62f3c1 实际 Test Runner 37 项旧用例 + 1 项进入真实 Play Mode 的 Bootstrap 集成全部通过；覆盖基础互伤、双方 5/10 同死、暂停和重开。最终产物新于源码。原始 XML 和编译快照同目录。
- 设计与实现：BallCollisionSystem 原地替换为会话本地 PhysicsScene2D 适配，移除旧积分/反弹运行路径；增加 BallContactRelay 仅收集回调，双球统一归并后调用既有结算器。BattleSession 每逻辑步最多两个子步，默认 1/120 秒；子步后同步快照及结算，死亡后停止下一子步，超时末步截短。全局物理设置保持原值。
- 运动与表现：BallMotor 的位置/速度成为只读模拟快照；巡航有限加速度恢复，冲量默认保护 0.15 战斗秒，显式停止优先。View 保留唯一插值，不写刚体；旧碰撞预算提示改为实际补步预算。物理参数、容差与当前 API 见 35。
- 迁移后测试：Unity 编译通过，Test Runner 5 节点全部通过（2 几何、含 8 规则的结算回归、Bootstrap 集成、含 14 场景的物理集成）。专项覆盖击退保护、持续接触、真实重撞、同死、停止恢复、免疫、初始穿透、墙角、超时、暂停/倍速、世界隔离、帧率及释放。8 种子标准/十倍速度各 1,800 逻辑步，共 28,800 物理子步通过；容差为墙边 0.03 格、球距至少 0.95 格。
- 测试修复：初次专项协程在 EnterPlayMode 域重载时丢失闭包；改为重载完成后创建嵌套验证协程，最终通过。该问题属于测试组织，不作为物理失败或玩法变更。
- 实际 UI：16:9 Game 可见双球、矩形场地及血条；接触 1 时双方显示 90/100。点击暂停后时间保持 11.7 秒、位置保持；重开回到 100/100、接触 0 并替换物理场景身份；退出 Play Mode 后只剩 Bootstrap。未主动保存场景或修改编辑态参数。
- 资源与边界：保留既有 GUID，没有修改场景、包、ProjectSettings、旧工程或使用 Git。释放先禁用旧物理节点、关闭结算并清反馈，再卸载场景；测试验证场景/刚体数恢复。测试程序集补充 SceneInit 引用以覆盖真实装配。
- 证据：outputs/dynamic_validation/dynamic/unity-results.xml、physics-scenarios.txt、unity-compilation.json、static-validation.json。旧纯逻辑脚本改为读取归档源码，不再将其冒充 Dynamic 测试。
- 未关闭：弹道、多实体、组合形状、池化、墙面能力载荷、多来源排序未实现；多视口/完整视觉、实际系统前后台、Rider、平台和真机性能仍待专项。现有独立世界隔离及脚本模拟依据 Unity 2022.3 PhysicsScene2D.Simulate 文档，非 Play Mode 不派发碰撞回调，所以物理专项实际进入 Play Mode。
### 2026-10-13 DEV-005 圆形高速弹道闭环

- 授权：接入一种圆形高速弹道，跑通轨迹、扫掠、过滤、命中、伤害和回收；重点验证双方高速交叉、目标墙边反弹、重开清理。
- 实现：新增纯几何 ProjectileGeometry、CircleProjectileRequest/快照及会话持有的 CircleProjectileSystem。弹道直线、不穿透、不反弹，首个合格敌方命中消耗（免疫也消耗），碰墙/寿命/结束回收。128 值槽位复用，满时拒绝并诊断；按会话 + AttackId 区分生命期。当前只查询双球，不提前扩展多实体或完整 Runner。
- 扫掠：有在途弹道时，在原物理子步内按当前最大刚体速度继续细分实际 Simulate，预计路程阈值 0.02 格，每子步预算 256 段。每段读取真实前后快照作圆形相对扫掠；墙边反弹不再用整子步直线。预算耗尽显式取消尚在飞行的弹道，保留此前已捕获命中，剩余物理照常推进。阈值不是任意动态轨迹的严格误差保证，详见 35。
- 结算：同名义子步先球球双向互伤，再按弹道身份递增独立处理，最后超时；SkillManager → DamagePipeline → CombatManager 复用既有链路，AttackId 与 ContactRound 分离。死亡后清所有在途弹道并停止后续子步，单向命中不会变为整步同时互杀。
- 表现：复用圆形 Sprite 节点，独立有界承伤快照继续驱动白字/闪白；增加红/蓝方发射按钮（速度 80、半径 0.15、寿命 2 秒、伤害 10），及活跃/命中/容量拒绝/扫掠预算诊断。没有新增自动施法或序列化配置字段。
- 首轮发现与修正：480 格/秒交叉虽已命中，但仍继续无用细分并触发原 128 段预算；增加全部候选终止后的细分早停，并将预算设为 256。Bootstrap 测试同帧 Find 取到等待 Destroy 的旧根，改为选取启用根，帧末再验证旧根销毁。首轮 6 通过/2 失败保存在 outputs/projectile_validation/first_run。
- 最终 Unity 验证：2022.3.62f3c1，Test Runner 8 节点全部通过：3 个几何、结算回归（8 条规则）、Dynamic 集成（14 场景）、弹道集成（15 场景）、原 Bootstrap 集成及三轮弹道表现池重开集成。静止目标以 1000 格/秒弹道测试，交叉双方均 480 格/秒，墙边目标 240 格/秒；返回路径/安全近失和 20 倍细逻辑步参考一致。128 发实际伤害全部完成，第 129 发拒绝。
- 实际 UI 验证：Bootstrap 16:9 Game 的两方发射按钮可用；分别点击后命中计数由 0→1→2，对方各减少 10 HP，并看到白色 -10；重开恢复双方 100 HP、接触/命中/活跃弹道归零并替换物理会话。此 UI 操作不等同完整平台视觉验收。
- 资源/边界：新增四个源码资产及配套 meta，既有 GUID 保留；未改场景、包、ProjectSettings、旧工程或使用 Git。三轮重开各清八个在途弹道及旧 View 根；独立会话释放后场景和刚体数恢复。
- 证据：outputs/projectile_validation/unity-results.xml、projectile-scenarios.txt、physics-scenarios.txt、unity-compilation.json；静态记录同目录。最终 DLL 均新于相关源码；最近增量 Bee 日志含 GamePlay/View/SceneInit/Tests 成功节点，LogicCore 在上一轮生成并被最终测试实际加载。
- 静态验证：7 个 asmdef 依赖与平台边界、63 个唯一资产 GUID、Bootstrap 场景和构建引用、33 个文档本地链接及 UTF-8 检查通过。退出 Play Mode 后 Hierarchy 仅剩 Bootstrap，未保存或改写编辑态场景。
- 未关闭：复杂曲线/旋转/组合形状、多敌方目标、分身召唤与实体池化、正式配置/自动技能、平台构建和真机性能。128 容量和 256 段预算是原型保护，不是设备性能承诺。
- 同步文档：00、10、30、35、70、98、99 和本记录，稳定规则与实际验证分别记录。

### 2026-10-13 DEV-006 配置驱动自动弹道最小闭环

- 授权：先核验插件导入编译，再完成手写 JSON 加载/引用校验、周期 Trait、配置圆形技能及可运行配置。复用现有时钟、扫掠与承伤，验证暂停/倍速/死亡/结束/重开，回归双球及弹道。未扩展多实体、完整 SkillEditor、Spine 或 Excel，未执行 Git 或修改旧工程。
- 插件预检：Unity 2022.3.62f3c1 当前 Bootstrap 编辑态 Console 0 错误/0 警告，最近 Bee 插件编译节点 exitcode=0；自有 DLL 时间和哈希保存在 plugin-preflight.json。Newtonsoft 3.2.2 使用官方包 DLL；Common 改为 overrideReferences=true，仅显式预编译引用 Newtonsoft.Json.dll。LogicCore 保持无引擎/插件引用。Odin/NodeCanvas 未接入本批业务，Spine 仍缺 Unity 集成。
- 配置：Common JsonConfigReader 严格检查字段/类型/重复键及文档；GamePlay BattleContent.Load 定义版本 1 battle/skills/traits/balls 契约，校验范围、重复 ID、缺失引用及上限后发布只读快照。DTO 仅加载期使用，link.xml 保留反射记录。时间为毫秒，空间为格/格每秒，具体字段见 40。
- 运行：BallCharacter 绑定 TraitManager，每角色独立绝对周期序号、移除标记及触发诊断；最小周期 10ms、每球最多 16 个 Trait。Trait 只请求 SkillManager；后者校验来源/会话、索敌并将出生中心按弹道半径内缩到场内，再提交原 CircleProjectileRequest。伤害与死亡流水线未改。
- 边界：fireOnStart=false 等首个周期；true 在首个实际推进步发射。普通周期在完成承伤后的子步边界触发，新增弹道下子步才飞行；死亡/超时不新增到期触发。周期按序号计算不累积量化漂移；满池拒绝消耗本轮并诊断，不突发补发。暂停/后台不推进，倍速复用固定步；移除停未来触发，结束/释放清在途弹道及状态。
- 装配：Bootstrap 默认从 Resources/Config/AutoProjectileBattle.json 每次重开重新解析，错误阻止启动且不静默回退；原 Inspector 保留显式 `_usePrototypeSettings` 回归模式。默认双方每 1000ms 发射，速度 12、半径 0.15、寿命 2000ms、伤害 10；手动按钮在配置模式也消费对应技能。入口设置 60 FPS 目标及 vSyncCount=0。
- 设计取舍：本批技能是立即生成单颗弹道，没有时间线片段，因此不新建无消费意义的 SO、空 SkillHit 表或全局 ConfigManager。后续完整时间线仍遵守 JSON 数值/规则与 SO 时间线唯一来源。现有 128 槽与双球目标查询保持原验证边界，不宣称达到 300+20。
- Unity 编译：实际 Common/GamePlay/View/SceneInit/Tests Csc 节点全部 exitcode=0、无编译输出，DLL 时间晚于相关源码；未改 LogicCore 使用已有成功产物。没有运行 dotnet build 或独立 C# 编译器替代 Unity。
- Test Runner：首轮 37 个节点全部通过；收尾补齐极小正速度必须符合弹道零速保护的校验与第 26 组非法字段测试，最终 38 节点全部通过，失败/跳过均 0。新增 30 节点包含只读配置、26 组非法字段、重复技能/Trait ID/非法文档、9 项 Play Mode 聚合专项及真实 Bootstrap 装配集成。旧 8 节点全部通过，含 14 物理、15 弹道、8 结算规则和 Bootstrap 重开/同死回归。原 DEV-004/005 结果保留，新报告路由到本批目录。
- 回归等待修正：补极小速度测试后的中间轮曾为 37 通过/1 失败，唯一失败是旧 Bootstrap 表现池重开测试的“等待两次协程轮询后检查 Destroy”。60 FPS 限帧后 EditMode Test Runner 两次轮询不保证经过渲染帧末；测试改为以 Time.frameCount 等待两个真实渲染帧，最多 5 秒并断言超时，运行时 Dispose/Destroy 逻辑未变。随后全套 38 通过。首轮成功快照在 initial_pass，中间失败原始 XML/编译快照在 restart_wait_failure，未掩盖失败记录。
- 实际 UI：默认配置自主发射并造成伤害，一轮在 16.8 秒结束（蓝方 30 HP，红方死亡），双方累计自动发射各 16 次，结束清在途弹道。实际点击重开恢复双方 100 HP 和计数；另一轮在 7.5 秒点击暂停后，1 个在途弹道、5 次命中、双方各 7 次自动发射以及位置/HP 在后续观察中保持不变。退出 Play Mode 后 Hierarchy 仅 Bootstrap，Console 0 错误/0 警告，未保存或修改编辑态场景。
- 静态检查：7 个自有 asmdef 依赖/平台边界，Common 显式插件引用，运行时无 UnityEditor，项目自有资产 meta/GUID、Bootstrap/构建引用、UTF-8 和文档链接通过。第三方资产不按自有命名规则扫描；详细计数在 static-validation.json。
- 证据：WorkspaceRoot/outputs/config_skill_validation 下 plugin-preflight.json、unity-compilation.json、unity-results.xml、configured-scenarios.txt、physics-scenarios.txt、projectile-scenarios.txt、static-validation.json、source-hashes.json。
- 未关闭：平台构建/AOT 与真实前后台回调、目标设备 60 FPS、300 弹道 + 20 实体联合性能、多实体注册、非周期 Trait/升级/修改器、完整时间线/编辑器、四种能力和 Spine Unity 集成。仅保留反射类型不等于 AOT/平台验证通过。

### 2026-10-13 DEV-007 弹道出生位置、方向与随机开关

- 授权：用户询问当前 JSON 属于弹道还是技能配置，并要求配置控制子弹出生位置、运动方向及是否随机。现有文件是 battle/balls/traits/skills 的战斗配置集合；本次将生成参数作为 circleProjectile 技能记录的兼容扩展，不新建独立弹道表或完整生成器编辑器。
- 设计：出生点由自身/敌方/场地中心参考点加世界 XY 偏移后按半径 Clamp 到场内；方向可朝敌方、沿来源当前速度或固定世界角度。randomDirection=true 覆盖方向模式，每次发射均匀抽取 [0°,360°)，生成后保持直线。零目标距离/零运动速度用来源保留方向兜底。数值边界与示例见 40。
- 实现：GamePlay 新增只读 ProjectileLaunchDefinition 与枚举；BattleContent 将字符串模式在加载期校验并转换，不在 Tick 解析字符串。SkillManager 生成请求时解析参考点/方向，继续复用原 CircleProjectileSystem 与承伤链。BattleSession 持有独立技能 System.Random，使用 battle.seed 派生，不消耗小球初始运动随机序列；重开重置，拒绝的前置校验不抽样，满池拒绝已消费本次抽样。
- 兼容：schemaVersion 保持 1；新增六字段用 System.ComponentModel.DefaultValue 声明缺省值。Common 仅对明确声明默认值的字段允许缺失并 Populate；旧字段仍必填，显式 null/错误类型/非法值仍拒绝。不引入 JSON 对 GamePlay 之外的反向依赖，也不修改共享定义。
- 默认配置显式写出六项：caster、偏移 (0,0)、towardTarget、固定角度 0、随机 false；保持已有自动对战。用户可以只改这些字段并导入/重开，不需要改 C#。源码和 Unity 生成 meta 保留既有资产引用；未修改旧工程、未执行 Git。
- 验证：实际 Unity 2022.3.62f3c1 编译通过，首轮 Test Runner 56 节点全部通过（原 38 + 新 18），0 失败/跳过。新增含旧配置兼容、14 组非法输入、3 个真实 Play Mode 集成，覆盖三种参考点及偏移、边界 Clamp、红蓝固定角度、从出生点索敌、零速兜底、非追踪、随机速度/方向/序列/四象限及旧会话/暂停拒绝。旧双球/高速弹道/配置周期/生命周期回归全部保留。
- 证据：outputs/projectile_launch_validation/unity-results.xml、unity-compilation.json、static-validation.json、source-hashes.json，以及本轮三份旧专项场景报告。调整测试报告输出路径，不覆盖 DEV-006 或更早原始证据。生成路径复用现有值请求和池，未引入逐发临时集合或托管对象创建；未做设备 GC/性能测量，不把代码检查当作真机结论。
- 同步 10/30/40/96/98/99。后续局部朝向偏移、随机扇区、多弹排列/波次、追踪/曲线、召唤实体和生成器编辑器仍按需求分批实现，本批没有扩展这些机制。

### 2026-10-13 DEV-008 弹道墙面反弹

- 授权：用户要求配置是否碰墙反弹并实现，且本轮不控制 Unity，改由用户操作。仅编辑项目源码、测试、样例和必要上下文；未调用 Unity UI、命令行 Unity 或请求文件触发测试，未执行 Git 或 dotnet build。
- 设计与兼容：skills[].bounceOnWall 为可选 bool，false/省略保持碰墙回收，true 按矩形墙反射速度；严格拒绝 null/类型错误。保持 schemaVersion=1 和既有 Common 默认值解析。保留用户已改为 true 的 randomDirection，当前样例开启 bounceOnWall=true，其他数值不变。
- 实现影响：BattleContentRows/只读 CircleSkillDefinition/SkillManager/CircleProjectileRequest 传递开关；CircleProjectileSystem 在实例内保存当前速度，复用 LogicCore 的 SweepWalls/ReflectWalls，逐段推进撞墙后的剩余时间并同步目标时间偏移。角落翻转两个分量但计一次，速度大小/寿命/身份不重置。Trait、BattleSession 时钟、Dynamic 实体、View 和承伤流水线保持原有职责。
- 预算：每颗弹道每查询细段最多 64 次反弹，下一次墙碰撞超预算时回收该弹道并累计诊断；不丢弃其他弹道或穿越未查询路径。不将预算、代码检查或池容量当作真机性能证据。
- 测试准备：新增 ProjectileBounceTests，9 个测试节点（兼容 1、非法值 4、Unity 集成 4），覆盖四墙/角落/贴墙向内外/高速连续多弹/反弹后一次承伤及免疫/墙前目标/同刻墙优先/寿命/池复用/目标片段时间偏移/配置消费/暂停后台倍速/帧率/死亡超时/旧会话/极窄场地预算。目标时间偏移和同刻边界采用明确构造的查询片段，其他运行场景经 BattleSession 实际物理步。原 56 节点保留，预期总数 65，以用户 Unity 实际结果为准。
- 测试输入修正：既有配置集成与方向测试显式关闭随机/反弹，避免当前样例调参改变这些专项的前置条件。新增验证菜单仅响应用户点击，结果写入 outputs/projectile_bounce_validation，旧专项报告同步路由至新目录，不覆盖历史证据。
- 当前验证：完成文件静态检查；Unity 编译、Test Runner 和画面观察均待用户操作，不复用 DEV-007 的成功结果为本批背书。用户操作入口、步骤和未勾选验收见 98；静态报告在 outputs/projectile_bounce_validation/static-validation.json。

## 下一批建议（DEV-008 后）

DEV-001：本批已补齐 Bootstrap Unity 编译与 Play Mode 集成，继续补齐 Rider 联动及平台验证；平台确定后完成目标平台初步构建。

DEV-004/005/006：旧 Unity 基线、双球 Dynamic、圆形高速弹道与配置自动技能闭环已完成，后续按授权扩展具备质量的分身/召唤物，再制作能力/生成组编辑器及联合容量验证。新物理/弹道/配置集成与旧纯逻辑证据分别记录。若需要复用旧模块，先按用户明确范围审查源码依赖。

不在没有依赖审查时直接复制整个 ARPG 业务目录。工程宿主未确定前，不默认引入 FFEngine、YooAsset、HybridCLR 或 Luban。

## 后续记录模板

每批记录：日期与编号、任务授权范围、设计变化、涉及系统/文件、数据与资源影响、实际验证、未关闭事项、下一步。发生重构时增加原因、替代边界与旧实现处理方式。

同一批追加验证结果，不为每次对话创建零散总结。已失效决定保留为历史并标明替代记录，当前主题文档只表达有效规则。

## 2026-10-14 DEV-009 多实体、能力与生成组编辑器（已实现待用户 Unity 验证）

- 授权：用户要求继续前述 2～5 点，包含实体注册、召唤闭环、生命周期、联合容量评估以及推进编辑器/四种能力；明确主球决定胜负，主人死亡清理召唤物。
- 验证事实：用户反馈已执行 DEV-008 回归；本地保存报告实际为 61 通过/4 失败，四项是 ConfiguredSkillTests 写死 2000ms 与用户样例 60000ms 不一致。本批隔离契约测试固定 JSON，不回退用户调参；尚未重跑，不写成全部通过。
- 实施：BattleSession 有界实体注册与序列，BallCollisionSystem 多实体物理池/接触归并，弹道相对运动查询＋保守候选裁剪，召唤归属/寿命/质量/死亡回收；新增 AbilitySystem、临时减速、存活治疗、事件 Trait、六种技能定义和三个样例。
- 编辑器：GenerationSequenceAsset 和 GenerationEditorWindow 最小生成组制作/预览，JSON 数值只读，SO 时间和引用，Play Mode 预览复用独立会话。完整拖拽多轨道、子技能树、Spine、Excel 不在本批已完成范围。
- 验证准备：新增 SummonAbilityTests（8 个测试节点），包括配置、召唤/质量/命中/生命期/主人死亡/时间线/四种能力/墙角/免疫/击退/300+20 工作负载。原 65 节点保留，预计共 73，以 Unity 实际结果为准；旧专项报告同步路由至本批目录，避免覆盖旧证据。
- 独立静态验证：使用安装的 Unity Roslyn 与已有引用清单，隔离输出编译 LogicCore/Common/GamePlay/View/SceneInit/Editor/Tests 七程序集通过；未执行 dotnet build，未写 Library/ScriptAssemblies，不代替 Editor 编译、测试或运行。证据 outputs/summon_ability_validation/static_compile/results.json；四份 JSON 通过实际 BattleContent.Load 的独立配置加载检查，见 config-load.json。
- Unity 控制工具授权等待超时，没有执行 UI 输入/测试。随后用户明确今后全部 Unity 操作由本人手动执行、助手指导；已写根 AGENTS。没有创建 Unity 请求文件或运行 Unity 命令行。
- 仍待：用户完成本批 Unity 编译/回归/样例观察及编辑器保存撤销/关闭清理；联合性能报告须运行后才能评估，目标平台真机 60 FPS 尚无证据。源码与静态证据不作为完成运行验收。
