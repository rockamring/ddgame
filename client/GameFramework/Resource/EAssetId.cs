// 基础枚举定义，Phase 2 的 resource_codegen.py 会重写为完整成员

namespace GameFramework.Resource
{
    /// <summary>
    /// 统一资源 ID 枚举。
    /// 所有资源（文件资源 + 配置表定义的逻辑资源）共用同一套 ID。
    /// </summary>
    public enum EAssetId : uint
    {
        None = 0,
    }
}
