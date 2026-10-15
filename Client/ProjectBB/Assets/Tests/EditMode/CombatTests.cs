using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectBB.Tests
{
    /// <summary>在 Unity 生命周期中执行与运动无关的既有结算回归。</summary>
    public sealed class CombatTests
    {
        [UnityTest]
        public IEnumerator SettlementRegressions()
        {
            yield return new EnterPlayMode();
            foreach (MethodInfo method in typeof(CombatTestCases).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                try { method.Invoke(null, null); }
                catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
            }
            yield return null;
            LogAssert.NoUnexpectedReceived();
            yield return new ExitPlayMode();
        }
    }
}
