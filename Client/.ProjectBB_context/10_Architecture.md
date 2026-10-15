# ProjectBB 架构

## 设计状态

本文定义目标职责与依赖方向。当前已有程序集骨架、启动入口、双球运动与基础互伤/胜负闭环，旧基线与双球 Dynamic Unity 集成验证通过；完整技能、Buff、Trait 等仍待实现。下文运行层表描述目标职责，已落地范围见文末，不将计划职责视为已有 API。

2026-10-12 用户确定实体运动采用 Unity Physics2D Dynamic，以支持改变物理运动的技能及实体分身、召唤物。物理适配归 GamePlay，SceneInit 装配，由 BattleSession 单一入口驱动；LogicCore 继续保持纯 C#。DEV-004 已将双球与矩形墙迁移至会话独占的 PhysicsScene2D；DEV-005 已接入圆形直线查询弹道，分身和召唤物待实施。

## 分层

| 层 | 职责 | 边界 |
| --- | --- | --- |
| LogicCore | 属性、修改器、伤害公式、二维几何、纯输入与结果数据 | 纯 C#，不引用 Unity、GamePlay 或表现对象 |
| GamePlay | 战斗会话、运动规则、接触关系、技能、Buff、Trait、结算 | 不实现 UI、相机、特效播放；完整伤害编排归此层 |
| View | 小球外观、特效、飘字、血条、相机、UI | 消费事实，不裁决命中、伤害和胜负 |
| Common | 配置加载、资源、对象池、音频等基础设施 | 不反向依赖具体战斗业务 |
| SceneInit | 启动、场景装配、逻辑与表现绑定、退出清理 | 负责组装，不承载伤害公式和技能规则 |

GamePlay 可依赖 LogicCore 与 Common；View 可读取 GamePlay 提供的只读状态并调用明确的业务入口。SceneInit 负责连接各层。Common 中配置基础设施不引用具体 GamePlay Row，业务配置由装配层注册。

当前不引入完整 ECS，不要求全部既有 Manager 一次性纯 C# 化。新增几何和状态数据优先保持独立可测试。

## 战斗运行层

| 计划类型 | 所有权与职责 |
| --- | --- |
| BattleSession | 一场对战的时钟、物理步调度、随机源、实体注册、命中队列、运行状态与释放顺序 |
| ArenaManager | 当前场地的边界、出生位置及场地配置查询 |
| BallCharacter | 一个小球实体，组合战斗、技能、Trait 与运动能力 |
| BallMotor | 目标巡航速度、运动控制状态与物理指令；读取模拟位置，不再自行积分或重复反弹 |
| BallCollisionSystem / BallContactRelay | 管理 Dynamic/Collider、同步模拟快照、归并实体接触轮次；已替代旧双球自管运动求解 |
| CircleProjectileSystem | 已落地圆形直线弹道池、相对扫掠、实体过滤与命中候选；不自行扣血或隐式推挤，其他技能形状待实施 |
| BattleSettlementSystem | 批次顺序、延迟死亡提交、胜负确认；不复制伤害公式或直接改 HP |

这些对象按会话持有和释放，不能为了方便全部做成跨场景单例。几何纯函数可以进入 LogicCore，贴附、反弹策略、实体查找和接触生命周期仍属于 GamePlay。

## 角色组件边界

- CombatManager：属性、Buff、战斗控制状态、承伤、治疗、待死亡状态与死亡提交。
- SkillManager：技能注册、释放请求、冷却、Runner、命中转发与技能生命周期。
- SkillRunner：执行一次技能的时间线模块，不决定能力为什么触发。
- TraitManager：长期能力的获得/移除、条件判断、触发请求、参数修改。
- BallMotor：将已裁决的运动状态交给物理适配，不负责伤害公式；技能不直接争写 Transform 或 Rigidbody2D。

SkillManager 可以读取 CombatManager。CombatManager 不持有或调用 SkillManager；死亡后的取消与清理由会话协调或已定义事件接入。

