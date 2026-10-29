using System;
using Unravel.Core;

namespace Survivors
{
    /// <summary>Asset presence tests: GetAsset never returns null, a missing key comes back with an empty uid.</summary>
    public static class AssetChecks
    {
        public static bool IsLoaded<T>(T asset) where T : Asset<T>
        {
            return !ReferenceEquals(asset, null) && asset.uid != Guid.Empty;
        }
    }
}
