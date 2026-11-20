using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ReinoRunas.EditorTools
{
    /// <summary>
    /// Menú "Reino de las Runas/Compilar .exe para Windows": genera el ejecutable del juego
    /// (sólo con su escena) en Builds/ReinoDeLasRunas y lo comprime en Assets/ReinoDeLasRunas/Build~/ReinoDeLasRunas-Windows.zip
    /// (la carpeta con "~" no la importa Unity, pero sí se sube al repositorio).
    /// </summary>
    public static class GameBuilder
    {
        const string ScenePath = "Assets/ReinoDeLasRunas/Scenes/CombateCastillo.unity";
        const string ExeName = "ReinoDeLasRunas.exe";
        const string ProductName = "Reino de las Runas";

        [MenuItem("Reino de las Runas/Compilar .exe para Windows", priority = 40)]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog(ProductName, "No se encontró la escena " + ScenePath + ". Construila primero desde el menú.", "OK");
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(projectRoot, "Builds", "ReinoDeLasRunas");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);

            string prevName = PlayerSettings.productName;
            PlayerSettings.productName = ProductName;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(outDir, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.productName = prevName;
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(ProductName, "La compilación falló. Revisá la consola.", "OK");
                return;
            }

            // Carpetas de depuración que no hay que distribuir
            foreach (var d in Directory.GetDirectories(outDir, "*DoNotShip*")) Directory.Delete(d, true);

            string zipDir = Path.Combine(Application.dataPath, "ReinoDeLasRunas", "Build~");
            Directory.CreateDirectory(zipDir);
            string zipPath = Path.Combine(zipDir, "ReinoDeLasRunas-Windows.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(outDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            long mb = new FileInfo(zipPath).Length / (1024 * 1024);
            Debug.Log($"[{ProductName}] Ejecutable listo: {Path.Combine(outDir, ExeName)}  ·  ZIP ({mb} MB): {zipPath}");
            EditorUtility.DisplayDialog(ProductName, $"¡Listo!\n\nEjecutable: {Path.Combine(outDir, ExeName)}\nZIP para subir ({mb} MB): {zipPath}", "OK");
        }

        [MenuItem("Reino de las Runas/Compilar WebGL para itch.io", priority = 41)]
        public static void BuildWebGL()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorUtility.DisplayDialog(ProductName, "Unity todavía está compilando scripts. Esperá unos segundos y volvé a intentar.", "OK");
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog(ProductName, "Falta instalar el módulo \"WebGL Build Support\" desde Unity Hub (Instalaciones → engranaje → Agregar módulos).", "OK");
                return;
            }
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog(ProductName, "No se encontró la escena " + ScenePath + ".", "OK");
                return;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(projectRoot, "Builds", "ReinoDeLasRunas_WebGL");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);

            string prevName = PlayerSettings.productName;
            var prevCompression = PlayerSettings.WebGL.compressionFormat;
            bool prevFallback = PlayerSettings.WebGL.decompressionFallback;
            int prevW = PlayerSettings.defaultWebScreenWidth, prevH = PlayerSettings.defaultWebScreenHeight;

            // WebGL no soporta Forward+: si el renderer usa Forward+, los shaders iluminados no se incluyen
            // y los objetos quedan invisibles. Para esta compilación usamos Forward (y después se restaura).
            var renderers = AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" })
                .Select(g => AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(r => r != null).ToList();
            var prevModes = new List<RenderingMode>();
            foreach (var r in renderers)
            {
                prevModes.Add(r.renderingMode);
                if (r.renderingMode != RenderingMode.Forward) { r.renderingMode = RenderingMode.Forward; EditorUtility.SetDirty(r); }
            }
            AssetDatabase.SaveAssets();

            PlayerSettings.productName = ProductName;
            // itch.io no siempre envía los encabezados de compresión: Gzip + "fallback" funciona en cualquier servidor.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outDir,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.productName = prevName;
                PlayerSettings.WebGL.compressionFormat = prevCompression;
                PlayerSettings.WebGL.decompressionFallback = prevFallback;
                PlayerSettings.defaultWebScreenWidth = prevW;
                PlayerSettings.defaultWebScreenHeight = prevH;
                for (int i = 0; i < renderers.Count; i++)
                {
                    renderers[i].renderingMode = prevModes[i];
                    EditorUtility.SetDirty(renderers[i]);
                }
                AssetDatabase.SaveAssets();
            }

            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(ProductName, "La compilación WebGL falló. Revisá la consola.", "OK");
                return;
            }

            string zipPath = Path.Combine(projectRoot, "Builds", "ReinoDeLasRunas-WebGL-itchio.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(outDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, false);
            long mb = new FileInfo(zipPath).Length / (1024 * 1024);
            Debug.Log($"[{ProductName}] WebGL listo para itch.io ({mb} MB): {zipPath}");
            EditorUtility.DisplayDialog(ProductName, $"¡WebGL listo!\n\nSubí este ZIP a itch.io como proyecto HTML ({mb} MB):\n{zipPath}", "OK");
        }
    }
}
