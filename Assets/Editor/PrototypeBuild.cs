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
            BuildClient("Builds/Windows", BuildOptions.Development);
        }

        [MenuItem("Chainbound Raid/Build Tester Client")]
        public static void BuildTesterClient()
        {
            BuildClient("Builds/Tester", BuildOptions.None);
        }

        static void BuildClient(string outputDirectory, BuildOptions buildOptions)
        {
            const string scene = "Assets/Scenes/RaidSessionScene.unity";
            if (!System.IO.File.Exists(scene))
                throw new InvalidOperationException("RaidSessionScene.unity is missing. Create the 01-02 network scene first.");
            System.IO.Directory.CreateDirectory(outputDirectory);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = outputDirectory + "/ChainboundRaid.exe",
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = buildOptions
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Windows build failed: {report.summary.result}");
            System.IO.File.WriteAllText(outputDirectory + "/THIRD_PARTY_NOTICES.txt",
                "Player models: Kenney Blocky Characters 2.0, CC0 1.0. https://kenney.nl/assets/blocky-characters\n" +
                "Elephant placeholder: syncopika, CC0 1.0. https://opengameart.org/content/low-poly-elephant-model\n" +
                "Chain 3D model by Kutejnikov (2020), modified for this game.\n" +
                "Source: https://opengameart.org/content/chain\n" +
                "License: Creative Commons Attribution 4.0 International (CC BY 4.0).\n" +
                "https://creativecommons.org/licenses/by/4.0/\n" +
                "Mammoth boss model: original work by the project owner, included with permission for tester distribution.\n" +
                "No separate permission to reuse this model is granted.\n");
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
