# ProjectBB 当前状态

更新时间：2026-10-14。

## 当前阶段

DEV-009 已实现多实体注册/物理槽池/目标查询、配置召唤、四种特色能力最小模块、生成组 SO 与编辑器、三个新样例及 300 弹道＋20 召唤物工作负载。七个自有程序集独立 Roslyn 静态编译通过；当前最终源码的 Unity 导入/编译、全部回归、画面/编辑器交互与容量报告待用户操作，不称为已验证。

用户确认主球决定胜负，主人死亡清理召唤物。用户同时明确以后 Unity 操作均由本人手动执行，助手只修改文件、静态检查并指导；见根 AGENTS。

DEV-008 用户反馈已执行回归；本地报告为 61 通过/4 失败，四项均源于旧配置测试写死 2000ms，而用户已将寿命改为 60000ms。本批保留用户样例，隔离固定测试配置，尚待重新运行。DEV-004～007 的历史通过不替代当前代码的验证。

原双球独立 PhysicsScene2D、固定战斗时钟、接触双向批次与弹道墙反弹继续保留。新代码按所有实体的运动片段做相对扫掠，容量默认 128，可配置至 4096；最多 64 个实体含主球。以上是代码预算，不是真机性能结论。

## 已核对的目录事实

- 项目工作根：D:/ProjectBB/Client。
- 唯一上下文：D:/ProjectBB/Client/.ProjectBB_context。
- 实际 ProjectRoot：D:/ProjectBB/Client/ProjectBB，存在 Assets、Packages、ProjectSettings。
- ProjectVersion.txt：2022.3.62f3c1，revision 为 1623fc0bbb97，符合项目要求。
- 默认行为为 2D；Graphics 和各质量档均未分配 SRP 资产，当前为 Built-in，色彩空间为 Linear。
- EditorSettings 已配置 Force Text，Enter Play Mode Options 未启用，保留完整重载。
- 新启动场景为 Assets/Scenes/Bootstrap.unity，含正交 Main Camera 与一个 ProjectBootstrap，构建列表仅启用该场景。原 SampleScene 及其 GUID 保留。
- Assets/Scripts 已建立五层 asmdef；LogicCore 禁止引擎和隐式插件引用。Assets/Editor 的独立 asmdef 仅面向 Editor，提供“ProjectBB/打开启动场景”菜单。
- ProjectBootstrap 已装配 BattleSession 与 BattlePrototypeView，统一推进固定步长战斗，失焦/后台暂停并在恢复时丢弃首帧时长；停用或重开关闭旧结算器、清空反馈并释放表现。
- LogicCore 已有二维几何、弹道相对扫掠及基础 HP 损失公式；GamePlay 已有 Dynamic 物理适配、接触归并、CircleProjectileSystem、SkillManager 释放/命中转发、DamagePipeline、CombatManager、BattleSettlementSystem，以及 BattleContent 业务配置校验和 TraitManager 周期触发。Common 新增通用 JsonConfigReader，显式引用 Newtonsoft.Json.dll。
- View 已有临时红蓝圆形、场地、相机适配与插值、血条、实际扣血飘字、受击闪白及胜负显示；提供暂停、1×/2×、同种子重开及红/蓝方弹道发射控件，复用圆形弹道表现池，尚未接入 Spine。
- Assets/Tests/EditMode 的 ProjectBB.Tests 已迁移为纯几何、结算回归和进入真实 Play Mode 的物理/Bootstrap 集成。旧 19 + 18 用例及源码保留在 outputs/dynamic_validation/baseline；38 项迁移前 Unity 结果单独保存。
- DEV-004 实际 Unity 编译节点与最终 DLL 时间/哈希见 outputs/dynamic_validation/dynamic/unity-compilation.json；原始测试 XML、14 场景报告与旧基线分开留存。
- 用户反馈 Unity、Rider 2026.1、WebGL/Android/iOS 构建模块均已安装；模块安装状态未独立验证。Rider 集成包为 manifest 中的 3.0.36，首个自有脚本的打开、补全与断点联动待验证。
- 上下文初始化验证记录见 96 的 DOC-001；工程骨架见 DEV-001，运动见 DEV-002，基础伤害与结算见 DEV-003。
- 尚未核验 Git 仓库归属，不根据目录推断仓库根。
- 2026-10-13 用户手动导入插件后：Newtonsoft JSON 3.2.2、Odin Inspector/Validator 文件、NodeCanvas（框架常量 3.41）已存在；Spine 仅有 spine-csharp 4.3.41，尚未发现 spine-unity Unity 集成。插件导入后 Console 0 错误/0 警告，DEV-006 实际 Unity 编译与测试通过。Odin 产品版本及平台构建未独立验证，详见 20。

## 已确定设计

2026-10-13 后续补充：大量弹道与实体召唤物是内容重点，首个联合规模起点为同时约 300 弹道 + 20 个有碰撞/血量的实体召唤物，另有基础双球，仍以 60 FPS 为目标。要求强化 SkillEditor 生成器、轨迹、事件、生命周期与诊断 UI；方案见 30，容量/验收见 35/98。DEV-009 已实现多实体与联合工作负载，运行结果及真机目标尚未验证。

2026-10-13 用户补充：整个游戏以 60 FPS 为目标；技能与词条配置化，阶段 2 四种能力同步建立最小配置闭环，阶段 3 完善导表与编辑器。DEV-006 已设置 targetFrameRate=60/vSyncCount=0，配置自动圆形技能与周期 Trait 已实现；DEV-009 四种能力最小模块已实现待验证，蘑菇中毒未接入。60Hz 逻辑步、编辑器统计和配置集成不代表真机稳定 60 FPS。详情见 25、40、96、98。

