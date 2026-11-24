using System.Diagnostics;

namespace LightEngine.Editor.Export;

/// <summary>
/// Oyunun tek dosya çalıştırılabilir .exe olarak dışa aktarılması için paketleme seçenekleri.
/// </summary>
public class ExportOptions
{
    public string GameProjectPath { get; set; } = "";
    public string OutputDirectory { get; set; } = "";
    public string RuntimeIdentifier { get; set; } = "win-x64";
    public bool SelfContained { get; set; } = true;
    public bool PublishSingleFile { get; set; } = true;
}

/// <summary>
/// Light Engine oyun projesini bağımsız (standalone) çalıştırılabilir .exe dosyası olarak
/// derleyip paketleyen dışa aktarım (Export) aracı.
/// </summary>
public static class GameExporter
{
    public static Task<(bool Success, string Summary)> ExportAsync(ExportOptions options, Action<string>? onOutput = null)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(options.GameProjectPath) || !File.Exists(options.GameProjectPath))
            {
                return (false, $"Game project file not found: {options.GameProjectPath}");
            }

            Directory.CreateDirectory(options.OutputDirectory);

            string args = $"publish \"{options.GameProjectPath}\" -c Release -r {options.RuntimeIdentifier} " +
                          $"--self-contained {(options.SelfContained ? "true" : "false")} " +
                          $"-p:PublishSingleFile={(options.PublishSingleFile ? "true" : "false")} " +
                          $"-p:IncludeNativeLibrariesForSelfExtract=true " +
                          $"-o \"{options.OutputDirectory}\"";

            onOutput?.Invoke($"[Export] dotnet {args}");

            var psi = new ProcessStartInfo("dotnet", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    return (false, "Failed to start dotnet publish process.");
                }

                proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onOutput?.Invoke(e.Data); };
                proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) onOutput?.Invoke($"[ERR] {e.Data}"); };

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();

                if (proc.ExitCode == 0)
                {
                    return (true, $"Export succeeded! Output: {options.OutputDirectory}");
                }
                return (false, $"Export failed with exit code {proc.ExitCode}.");
            }
            catch (Exception ex)
            {
                return (false, $"Export exception: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Çalışma dizininden yukarı doğru tarayarak Game/Game.csproj dosyasının yolunu bulur.
    /// </summary>
    public static string FindGameProjectPath()
    {
        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            string candidate = Path.Combine(dir, "Game", "Game.csproj");
            if (File.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Path.Combine(Environment.CurrentDirectory, "Game", "Game.csproj");
    }
}
