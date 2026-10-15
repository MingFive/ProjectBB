using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace ProjectBB.Tests
{
    /// <summary>将同一组纯逻辑验证用例接入 Unity EditMode Test Runner。</summary>
    public sealed class MovementTests
    {
        public static IEnumerable<string> Cases()
        {
            foreach (MethodInfo method in typeof(MovementTestCases).GetMethods(BindingFlags.Public | BindingFlags.Static))
                yield return method.Name;
        }

        [TestCaseSource(nameof(Cases))]
        public void VerifyMotionRule(string caseName)
        {
            try { typeof(MovementTestCases).GetMethod(caseName).Invoke(null, null); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
    }
}
