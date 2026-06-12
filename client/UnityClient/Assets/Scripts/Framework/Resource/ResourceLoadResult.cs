using UnityEngine;
using Object = UnityEngine.Object;

namespace GameClient.Framework.Resource
{
    public readonly struct ResourceLoadResult<T> where T : Object
    {
        public ResourceLoadResult(T asset, object? releaseToken = null)
        {
            Asset = asset;
            ReleaseToken = releaseToken;
        }

        public T Asset { get; }

        public object? ReleaseToken { get; }
    }
}
