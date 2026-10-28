using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ChainboundRaid.Editor
{
    public static class PrototypeBuild
    {
        [MenuItem("Chainbound Raid/Build Windows Prototype")]
        public static void BuildWindows()
        {
            const string scene = "Assets/Scenes/RaidSessionScene.unity";
            if (!System.IO.File.Exists(scene))
                throw new InvalidOperationException("RaidSessionScene.unity is missing. Create the 01-02 network scene first.");
            System.IO.Directory.CreateDirectory("Builds/Windows");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = "Builds/Windows/ChainboundRaid.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
        }
    }
}
