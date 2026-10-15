# ProjectBB 内容配置

## 配置边界

采用 JSON 数值与规则配置 + ScriptableObject 技能时间线。保持明确 Row 类型与查询语义，不建立万能 object[] 表。

2026-10-13 用户确定近期由助手按能力逐份编写配置，先用手写 JSON 实现功能，Excel 导表后补。DEV-006 已实现下方版本 1 契约、加载校验及自动弹道消费路径；目前仍无工作簿和导表器。

2026-10-13 用户明确技能与词条必须配置化。阶段 2 制作四种能力时同步建立所需最小配置加载、引用校验和 Trait/Skill/Buff 组合，不将四个角色的触发与数值固化为角色专用分支后再等待阶段 3 改造。阶段 3 完善导表、二维时间线编辑器和批量制作体验。当前自动弹道配置闭环已实现，四种能力及其扩展配置仍待实施。

配置负责已有机制的组合、触发条件、技能引用与参数；代码提供受限且可复用的触发、贴附、周期命中、治疗、范围攻击和技能物模块。新增同类内容优先改配置；全新机制仍需新增相应模块，不承诺任意玩法都能只填表实现。编辑器不写回数值表，运行实例不修改共享定义。

Bootstrap 默认由 Resources/Config/AutoProjectileBattle.json 装配只读 BattleContent，场地、运动、双方基础战斗参数和初始 Trait 均来自此快照。既有 Inspector 字段仅在显式开启 `_usePrototypeSettings` 时用于原型回归；配置缺失/无效不会回退到原型模式。默认每方 100 HP、碰撞伤害 10、无免疫，时限 180 秒；超时比较剩余生命比例，相同平局。

## 首版配置集合

下列为逻辑表名与内容范围，不是已存在的 C# 字段或文件：

| 配置 | 内容 |
| --- | --- |
| 系统配置 | 战斗步长、格子换算、标准速度、运行上限、战斗超时策略 |
| 竞技场配置 | 场地形状与尺寸、出生点、初始方向随机范围 |
| 小球配置 | 基础 HP、移速档位、半径、碰撞攻击、初始 Trait、表现资源标识 |
| SkillConfig | 技能释放条件、冷却、目标关系和技能结构引用 |
| SkillHitConfig | 伤害参数、命中附带效果、目标过滤及必要命中规则 |
| 技能物配置 | 蘑菇/斧头的运动、寿命、数量上限及调用技能引用 |
| TraitConfig | 长期能力的触发、条件、行为、参数修改 |
| BuffConfig | 临时状态、叠层、持续时间、属性与周期行为 |
| SkillLineConfig SO | 时间线模块、锚点、范围、技能物生成与表现通知 |

肉鸽阶段按实际需求增加 CardConfig 和候选规则。首版不复制 ARPG 全部卡池、元素、装备、天赋与成长表。

## 单位与数值

- 时间单位必须在表头和类型语义中明确；建议数值表使用毫秒，逻辑时钟内部统一转换。
- 空间使用格或世界单位的明确换算；不能直接沿用旧 ARPG 的厘米、XZ、绕 Y 轴旋转字段。
- 配置百分比建议延续万分比整数表达，在统一加载/数值入口转换，业务不得各自除以 10000。
- 移速档位 100 与实际格/秒有唯一换算来源。
- 逻辑半径独立配置，不能用 Sprite 尺寸或受击缩放推导。
- 若迁入旧公式的向上取整、百分比叠加等规则，必须检查源码后明确，不把历史文档当作已实现行为。

## 所有权

- ConfigManager 管注册、查询、缓存；具体业务表由业务模块定义和装配。
- 业务运行时不各自读取 JSON、Resources 路径或重复反序列化。
- 配置是只读定义，冷却、层数、剩余时长、命中集合属于实例。
- Trait 对技能参数的修改产生实例/请求侧结果，不改写共享配置或 SO。
- DSL 如有必要，在加载期解析为强类型结构；不在碰撞和 Tick 热路径解析字符串。
- 配置使用稳定资源标识，资源来源由 Common 适配；不预设已安装任何资源框架。

## 校验

加载/导出时检查重复 ID、缺失引用、非法单位、非正半径、场地容纳性、出生越界、无效周期、数量上限和派生循环风险。

