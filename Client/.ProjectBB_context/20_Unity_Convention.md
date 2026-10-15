# ProjectBB Unity 与 C# 规范

## 技术基线

Unity 2022.3.62f3c1、C#、横屏 2D、小游戏目标平台。2026-10-10 已核对 ProjectRoot/ProjectSettings/ProjectVersion.txt，版本符合要求。包版本与平台 SDK 以实际工程为准；当前先验证编辑器运行，小游戏平台未选定。

用户反馈代码编辑器使用 Rider 2026.1；manifest 中 com.unity.ide.rider 为 3.0.36。编辑器产品版本与 Unity 集成包版本分别记录，首个自有脚本的打开、补全和断点联动待验证。

## 插件准备清单（2026-10-13）

用户要求安装清单后已手动导入插件。2026-10-13 最新文件核对：Newtonsoft JSON 3.2.2 已在 manifest/lock；Assets 有 Odin Inspector、Odin Validator 和 NodeCanvas，NodeCanvas 框架常量为 3.41。Odin DLL 文件版本不能作为产品版本，具体产品版本待核对。Spine 当前仅有 spine-csharp 4.3.41（Git 4.3 分支），尚未发现 spine-unity Unity 集成包或 SkeletonAnimation/SkeletonGraphic 源码；AI Navigation 独立包未见安装。插件导入后的 Unity 编译已在 DEV-006 补验，见下段。

DEV-006 补验：插件导入后 Unity Console 为 0 错误/0 警告；实际 Unity 编译与全套 38 个 Test Runner 节点通过。Common 使用 overrideReferences=true，仅显式预编译引用 Newtonsoft.Json.dll；LogicCore 继续无引擎/插件引用。Odin/NodeCanvas 没有接入本批业务，Spine Unity 层仍缺。证据在 outputs/config_skill_validation/plugin-preflight.json、unity-compilation.json、unity-results.xml。这只验证编辑器编译和运行，不代表插件平台构建全部通过。

2D 功能集、Physics2D、Timeline 1.7.7、Test Framework 1.1.33 已有，Burst 1.8.21 / Collections 1.2.4 为锁文件中的现有依赖，不代表业务已经使用。下面保留安装用途与顺序，实际已导入项无需重复安装。

旧 Editor/SkillEditor 与 GamePlay/Skill 中未发现 Sirenix、NodeCanvas/ParadoxNotion、DOTween 直接引用；JSON 相关源码使用 Newtonsoft。旧工程其他编辑器/AI/角色代码有不同依赖，不能把旧工程整体插件列表当作新技能编辑器安装前置。

| 顺序 | 插件/包 | 当前建议与安装入口 |
| --- | --- | --- |
| 1 | Unity Newtonsoft Json | 已安装官方 com.unity.nuget.newtonsoft-json 3.2.2，保留实际版本；后续配置加载接入时验证程序集引用与编译，不复制旧 DLL |
| 2（可选） | Odin Inspector | 已有 Inspector/Validator 文件；可辅助复杂属性面板，旧 SkillEditor 核心不要求它。不因此引入 Odin 序列化格式 |
| 3（动画接入时） | spine-unity | 当前仅安装 spine-csharp 核心，Unity 集成仍待补齐；需与制作/导出版本匹配。当前 Built-in，不额外要求 URP 或 Spine Timeline 扩展 |
| 暂不装 | AI Navigation / NavMeshComponents | 当前 Dynamic 小球、轨迹弹道与简单召唤行为没有 NavMesh 需求；com.unity.modules.ai 的存在不等于已安装 com.unity.ai.navigation |
| 已导入，暂不接入业务 | NodeCanvas | 用户已导入，保留；后续明确需要行为树/FSM 制作复杂召唤物 AI 时再接入，弹道生成、轨迹和技能调用链不要求它 |
| 暂不装 | DOTween、寻路/弹幕插件、独立池框架、ECS 套件 | 目前没有本批必要依赖；对象池、候选查询与批量更新先在现有架构内实现，再根据测量决定是否需要外部工具 |

