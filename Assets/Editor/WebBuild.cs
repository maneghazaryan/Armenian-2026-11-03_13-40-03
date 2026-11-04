using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Aib.Editor
{
    /// <summary>
    /// Web build for static hosting (GitHub Pages).
    /// Menu: Ayb → Build Web, or from the command line:
    ///   Unity -batchmode -quit -projectPath . -executeMethod Aib.Editor.WebBuild.Build
    /// Output: Build/Web (index.html + Build/ + TemplateData/).
    /// </summary>
    public static class WebBuild
    {
        const string Output = "Build/Web";

        [MenuItem("Ayb/Build Web")]
        public static void Build()
        {
            // GitHub Pages cannot send Content-Encoding headers for pre-compressed files,
            // so the loader decompresses gzip itself.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            Debug.Log($"Web build: {summary.result}, {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors → {Output}");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
