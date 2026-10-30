using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace ChainboundRaid.Editor
{
    public static class PrototypeBuild
    {
        [MenuItem("Chainbound Raid/Build Client and Dedicated Server")]
        public static void BuildClientAndServer()
        {
            BuildWindows();
            BuildWindowsServer();
        }

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
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
            System.IO.File.WriteAllText("Builds/Windows/THIRD_PARTY_NOTICES.txt",
                "Chain 3D model by Kutejnikov (2020), modified for this game.\n" +
                "Source: https://opengameart.org/content/chain\n" +
                "License: Creative Commons Attribution 4.0 International (CC BY 4.0).\n" +
                "https://creativecommons.org/licenses/by/4.0/\n");
        }

        [MenuItem("Chainbound Raid/Build Windows Dedicated Server")]
        public static void BuildWindowsServer()
        {
            const string scene = "Assets/Scenes/RaidSessionScene.unity";
            if (!System.IO.File.Exists(scene))
                throw new InvalidOperationException("RaidSessionScene.unity is missing.");
            System.IO.Directory.CreateDirectory("Builds/Server");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = "Builds/Server/ChainboundRaidServer.exe",
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows server build failed: {report.summary.result}");
        }
    }
}
