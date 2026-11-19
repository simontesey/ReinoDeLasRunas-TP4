using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

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
    }
}