必须区分“无限”“未配置”“零时长”等特殊值，不依靠任意负数兜底。配置缺失应给出表、行与引用信息，不能静默生成完整默认玩法。

## 技能编辑器

SkillEditor 写 SkillLineConfig，不写回 SkillConfig 或 SkillHitConfig JSON；可读数值快照用于预览。编辑器与运行时统一二维锚点语义，预览不修改运行时配置缓存。

引入外部 SO/Prefab 时检查 .meta、枚举序号和序列化字段。ProjectBB 尚无旧存档兼容负担，不因此自动承诺兼容所有 ARPG 内容。

## 手写配置工作流与结构建议（2026-10-13）

手写 JSON、后补 Excel 已确定；以下保留内容扩展建议，当前已落地字段与协议以下方 DEV-006 为准：

- 每次能力交付配套小球初始 Trait 引用、Trait 触发规则、Skill、命中、必要 Buff/技能物配置与时间线 SO；同时给出可调整参数、引用关系和验证结果。
- 先覆盖圆形直线弹道已有参数和周期释放，再以吸血鬼验证贴附、周期伤害、实际 HP 损失吸血及移除清理。具体实施顺序仍以用户下一批授权为准。
- 同一配置定义只有一个制作来源。JSON 管数值与规则，SO 管时间片段、锚点、几何内容与表现通知。伤害、冷却、寿命等不在两处重复维护；需要变体时引用不同定义，或通过明确的实例修改器处理。
- 建议 JSON 采用带版本信息的具名记录集合，加载时建立 ID/等级索引；Trait/Buff 条件与动作优先用有类型、具名参数的结构化条目。保留旧系统加载期解析的思想，不必为了手写配置先迁入完整花括号 DSL。最终结构确认后，Excel 只负责导出同一契约，运行时不识别来源。
- 初期不做运行中热更新；重载校验成功后用于新会话，已有会话持有原只读快照。引用/周期/单位错误需定位文件、记录和字段；配置无效时阻止该配置集启动，不静默替换玩法。
- 时间数值明确毫秒，百分比明确万分比，空间明确格/格每秒，角度为度；SO 的内容时间基准须单独记录，不与渲染帧率混用。
- SO 初期通过 Unity 内的最小创建入口或 Inspector 制作，完整技能编辑器不是第一份可运行配置的前置条件。

旧代码使用 Newtonsoft.Json；ProjectBB 使用官方 com.unity.nuget.newtonsoft-json 3.2.2。DEV-006 Common 明确引用 Newtonsoft.Json.dll，业务记录在 GamePlay，LogicCore 不引用 JSON 或插件。

## DEV-006 自动弹道配置契约（已实现）

文件：ProjectRoot/Assets/Resources/Config/AutoProjectileBattle.json。进入 Bootstrap Play Mode 默认自动生效；编辑并让 Unity 导入 JSON 后点击“重新开始”，新会话重新加载并校验。运行中的旧会话保持原快照，不做运行中热更新；Player 构建中的 Resources 为打包内容，不是外部热更文件。

| 记录/字段 | 含义与当前约束 |
| --- | --- |
| schemaVersion | 必须为整数 1 |
| battle | widthCells/heightCells、ballRadiusCells、standardSpeedCellsPerSecond、speedIndex、seed、maxBattleMs、firstBallId/secondBallId；宽高大于四倍球半径，出生仍使用 ArenaManager 的左右场内出生规则 |
| skills[].id/kind | 唯一 ID；当前仅 circleProjectile，瞬时生成一颗圆形直线弹道，无时间线 SO |
| skills[].speedCellsPerSecond | 有限数，严格大于 1e-12、最大 10000 格/秒，与现有弹道零速保护一致；默认 12 |
| skills[].radiusCells | 正数，严格小于场地较短边的一半；默认 0.15 格 |
| skills[].lifetimeMs | 整数 1～60000 毫秒；默认 2000 |
| skills[].damage | 非负 int，默认 10；通过既有 DamagePipeline/CombatManager 结算 |
| traits[].trigger/skillId | 当前只允许 periodic；引用已存在技能 |
| traits[].periodMs | 至少 10 的整数毫秒，默认 1000；大于当前 1/120 秒子步，避免单能力单边界无界补发 |
| traits[].fireOnStart | 必填布尔值；false 等待一个周期，true 在首个实际战斗步开始时触发 |
| balls[] | id、maxHp、collisionDamage、damageImmune、traitIds；每球最多 16 个不重复 Trait，空数组明确表示无初始能力 |

