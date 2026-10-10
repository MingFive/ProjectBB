# ProjectBB 当前状态

更新时间：2026-10-10。

## 当前阶段

阶段 0：用户已创建 Unity 空工程，工程文件与基础设置已核对；空场景运行、C# 编辑器联动和业务代码仍待验证或实施。用户当前要求指导环境搭建，尚未授权开展完整战斗实现。

## 已核对的目录事实

- 项目工作根：D:/ProjectBB/Client。
- 唯一上下文：D:/ProjectBB/Client/.ProjectBB_context。
- 实际 ProjectRoot：D:/ProjectBB/Client/ProjectBB，存在 Assets、Packages、ProjectSettings。
- ProjectVersion.txt：2022.3.62f3c1，revision 为 1623fc0bbb97，符合项目要求。
- 默认行为为 2D；Graphics 和各质量档均未分配 SRP 资产，当前为 Built-in，色彩空间为 Linear。
- EditorSettings 已配置 Force Text，Enter Play Mode Options 未启用，保留完整重载。
- 当前场景为 Assets/Scenes/SampleScene.unity，含正交 Main Camera，已列入构建场景。尚未创建 Bootstrap 场景，Assets 下未发现 C# 或 asmdef 文件。
- 用户反馈 Unity、C# 编辑器、WebGL/Android/iOS 构建模块均已安装；模块安装状态及 IDE 联动未独立验证。
- 上下文初始化验证记录见 96 的 DOC-001；本次工程文件核对见 DEV-001。
- 尚未核验 Git 仓库归属，不根据目录推断仓库根。

## 已确定设计

五层分工、统一双球实体、固定逻辑时钟、自管二维运动、矩形首版、接触去重、双向碰撞批次、延迟死亡、同时死亡平局、吸血短暂贴附、统一命中承伤、JSON + SO、横屏正交相机、四种球能力验证集。

详细规则以对应主题为准，此处不重复维护方法、字段或事件定义。

动作与动画已确定采用分层图片 + 序列帧：基础反馈使用分层图片的缩放、旋转、局部偏移和颜色变化，局部复杂动作与特效使用序列帧；骨骼动画不作为首版基础依赖。方案见 [25_Render_Foundation.md](25_Render_Foundation.md)，资源制作、播放器及运行验证均待实施，具体表现参数待调试。

## 尚未实现

所有战斗、运动、技能、配置、表现、编辑器、协议与服务端均未实现。没有已通过的 Unity 编译、Play Mode、小游戏构建或真机性能结果。

旧 ARPG 的模块为候选参考来源，尚未完成源码依赖审查，不能声称可以直接复制运行。

## 待确定或待调参

| 项目 | 当前处理方式 |
| --- | --- |
| 小游戏平台与 SDK | 用户明确尚未确定，当前先跑通编辑器，后续再验证发布链路 |
| 渲染管线与色彩空间 | 当前 Built-in + Linear，正式质量与平台兼容性待验证 |
| 逻辑步长 | 初始建议 1/60 秒，待真机确认 |
| 数值 | HP 100、碰撞伤害 10、10×10 场地等为初始参考，尚未平衡验证 |
| 吸血、蘑菇、斧头 | 时长、间隔、数量、免疫时消耗等在能力制作时明确 |
| 战斗超时与卡住处理 | 需要显式规则，不能默认无限运行 |
| 持续伤害 | 不照搬旧伤害池语义，制作毒蘑菇时确定 |
| 肉鸽选卡与跨场状态 | 第四阶段细化，未默认三选一或全量续血 |
| 联网与账号 | 首版非前置，未来另定权威、同步和存档 |

## 下一步

继续 DEV-001：核对外部 C# 编辑器联动，保存启动场景，验证重复 Play/Stop 与 Console。平台确定后尽早完成最小构建。随后按 DEV-002 审查最小可复用代码并实现基础对战，不直接开始旧框架宿主迁移或批量导入无关资源。

验收清单见 [98_Acceptance_Plan.md](98_Acceptance_Plan.md)，设计与执行历史见 [96_Development_Record.md](96_Development_Record.md)。
