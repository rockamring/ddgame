using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameClient.Framework.Editor
{
    public static class GameFrameworkDllSync
    {
        private const string MenuRoot = "Tools/GameFramework/";

        [MenuItem(MenuRoot + "Sync Dependency DLLs")]
        public static void Sync()
        {
            var pluginDir = Path.Combine(Application.dataPath, "Scripts", "Framework", "Plugins");
            Directory.CreateDirectory(pluginDir);

            CopyRequired(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages", "google.protobuf", "3.28.0", "lib", "netstandard2.0", "Google.Protobuf.dll"),
                Path.Combine(pluginDir, "Google.Protobuf.dll"));

            AssetDatabase.Refresh();
            Debug.Log("[GameFramework] dependency DLLs synced to Unity Plugins.");
        }

        private static void CopyRequired(string source, string destination)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException($"Required DLL was not found: {source}", source);

            File.Copy(source, destination, overwrite: true);
        }
    }
}