五层分工、统一角色实体、固定战斗时钟、实体 Dynamic 物理 + 普通弹道主动查询、矩形首版、接触归并去重、双向碰撞批次、延迟死亡、同时死亡平局、超时按剩余生命比例判胜负（相同比例平局）、吸血短暂贴附、统一命中承伤、JSON + SO、横屏正交相机、四种球能力验证集。

Dynamic 方案替代原自管运动目标：BattleSession 统一驱动物理步，回调只收集事实，子步边界逐批结算并停止后续推进；不承诺在 Unity 内部精确接触时刻中断，不将整步所有攻击视为同时伤害。主球巡航恢复与技能控制协调，不强制恒速抹去外力。第一批为两个圆形 Dynamic 实体 + 圆形高速弹道；DEV-009 已增加多实体召唤与容量测试代码，复杂形状待实施。细节见 35、70。

详细规则以对应主题为准，此处不重复维护方法、字段或事件定义。

动作与动画已确定采用分层图片 + Spine 角色动画，少量序列帧辅助特效，替代此前骨骼非首版依赖的约定。兼容结构的小球优先共用骨骼、动作并通过附件与 Skin 换皮；动画不驱动权威位移或命中，当前实体表现读取 GamePlay 物理快照，武器轨道由技能逻辑驱动。方案见 [25_Render_Foundation.md](25_Render_Foundation.md)，Spine 资源制作、运行时接入及运行验证均待实施，具体表现参数待调试。

## 当前增量与剩余范围

新增源码包括 SummonDefinition/Request、AbilitySystem、SpeedBuffCollection、GenerationSequenceAsset、GenerationEditorWindow；SkillDefinition 替代原圆形专用定义。BattleSession 提供统一 FindBall/GetEntity 与序列装配，FirstBall/SecondBall 保留主球语义。实体召唤物目前仅简单圆形巡航、没有自己的 Trait；不支持递归召唤。

尚未实现完整 SkillRunner/拖拽多模块时间线、生成事件/子技能树、追踪/穿透/复杂形状、配置化 DOT/完整 Buff/护盾、Trait 升级与长期属性修改、Spine Unity 集成、Excel 导表、肉鸽选卡/跨场状态、协议与服务端。已有范围/陷阱/环绕/贴附/治疗是最小模块，不意味着完整目标架构全部落地。

当前实体显示与血条、技能物池表现已扩展，完整动画、治疗反馈、多视口、实际前后台回调、平台/AOT 和真机性能继续待专项验证。旧工程没有迁入源码/资产，本批未执行 Git。

## 待确定或待调参

| 项目 | 当前处理方式 |
| --- | --- |
| 小游戏平台与 SDK | 用户明确尚未确定，当前先跑通编辑器，后续再验证发布链路 |
| 渲染管线与色彩空间 | 当前 Built-in + Linear，正式质量与平台兼容性待验证 |
| Spine 制作与接入 | 方案已确定；编辑器、导出数据与 spine-unity 版本待核对选定，先验证单球动作与换皮样例 |
| 逻辑步长 | 当前 1/60 秒逻辑步、每步 2 个物理子步、单帧最多 8 步；末步按时限截短，积压舍弃并诊断；子步精度与平台预算后续调参 |
| Dynamic 控制与结算 | 双球默认恢复加速度约 7.071 格/秒²、击退保护 0.15 秒、停止优先；质量 1、零摩擦/重力/阻尼、弹性 1；双球接触及超时末步已验证。扩展来源排序待对应系统实施，不承诺旧轨迹等价 |
| 弹道检测与容量 | 默认 128、可配置 1～4096 槽；多实体相对扫掠、保守包围盒候选裁剪已实现待验证，仍采用 0.02 格细分路程阈值。联合 300+20 测试待执行，复杂形状/真机容量待验证 |
| 数值 | 默认双方 HP 100、碰撞伤害 10、10×10 场地；JSON 分别定义 HP/碰撞伤害/免疫/初始 Trait，技能和周期可配置，尚未平衡验证；Inspector 仅显式原型回归模式使用 |
| 吸血、蘑菇、斧头 | 已配置最小样例；蘑菇接触免疫者也消耗，环绕可持续至移除，贴附有上限。倍率/间隔/时长仍待试玩；中毒模型未定 |
| 战斗超时与卡住处理 | 用户确定按剩余生命比例比较，相同平局；当前可调时限默认 180 秒，具体时长待调，碰撞预算保留诊断 |
| 持续伤害 | 不照搬旧伤害池语义，制作毒蘑菇时确定 |
| 肉鸽选卡与跨场状态 | 第四阶段细化，未默认三选一或全量续血 |
| 联网与账号 | 首版非前置，未来另定权威、同步和存档 |

## 下一步

由用户按 98 的 DEV-009 步骤完成 Unity 编译、菜单“召唤能力与全部回归”、三个新样例及生成组编辑器交互。读取 outputs/summon_ability_validation 的实际报告，修复失败后才评价联合容量；目前不应继续叠加新机制来替代验收。之后根据报告推进性能优化与编辑器制作体验，目标平台确定后验证最小构建和真机 60 FPS。Spine Unity 集成、Rider 联动与其他专项仍按既有计划。