BallCharacter 是统一实体类型，阵营与归属使用数据表达。继承旧 BaseCharacter 前必须检查其 3D 移动、动画、输入和单例依赖；不能仅靠改类名完成迁入。

## 唯一事实源

- Dynamic 实体的模拟位置与速度由 Unity 物理求解，GamePlay 物理适配提供只读快照和归并接触；原 BallMotor 不再作为第二套位置求解器。View 的 Transform 只作表现投影，物理节点与外观形变节点分离。
- 普通查询弹道的轨迹由技能物逻辑持有，命中经统一入口处理；需要物理交互的投射物采用 Dynamic。每个对象只有一个运动权威，不能对同一实体叠加两套反弹/积分。
- HP 与护盾通过统一资源/承伤入口变化，UI 不反向写入。
- 每个会话分配稳定实体 ID，Unity InstanceID 不作为长期战斗身份。
- 攻击实例、技能物、接触轮次各自拥有明确身份；对象池复用不复用上次生命期的命中记录。
- 场地尺寸来自竞技场配置，不从相机视野或屏幕分辨率反推。
- 同一状态不得同时由独立 Update 与 BattleSession 驱动。

## 生命周期

创建会话 → 加载并校验配置 → 创建场地和双方实体 → 绑定表现 → 开始战斗 → 结算 → 释放。

暂停、恢复、倍速和前后台恢复统一作用于战斗时钟。退出时停止新请求，撤销贴附关系与来源修改器，取消技能和计时任务，回收技能物、解除事件订阅，最后释放实体及表现。

重开创建新会话身份；旧异步加载结果、回调和对象池归还不得污染新会话。

## 肉鸽与服务端预留

后续整次肉鸽尝试跨多场 BattleSession；构筑属于整次尝试，接触和临时技能物属于单场。血量、Buff、冷却是否跨场保留是玩法配置决策，尚未确定。

Protocol / Server 按 [15_GameServer_System.md](15_GameServer_System.md) 在需要时接入，不作为首场战斗的启动前置条件。

## 代码落点与程序集基线

本节记录当前实际源码；双球 Dynamic 和一种圆形查询弹道已实现，现有双球持有结构和 API 不能直接视为支持多实体注册。

已在 ProjectRoot/Assets/Scripts 下建立 LogicCore、GamePlay、View、Common、SceneInit。后续 GamePlay 按 Battle、Arena、Character、Skill、Buff、Trait、Card 等业务分组；角色使用 Character，不建立 Unit 与 Character 两套目录。

Unity 工程位于 D:/ProjectBB/Client/ProjectBB。各层通过同名 ProjectBB.* asmdef 显式引用，关闭 Auto Referenced；依赖表如下：

| 程序集 | 自有程序集引用 | 约束 |
| --- | --- | --- |
| ProjectBB.LogicCore | 无 | noEngineReferences=true，overrideReferences=true，预编译插件引用为空 |
| ProjectBB.Common | 无 | 不引用业务层 |
| ProjectBB.GamePlay | LogicCore、Common | 不引用 View、SceneInit |
| ProjectBB.View | LogicCore、Common、GamePlay | 不引用 SceneInit |
| ProjectBB.SceneInit | LogicCore、Common、GamePlay、View | 负责装配 |
| ProjectBB.Editor | 当前无 | 位于 Assets/Editor，仅 Editor 平台；按工具实际需求增加运行时程序集引用 |
| ProjectBB.Tests | LogicCore、GamePlay、SceneInit | 位于 Assets/Tests/EditMode，仅 Editor 测试程序集 |

Common 已有 JsonConfigReader，仅显式引用 Newtonsoft.Json.dll；LogicCore 已有二维几何、ProjectileGeometry 及 DamageFormula。GamePlay 已有会话、运动、CombatDefinition、CombatManager、CircleProjectileSystem、SkillManager、DamagePipeline、请求/结果快照及 BattleSettlementSystem；DEV-006 增加 BattleContent 只读配置/引用校验与角色 TraitManager。View 已有 BattlePrototypeView。编辑器工具不放入运行时 asmdef 管辖范围。GamePlay 仅向 ProjectBB.Tests 开放 internal 测试访问。

