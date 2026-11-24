using ImGuiNET;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor.Panels;

/// <summary>
/// Motor ve oyun loglarını (Info, Warning, Error) listeleyen Konsol Paneli.
/// </summary>
public class ConsolePanel
{
    private readonly EditorContext _context;
    private bool _autoScroll = true;
    private string _filter = "";

    public ConsolePanel(EditorContext context)
    {
        _context = context;
    }

    public void Draw()
    {
        if (ImGui.Begin("Console##ConsolePanel"))
        {
            DrawContent();
        }
        ImGui.End();
    }

    private string _commandInput = "";
    private LogLevel? _filterLevel = null;

    public void DrawContent()
    {
        // 1. Üst Kontrol Çubuğu (Clear, Auto-Scroll, Log Seviye Filtreleri)
        if (ImGui.Button("Clear"))
        {
            _context.ClearLogs();
        }

        ImGui.SameLine();
        ImGui.Checkbox("Auto-scroll", ref _autoScroll);

        ImGui.SameLine();
        ImGui.InputTextWithHint("##ConsoleFilter", "Filter text...", ref _filter, 32);

        // Seviye sayaçları
        int infoCount = 0, warnCount = 0, errCount = 0;
        for (int i = 0; i < _context.Logs.Count; i++)
        {
            var l = _context.Logs[i].Level;
            if (l == LogLevel.Info) infoCount++;
            else if (l == LogLevel.Warning) warnCount++;
            else if (l == LogLevel.Error) errCount++;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"All ({_context.Logs.Count})")) _filterLevel = null;

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new NumVec4(0.4f, 0.8f, 1f, 1f));
        if (ImGui.SmallButton($"Info ({infoCount})")) _filterLevel = LogLevel.Info;
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new NumVec4(1f, 0.75f, 0.2f, 1f));
        if (ImGui.SmallButton($"Warn ({warnCount})")) _filterLevel = LogLevel.Warning;
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new NumVec4(1f, 0.35f, 0.35f, 1f));
        if (ImGui.SmallButton($"Error ({errCount})")) _filterLevel = LogLevel.Error;
        ImGui.PopStyleColor();

        ImGui.Separator();

        // 2. Log Kaydırma Alanı
        float footerHeight = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();
        ImGui.BeginChild("LogScrollingRegion", new NumVec2(0, -footerHeight), ImGuiChildFlags.None);

        for (int i = 0; i < _context.Logs.Count; i++)
        {
            var entry = _context.Logs[i];

            if (_filterLevel.HasValue && entry.Level != _filterLevel.Value)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(_filter) &&
                !entry.Message.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            NumVec4 color = entry.Level switch
            {
                LogLevel.Warning => new NumVec4(1f, 0.75f, 0.2f, 1f),
                LogLevel.Error => new NumVec4(1f, 0.3f, 0.3f, 1f),
                _ => new NumVec4(0.85f, 0.88f, 0.9f, 1f)
            };

            string tag = entry.Level switch
            {
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]",
                _ => "[INFO]"
            };

            ImGui.TextDisabled($"[{entry.Timestamp:HH:mm:ss}]");
            ImGui.SameLine();
            ImGui.TextColored(color, $"{tag} {entry.Message}");
        }

        if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
        {
            ImGui.SetScrollHereY(1.0f);
        }

        ImGui.EndChild();

        ImGui.Separator();

        // 3. Komut Satırı (CLI) Girişi
        ImGui.PushItemWidth(-60);
        bool reclaimFocus = false;
        if (ImGui.InputTextWithHint("##ConsoleCmd", "Enter command ('help' for list)...", ref _commandInput, 128, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            ExecuteCommand(_commandInput.Trim());
            _commandInput = "";
            reclaimFocus = true;
        }
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (ImGui.Button("Exec", new NumVec2(50, 0)))
        {
            ExecuteCommand(_commandInput.Trim());
            _commandInput = "";
            reclaimFocus = true;
        }

        if (reclaimFocus)
        {
            ImGui.SetItemDefaultFocus();
            ImGui.SetKeyboardFocusHere(-1);
        }
    }

    private void ExecuteCommand(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return;

        _context.Log($"> {cmd}", LogLevel.Info);
        var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string main = parts[0].ToLowerInvariant();

        switch (main)
        {
            case "help":
                _context.Log("Available commands: help, clear, fps, play, stop, pause, scene.list, select <name>, snap <size>", LogLevel.Info);
                break;
            case "clear":
                _context.ClearLogs();
                break;
            case "fps":
                _context.Log($"FPS: {_context.FPS:0.0} ({_context.FrameTimeMs:0.00} ms)", LogLevel.Info);
                break;
            case "play":
                _context.StartPlayMode();
                break;
            case "stop":
                _context.StopPlayMode();
                break;
            case "pause":
                _context.IsPaused = !_context.IsPaused;
                _context.Log(_context.IsPaused ? "Game paused." : "Game resumed.", LogLevel.Info);
                break;
            case "scene.list":
                if (_context.ActiveScene != null)
                {
                    _context.Log($"Scene: {_context.ActiveScene.Name} ({_context.ActiveScene.Entities.Count} entities):", LogLevel.Info);
                    foreach (var e in _context.ActiveScene.Entities)
                    {
                        if (!e.IsDestroyed) _context.Log($"  - {e.Name} [Tag: {e.Tag}, Active: {e.IsActive}]", LogLevel.Info);
                    }
                }
                break;
            case "select":
                if (parts.Length > 1 && _context.ActiveScene != null)
                {
                    string target = parts[1];
                    var found = _context.ActiveScene.Entities.FirstOrDefault(e => e.Name.Equals(target, StringComparison.OrdinalIgnoreCase) && !e.IsDestroyed);
                    if (found != null)
                    {
                        _context.SelectedEntity = found;
                        _context.Log($"Selected entity: {found.Name}", LogLevel.Info);
                    }
                    else
                    {
                        _context.Log($"Entity not found: {target}", LogLevel.Warning);
                    }
                }
                break;
            case "snap":
                if (parts.Length > 1 && float.TryParse(parts[1], out float sz))
                {
                    _context.GridSnapSize = sz;
                    _context.SnapToGrid = true;
                    _context.Log($"Grid snap set to {sz}px (Enabled).", LogLevel.Info);
                }
                break;
            default:
                _context.Log($"Unknown command: '{cmd}'. Type 'help' for available commands.", LogLevel.Warning);
                break;
        }
    }
}
