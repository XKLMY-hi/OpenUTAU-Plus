using Xunit;

namespace OpenUtau.Core.Vst {
    /// <summary>
    /// 共享 VstPluginManager/VstPluginRegistry/RenderGate 单例的测试集合。
    /// DisableParallelization：类内测试方法也串行——共享静态状态（单例注册表、
    /// 在飞计数、fire-and-forget 异步 Load 任务残留）无法并行安全断言。
    /// </summary>
    [CollectionDefinition("VstShared", DisableParallelization = true)]
    public class VstSharedCollection { }
}