启动场景为 Assets/Scenes/Bootstrap.unity，构建列表仅启用该场景。ProjectBootstrap 显式绑定场景相机，OnEnable 创建战斗会话及表现，Update 将未缩放帧时长送入 BattleSession，OnGUI 转发表现控件，OnDisable 释放会话和运行时资源。原 SampleScene 与其 GUID 保留。

ProjectBB/打开启动场景菜单仅在编辑模式可用，切换前调用 Unity 的场景保存提示，用户取消时不切换。运行时重开释放旧会话并创建新身份，同一配置种子可复现初始方向。

BattleSession 持有运动与战斗时钟、随机源、暂停、倍速、后台暂停、结算器及最多 64 个批次的表现反馈队列。BallCharacter 组合 BallMotor、CombatManager 与基础 SkillManager。新球球接触在物理子步结束归并后，由会话同步交给结算器，先捕获双方请求，再完成承伤及死亡提交；战斗结束停止运动、命中与战斗时间。表现只能消费已完成批次的值快照，不通过同步事件回调插入批次。重开/释放关闭旧结算器并清空反馈。

DEV-006 基线当时没有完整技能时间线、Buff、贴附、治疗或通用派生攻击队列，Trait 仅周期触发、墙撞击仅计数；DEV-009 的扩展见文末。结束后的 PresentationSeconds 仅用于反馈淡出，不能驱动游戏规则。验证与限制见 35、70、96、98。

DEV-006：Bootstrap 默认每次重开加载 Resources JSON，经 Common 严格解析与 GamePlay 业务引用校验后创建 BattleContent；记录转为只读定义，TraitManager 只持有自己的周期状态。SkillManager 解析已校验的技能、索敌并提交既有弹道请求。原 Inspector 装配保留为显式原型回归模式，不作配置失败回退。数值与周期协议见 40/60。

DEV-007：圆形技能额外持有 ProjectileLaunchDefinition，出生参考点/XY 偏移/固定或动态来源方向/随机开关在 SkillManager 释放时解析。BattleSession 持有与初始运动独立的技能随机序列，重开重置；Trait、View 与承伤层不参与随机方向或生成位置裁决。

DEV-005：BattleSession 额外持有 CircleProjectileSystem（128 值槽位）与最多 64 项弹道承伤反馈。有在途弹道时由同一物理适配进一步细分 Simulate，按目标运动片段查询；结算边界仍为原子步。先球球互伤，再按弹道 AttackId 独立结算，最后超时。View 复用圆形表现节点，读取弹道值快照；战斗结束/释放清在途实例，重开销毁旧 View 池。具体近似与预算见 35，命中/消耗规则见 30，结算见 70。

## DEV-009 多实体与能力增量（2026-10-14，已实现待 Unity 验证）

本节替代前文“仅双球寻址、无贴附/治疗/非周期 Trait”的当前实现描述；前文 DEV-004～008 保留历史基线含义。

BattleSession 维护至多 64 个实体（含两个主球）、稳定递增身份、召唤生命周期和生成时间线快照。FirstBall/SecondBall 保留为主球及旧测试入口，寻址与索敌改为实体集合。BallCollisionSystem 池化物理槽位，登记/移除同步清接触状态；逻辑角色实例每次重新创建。主球决定胜负，召唤物死亡不结束战斗，主人死亡和战斗结束清理召唤物，此规则由用户确认。

SkillDefinition 支持 circleProjectile/summon/area/trap/orbit/drain 六种有限模块。AbilitySystem 持有查询技能物和有上限贴附关系；实体回调依旧只收集事实，所有命中复用 SkillManager/DamagePipeline/CombatManager。CombatManager 增加存活者治疗和带来源的临时减速，仍不反向依赖 SkillManager。

GenerationSequenceAsset 仅保存生成时间/主球/技能引用；BattleSession 消费复制后的不可变 GenerationCue。Editor 的生成组窗口引用 GamePlay/LogicCore，JSON 数值只读；Play Mode 战斗预览创建并释放独立正式会话，不写预览版伤害逻辑。
