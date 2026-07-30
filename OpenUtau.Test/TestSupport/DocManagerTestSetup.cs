using System.Reflection;
using System.Threading;
using OpenUtau.Core;

namespace OpenUtau.Test.TestSupport {
    /// <summary>
    /// Minimal DocManager setup for tests that exercise code paths calling
    /// DocManager.Inst.ExecuteCmd. Avoids the heavy Initialize() (plugin search,
    /// PhonemizerRunner) by wiring only what ExecuteCmd needs: mainThread set to
    /// the test thread so commands run inline instead of being posted.
    /// </summary>
    internal static class DocManagerTestSetup {
        public static void RunOnCurrentThread() {
            var inst = DocManager.Inst;
            typeof(DocManager)
                .GetField("mainThread", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(inst, Thread.CurrentThread);
            // ExecuteCmd falls back to PostOnUIThread only when off-main-thread;
            // with mainThread == current thread it is never hit, so leave it null.
        }
    }
}