同表 ID 区分大小写，不允许空 ID/首尾空白；原有字段必填，DEV-007 的六个兼容扩展字段允许省略并采用明确默认值。拒绝未知字段、重复 JSON 键、重复记录 ID、缺失引用、错误 JSON 类型、非有限值和非法范围。错误包含源文件及字段路径，业务错误还包含记录 ID。`JsonConfigReader` 只解析通用契约，`BattleContent.Load` 一次完成业务校验并发布不可变定义和已解析引用。DTO 不进入 Tick，反射记录通过 link.xml 保留；平台构建/AOT 尚未验证。

当前不建空壳 SkillHit 表、SO 或通用 ConfigManager 单例：圆形技能参数和伤害只在该 skill 记录维护，没有重复制作来源。完整时间线落地时再增加 SO 引用，继续遵守 JSON 数值与 SO 时间线分离。

## DEV-007 弹道出生位置与方向

AutoProjectileBattle.json 是战斗配置集合，包含 battle、balls、traits、skills。当前 circleProjectile 是“释放时立即生成一颗圆形弹道”的技能，因此下列生成参数属于 skills 记录；尚未拆成独立弹道原型表。Trait 仍只负责触发周期。

| skills[] 字段 | 默认值 | 语义 |
| --- | --- | --- |
| spawnAnchor | caster | caster=自身中心，target=敌方中心，arenaCenter=场地中心；使用释放瞬间逻辑位置 |
| spawnOffsetXCells | 0 | 相对参考点的世界 X 偏移，正数向右，负数向左，范围为 ±场地宽度 |
| spawnOffsetYCells | 0 | 相对参考点的世界 Y 偏移，正数向上，负数向下，范围为 ±场地高度 |
| directionMode | towardTarget | towardTarget=从最终出生点朝向敌方；casterMovement=沿自身当前速度方向；fixedAngle=使用固定世界角度 |
| directionAngleDegrees | 0 | fixedAngle 模式使用；0°向右，90°向上，180°向左，-90°向下，允许 [-360,360]，不随阵营翻转 |
| randomDirection | false | true 覆盖方向模式，每次发射独立均匀抽取 [0°,360°)；只随机初始方向，不随机位置、不持续转向 |

先取参考点加 XY 偏移，再按弹道半径 Clamp 到场内；朝向敌方使用这个最终出生点。偏移是世界坐标，不随小球运动方向旋转。若需要场地固定坐标，选 arenaCenter 并填写 XY 偏移。target 参考点可能与敌方重叠，沿用初始重叠命中规则，不自动避开目标。

casterMovement 在零速度时使用小球保留的运动方向；towardTarget 在出生点与目标中心重合时同样兜底。方向在生成时确定，之后仍为直线弹道，不追踪目标。随机方向与小球初始方向使用独立 System.Random 序列，由 BattleSession 持有、以 battle.seed 初始化；相同种子和释放请求顺序在同一运行环境可复现，不承诺跨平台确定性。暂停/失效会话等前置校验失败不抽样；合法随机请求如果因满池被拒绝，本次抽样已消耗，随后按既有规则等待下个周期。

保持 schemaVersion=1 的向后兼容扩展：旧记录可以省略全部或部分新增字段；DefaultValue 属性声明缺省值，Common 解析层仅对明确声明默认值的字段允许缺失并填入该值。显式 null、错误类型、非法枚举名、非有限偏移/角度仍报错，原有必填校验不变。JSON 示例显式写出六项，方便调参。

例：从自身上方 0.6 格出生并向右飞行，设置 spawnAnchor=caster、spawnOffsetXCells=0、spawnOffsetYCells=0.6、directionMode=fixedAngle、directionAngleDegrees=0、randomDirection=false。只将 randomDirection 改成 true，即在同一出生位置随机向四周发射。

## DEV-008 弹道墙面反弹（已实现，Unity 待用户验证）

skills[].bounceOnWall 为可选布尔值，省略时为 false，schemaVersion 仍为 1。显式 null、字符串或数值不接受。true 表示弹道接触矩形场地墙面时镜面反弹并保持速度大小；false 保持原有碰墙回收行为，不表示穿墙。此字段与 randomDirection 独立，反弹时不重新抽取随机方向。

