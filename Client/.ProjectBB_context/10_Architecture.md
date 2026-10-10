# ProjectBB 架构

## 设计状态

本文定义目标职责与依赖方向。当前尚无业务源码；类型名称为计划名称，方法签名、字段和事件定义须在实现时核对并维护。

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
| BattleSession | 一场对战的时钟、随机源、实体注册、命中队列、运行状态与释放顺序 |
| ArenaManager | 当前场地的边界、出生位置及场地配置查询 |
| BallCharacter | 一个小球实体，组合战斗、技能、Trait 与运动能力 |
| BallMotor | 位置、方向、有效移速、运动控制状态；不自行和另一球各算一次碰撞 |
| BallCollisionSystem | 统一计算球墙与球球碰撞，接触轮次、进入/持续/退出与反弹计划 |
| BattleSettlementSystem | 批次顺序、延迟死亡提交、胜负确认；不复制伤害公式或直接改 HP |

这些对象按会话持有和释放，不能为了方便全部做成跨场景单例。几何纯函数可以进入 LogicCore，贴附、反弹策略、实体查找和接触生命周期仍属于 GamePlay。

## 角色组件边界

- CombatManager：属性、Buff、战斗控制状态、承伤、治疗、待死亡状态与死亡提交。
- SkillManager：技能注册、释放请求、冷却、Runner、命中转发与技能生命周期。
- SkillRunner：执行一次技能的时间线模块，不决定能力为什么触发。
- TraitManager：长期能力的获得/移除、条件判断、触发请求、参数修改。
- BallMotor：执行已裁决的运动状态，不负责触发伤害公式。

SkillManager 可以读取 CombatManager。CombatManager 不持有或调用 SkillManager；死亡后的取消与清理由会话协调或已定义事件接入。

BallCharacter 是统一实体类型，阵营与归属使用数据表达。继承旧 BaseCharacter 前必须检查其 3D 移动、动画、输入和单例依赖；不能仅靠改类名完成迁入。

## 唯一事实源

- 位置、半径、速度、接触状态由战斗逻辑持有；Transform 只是表现投影。
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

## 代码落点建议

计划在 ProjectRoot/Assets/Scripts 下采用 LogicCore、GamePlay、View、Common、SceneInit。GamePlay 按 Battle、Arena、Character、Skill、Buff、Trait、Card 等业务分组；角色使用 Character，不建立 Unit 与 Character 两套目录。

Unity 空工程已建立于 D:/ProjectBB/Client/ProjectBB。以上脚本目录仍是计划落点，尚无自有业务源码或程序集划分。
