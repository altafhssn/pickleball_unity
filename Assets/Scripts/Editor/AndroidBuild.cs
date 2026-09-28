#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Pickleball.EditorTools
{
    /// <summary>
    /// Builds the Android APK from the scenes in Build Settings, from the menu or the command line
    /// (the editor must be closed for batch mode):
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -buildTarget Android
    ///     -executeMethod Pickleball.EditorTools.AndroidBuild.BuildApk -logFile output/build.log
    ///
    /// Then install over the existing app, keeping its data: adb install -r output/PickleSmash.apk
    /// </summary>
    public static class AndroidBuild
    {
        public const string ApkPath = "output/PickleSmash.apk";

        [MenuItem("Pickleball/Build Android APK", false, 20)]
        public static void BuildApk()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
            // An .aab can't be side-loaded with adb.
            EditorUserBuildSettings.buildAppBundle = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            BuildSummary summary = report.summary;
            Debug.Log("[AndroidBuild] " + summary.result + " -> " + ApkPath + " (" +
                (summary.totalSize / (1024 * 1024)) + " MB, " + summary.totalTime + ", " +
                summary.totalErrors + " errors)");
            if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
#endif