安装逐项进行，每项后确认 Unity Console 编译结果并记录版本。插件导入后，自有 asmdef 仍需在实际使用层添加明确引用；LogicCore 保持纯 C#，不通过勾选全部程序集打破依赖边界。

官方参考（本日核对）：[按名称安装 UPM 包](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-ui-quick.html)、[Newtonsoft 变更记录](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/changelog/CHANGELOG.html)、[Odin 安装](https://odininspector.com/tutorials/getting-started/installing-odin-inspector)、[Spine 安装](https://us.esotericsoftware.com/spine-unity-installation)、[Spine 版本管理](https://uk.esotericsoftware.com/spine-versioning)、[AI Navigation](https://docs.unity.cn/cn/2022.3/Manual/com.unity.ai.navigation.html)、[NodeCanvas](https://nodecanvas.paradoxnotion.com/)。

## 命名与目录

- 类、方法、公开属性使用 PascalCase；参数和局部变量使用 camelCase；私有字段使用 _camelCase。
- 不新增 ResourceMgr 一类缩写；Manager 使用完整后缀。沿用明确的框架术语和已有公共接口时不为命名做无关重构。
- 自有命名空间最多两层，例如 ProjectBB.GamePlay、ProjectBB.View、ProjectBB.LogicCore。
- 目录可按业务继续分组，不因目录深度增加命名空间层级。
- UI 按业务模块组织；Ctrl、View、ViewData、Widget 等后缀表达角色，不建立全项目 Controller/Model/Widget 横向目录。
- Runtime 与 Editor 隔离；运行时程序集不得引用 UnityEditor。

## 注释与文件

- 文档和新源码使用 UTF-8。PowerShell 读取中文文档显式指定 -Encoding UTF8。
- 自有代码注释使用中文，专业名词与代码标识符可保留英文。
- 新增自有类用简短中文说明职责和生命周期，可用 // ============================================================ 分隔；复杂公开 API 和集成点补充 XML summary。
- 不为局部修改重写整份历史注释，不批量格式化既有文件或转换行尾。
- 实际迁入资产时保留 .meta 和 GUID，检查脚本、Prefab、SO、场景引用。

## Unity 对象与生命周期

- UnityEngine.Object 派生类型使用 == null / != null，禁止以 ReferenceEquals、?.、??、??= 绕过 Unity 的销毁对象判断。
- 普通 C# 对象不受上述特殊限制。
- OnDisable、OnDestroy、退出路径访问可能懒创建的单例前，使用实际存在的 HasInstance 或无副作用查询接口；不得编造 API。
- 生命周期绑定与解绑成对；池化对象每次启用重新建立本次身份，归还时清理计时器、命中集合、事件及来源引用。
- 异步加载带会话/生命周期校验；过期结果释放资源，不接入新战斗。

## 更新与集合

- 战斗逻辑通过会话统一推进，不同时使用 MonoBehaviour.Update 和会话 Tick 推进同一组件。
- 表现可在渲染帧更新，不能以动画或粒子播放进度裁决真实伤害。
- 逻辑侧不使用表现随机源，不依赖 Dictionary 枚举顺序决定结算先后。
- 不在枚举 Dictionary 时修改其内容；需修改时先构造稳定快照，高频路径复用缓存并考虑重入。
- 高频战斗循环避免 LINQ、每步字符串解析、临时集合及频繁创建销毁对象；优化需基于实际数据。

## 编辑器与验证

- 技能编辑器预览使用与运行时一致的二维坐标、锚点和朝向语义。
- SkillEditor 只编辑时间线内容，数值配置只读；编辑器预览不污染运行时配置缓存。
- 常规已授权修改直接执行。影响编辑流程、配置兼容或大范围接口的设计变更，先说明具体方案与影响；已确认方案不重复请求确认。
- 不以 dotnet build 或 .sln 构建替代 Unity 编译验收。静态检查、Unity 编译、Play Mode 和平台真机验证分别记录。
- 纯文档修改只检查路径、引用与规则一致性，不启动 Unity。
