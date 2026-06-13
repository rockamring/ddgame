using System.IO;
using UnityEditor;

namespace GameClient.Framework.Editor
{
    public sealed class GameFrameworkProjectReferencePostprocessor : AssetPostprocessor
    {
        private const string RuntimeProjectName = "GameFramework.Runtime.csproj";

        private static string OnGeneratedCSProject(string path, string content)
        {
            if (Path.GetFileName(path) != RuntimeProjectName)
                return content;

            content = EnsureReference(
                content,
                "Unity.Addressables",
                @"Library\ScriptAssemblies\Unity.Addressables.dll");

            return EnsureReference(
                content,
                "Unity.ResourceManager",
                @"Library\ScriptAssemblies\Unity.ResourceManager.dll");
        }

        private static string EnsureReference(string content, string assemblyName, string hintPath)
        {
            if (content.Contains($"<Reference Include=\"{assemblyName}\">"))
                return content;

            const string marker = "  </ItemGroup>\r\n  <ItemGroup>\r\n  </ItemGroup>";
            var reference =
                $"    <Reference Include=\"{assemblyName}\">\r\n" +
                $"      <HintPath>{hintPath}</HintPath>\r\n" +
                "      <Private>False</Private>\r\n" +
                "    </Reference>\r\n";

            var insertAt = content.LastIndexOf(marker, System.StringComparison.Ordinal);
            if (insertAt < 0)
                return content;

            return content.Insert(insertAt, reference);
        }
    }
}
