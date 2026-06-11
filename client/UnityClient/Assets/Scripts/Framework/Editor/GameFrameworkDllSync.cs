using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GameClient.Framework.Editor
{
    public static class GameFrameworkDllSync
    {
        private const string MenuRoot = "Tools/GameFramework/";

        [MenuItem(MenuRoot + "Build And Sync DLLs")]
        public static void BuildAndSync()
        {
            var repoRoot = GetRepoRoot();
            RunDotnetBuild(repoRoot);
            Sync();
        }

        [MenuItem(MenuRoot + "Sync DLLs")]
        public static void Sync()
        {
            var repoRoot = GetRepoRoot();
            var pluginDir = Path.Combine(Application.dataPath, "Scripts", "Framework", "Plugins");
            Directory.CreateDirectory(pluginDir);

            CopyRequired(
                Path.Combine(repoRoot, "client", "GameFramework", "bin", "Debug", "netstandard2.1", "GameFramework.dll"),
                Path.Combine(pluginDir, "GameFramework.dll"));

            CopyRequired(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget", "packages", "google.protobuf", "3.28.0", "lib", "netstandard2.0", "Google.Protobuf.dll"),
                Path.Combine(pluginDir, "Google.Protobuf.dll"));

            AssetDatabase.Refresh();
            Debug.Log("[GameFramework] DLLs synced to Unity Plugins.");
        }

        private static string GetRepoRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        private static void RunDotnetBuild(string repoRoot)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build .\\client\\GameFramework.sln",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start dotnet build.");

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"dotnet build failed.\n{output}\n{error}");
        }

        private static void CopyRequired(string source, string destination)
        {
            if (!File.Exists(source))
                throw new FileNotFoundException($"Required DLL was not found: {source}", source);

            File.Copy(source, destination, overwrite: true);
        }
    }
}
