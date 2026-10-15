using System.Reflection;
using System.Runtime.CompilerServices;

// 战斗规则与会话生命周期，不承担表现。
// 测试程序集可验证批次内部顺序，业务程序集仍只使用公开入口。
[assembly: AssemblyDescription("战斗规则与会话生命周期，不承担表现。")]
[assembly: InternalsVisibleTo("ProjectBB.Tests")]
