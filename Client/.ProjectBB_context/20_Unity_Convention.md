# ProjectBB Unity 与 C# 规范

## 技术基线

Unity 2022.3.62f3c1、C#、横屏 2D、小游戏目标平台。2026-10-10 已核对 ProjectRoot/ProjectSettings/ProjectVersion.txt，版本符合要求。包版本与平台 SDK 以实际工程为准；当前先验证编辑器运行，小游戏平台未选定。

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
