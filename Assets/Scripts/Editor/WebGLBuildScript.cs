using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CoalPulverizerEditor
{
    // pulverizer-frontend에 임베드할 WebGL 빌드를 만든다.
    // 로컬 정적 서빙(Vite dev server)에서 바로 풀리도록 압축은 끈다 - 압축을 쓰려면
    // 서버가 Content-Encoding 헤더를 정확히 얹어줘야 하는데 지금 단계에서는 불필요한 복잡도.
    public static class WebGLBuildScript
    {
        private const string ScenePath = "Assets/CoalPulverizer_DigitalTwin_v1.unity";
        private const string OutputPath = "Builds/WebGL";

        [MenuItem("Tools/Pulverizer Twin/Build WebGL")]
        public static void Build()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.productName = "Coal Pulverizer Digital Twin";
            PlayerSettings.companyName = "NEXTRO";

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);

            Debug.Log(
                $"[WebGLBuildScript] result={report.summary.result} " +
                $"size={report.summary.totalSize} bytes " +
                $"errors={report.summary.totalErrors} warnings={report.summary.totalWarnings}"
            );

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception($"WebGL build failed: {report.summary.result}");
            }
        }
    }
}