本次保留用户已设置的 randomDirection=true，并将当前 AutoProjectileBattle.json 的 bounceOnWall 显式设为 true，方便直接观察。没有调整周期、速度、半径、寿命或伤害。旧版本缺省值与当前样例设置应区分。

墙面按弹道半径内缩，角落同时反转 X/Y 分量；反弹不重置寿命、攻击身份或伤害，不提供额外穿透。命中首个合格敌方（含免疫）仍消耗，寿命到期/结束/释放仍清理。当前只支持矩形竞技场四面墙，不查询任意场景障碍物。

编辑配置后由用户让 Unity 完成导入，再重新开始战斗；运行中的旧快照不热更。手动验证步骤见 98 的 DEV-008。

## DEV-009 新配置契约（兼容 schemaVersion=1，待 Unity 验证）

原 AutoProjectileBattle.json 的用户调参保持：lifetimeMs=60000、randomDirection=true、bounceOnWall=true。历史 DEV-006 的 2000ms 是当时基线，不是当前样例值。契约测试改读 Assets/Tests/EditMode/Fixtures/AutoProjectileBaseline.json，避免合法调参破坏测试。

BattleContent 的技能只读类型由 CircleSkillDefinition 扩展为 SkillDefinition，六种 kind：circleProjectile、summon、area、trap、orbit、drain。原公共调用链保留，配置记录仍显式提供 speedCellsPerSecond/radiusCells/lifetimeMs/damage。弹道速度需正数，其他类型允许 0；area 的 lifetimeMs 仅作有限记录字段，不延迟瞬时执行。

| 新字段 | 默认/范围 | 含义 |
| --- | --- | --- |
| battle.projectileCapacity | 128；1～4096 | 会话弹道槽位数；常量 Capacity 保留为旧默认，实际容量看实例 Limit |
| skills[].count | 1；1～32 | 每次同时生成数量，仅弹道/召唤可大于 1 |
| skills[].spreadDegrees | 0；0～360 | 扇形展开角，360 表示闭合圆环且不重叠首尾 |
| skills[].summonBallId | 空 | summon 必须引用已有 balls 记录，首版该角色 traitIds 必须为空 |
| skills[].mass | 1；0.01～1000 | 召唤刚体质量 |
| skills[].maxPerOwner | 10；1～62 | 每来源同技能在场上限；orbit 需要至少 2 |
| skills[].orbitRadiusCells | 1；正数，不大于场地短边 | 环绕轨道半径 |
| skills[].angularSpeedDegrees | 180；-1440～1440 | 环绕角速度，度/秒 |
| skills[].hitIntervalMs | 250；至少 10 | 环绕重复命中间隔、吸血周期 |
| skills[].healRatioBps | 10000；0～10000 | 吸血按实际 HP 损失治疗的万分比 |
| skills[].speedMultiplierBps | 5000；0～10000 | 贴附期间巡航乘数，最强减速生效 |

上述扩展字段可省略但不接受显式 null/错误类型。orbit 的 lifetimeMs=0 明确表示直到移除/死亡/结束，其余类型 1～60000ms。非弹道不允许 bounceOnWall=true。Trait.trigger 增加 onStart/onWall/onContact；periodMs 对事件型为触发保护间隔，onStart 仅执行一次，periodic 保留原绝对周期语义。未实现的 trigger/kind 仍拒绝。

三个新资源路径：Config/SummonBattle（周期召唤＋弹道）、Config/AreaVsTrap（范围爆发对墙陷阱）、Config/DrainVsOrbit（贴附吸血对双环绕）。默认 Bootstrap 仍读取用户的 AutoProjectileBattle，不自动切场景或覆盖调参。召唤样例每方 500 HP、2 秒召唤一次、召唤体 30 HP/质量 0.5/半径 0.3/寿命 10 秒，数值未作平衡验收。

GenerationSequenceAsset 通过 ProjectBB/生成组时间线创建，SO 仅维护 timeSeconds/casterId/skillId。ProjectBootstrap 的 Generation Sequence 可选字段在会话建立后装配不可变快照。JSON 不被编辑器或运行实例写回。
