using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ProjectBB.Tests
{
    /// <summary>由显式请求文件或用户菜单启动工程测试，跨域重载保存 Unity 原始结果。</summary>
    [InitializeOnLoad]
    public sealed class ValidationRunner : ICallbacks
    {
        private static readonly string Output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/dynamic_validation"));
        private static TestRunnerApi _api;
        static ValidationRunner()
        {
            EditorApplication.delayCall += Register;
            EditorApplication.update += Poll;
        }
        private static void Register()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new ValidationRunner());
        }
        // 只由用户点击菜单启动；不创建请求文件，也不在脚本导入后自动运行。
        [MenuItem("ProjectBB/验证/弹道墙面反弹与全部回归")]
        private static void RunWallBounceValidation()
        {
            if (_api == null) Register();
            SessionState.SetString("ProjectBB.ValidationPhase", "projectile_bounce");
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "ProjectBB.Tests" } }));
        }
        [MenuItem("ProjectBB/验证/弹道墙面反弹与全部回归", true)]
        private static bool CanRunWallBounceValidation()
            => !EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem("ProjectBB/验证/召唤能力与全部回归")]
        private static void RunSummonValidation()
        {
            if (_api == null) Register();
            SessionState.SetString("ProjectBB.ValidationPhase", "summon_ability");
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "ProjectBB.Tests" } }));
        }
        [MenuItem("ProjectBB/验证/召唤能力与全部回归", true)]
        private static bool CanRunSummonValidation() => CanRunWallBounceValidation();
        private static void Poll()
        {
            if (_api == null || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Path.Combine(Output, "request.txt");
            if (!File.Exists(request)) return;
            string phase = File.ReadAllText(request).Trim();
            if (phase != "baseline" && phase != "dynamic" && phase != "projectile" && phase != "config_skill" && phase != "projectile_launch") return;
            File.Delete(request);
            SessionState.SetString("ProjectBB.ValidationPhase", phase);
            _api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "ProjectBB.Tests" } }));
        }
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string phase = SessionState.GetString("ProjectBB.ValidationPhase", "manual");
            string destination = phase == "projectile" || phase == "config_skill" || phase == "projectile_launch" || phase == "projectile_bounce" || phase == "summon_ability"
                ? Path.GetFullPath(Path.Combine(Output, "../" + phase + "_validation")) : Path.Combine(Output, phase);
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "unity-results.xml"), result.ToXml().OuterXml);
            File.WriteAllText(Path.Combine(destination, "summary.txt"), $"Unity {Application.unityVersion}\nPassed={result.PassCount}; Failed={result.FailCount}; Skipped={result.SkipCount}\n{result.Message}");
        }
    }
}
