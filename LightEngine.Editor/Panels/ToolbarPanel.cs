using ImGuiNET;
using LightEngine.Core.Common;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor.Panels;

/// <summary>
/// Editörün en üstünde yer alan Play, Pause, Stop ve motor hızı araç çubuğu (Toolbar).
/// </summary>
public class ToolbarPanel
{
    private readonly EditorContext _context;

    public ToolbarPanel(EditorContext context)
    {
        _context = context;
    }

    public void Draw()
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(new NumVec2(viewport.Pos.X, viewport.Pos.Y + 24)); // Menü çubuğunun hemen altı
        ImGui.SetNextWindowSize(new NumVec2(viewport.Size.X, 36));

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration |
                                ImGuiWindowFlags.NoScrollbar |
                                ImGuiWindowFlags.NoScrollWithMouse |
                                ImGuiWindowFlags.NoBringToFrontOnFocus;

        if (ImGui.Begin("##EditorToolbar", flags))
        {
            float availWidth = ImGui.GetContentRegionAvail().X;
            float buttonsWidth = 240f;
            // Sol taraf: 2D / 3D Mod Seçimi, Gizmo (W / E / R) ve Snap
            ImGui.SetCursorPosX(12f);

            bool is3D = _context.Is3DMode;
            if (!is3D) ImGui.PushStyleColor(ImGuiCol.Button, new NumVec4(0.18f, 0.52f, 0.88f, 1f));
            if (ImGui.Button("2D", new NumVec2(34, 24))) _context.Is3DMode = false;
            if (!is3D) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("2D Scene View Mode");

            ImGui.SameLine(0, 2);
            if (is3D) ImGui.PushStyleColor(ImGuiCol.Button, new NumVec4(0.18f, 0.52f, 0.88f, 1f));
            if (ImGui.Button("3D", new NumVec2(34, 24))) _context.Is3DMode = true;
            if (is3D) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("3D Scene View Mode (RMB+WASD Fly)");

            ImGui.SameLine(0, 12);
            if (ImGui.RadioButton("W##GizmoT", _context.ActiveGizmo == GizmoType.Translate)) _context.ActiveGizmo = GizmoType.Translate;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Translate Tool (W)");
            ImGui.SameLine();
            if (ImGui.RadioButton("E##GizmoR", _context.ActiveGizmo == GizmoType.Rotate)) _context.ActiveGizmo = GizmoType.Rotate;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Rotate Tool (E)");
            ImGui.SameLine();
            if (ImGui.RadioButton("R##GizmoS", _context.ActiveGizmo == GizmoType.Scale)) _context.ActiveGizmo = GizmoType.Scale;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Scale Tool (R)");
            ImGui.SameLine();
            bool snap = _context.SnapToGrid;
            if (ImGui.Checkbox("Snap", ref snap)) _context.SnapToGrid = snap;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Grid Snap (Hold Ctrl)");

            ImGui.SameLine(0, 12);
            if (ImGui.Button("Hot Reload C#", new NumVec2(105, 24)))
            {
                if (Engine.Services.TryGet<LightEngine.Core.Scripting.IScriptService>(out var scriptSvc) && scriptSvc != null)
                {
                    if (scriptSvc.ReloadScripts(_context.ActiveScene))
                    {
                        _context.Log($"Scripts reloaded ({scriptSvc.AvailableScriptNames.Count} loaded).", LogLevel.Info);
                    }
                    else
                    {
                        foreach (var err in scriptSvc.LastDiagnostics)
                            _context.Log(err, LogLevel.Error);
                    }
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Recompile & Hot Reload C# scripts in Assets/Scripts");

            // Görünüm Hızlı Seçimi: [Scene View] [Game View]
            ImGui.SameLine(0, 15);
            if (ImGui.Button("Scene View", new NumVec2(82, 24)))
            {
                _context.FocusSceneTabRequested = true;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Switch to Scene Viewport (Editing & Gizmos)");

            ImGui.SameLine(0, 4);
            if (ImGui.Button("Game View", new NumVec2(82, 24)))
            {
                _context.FocusGameTabRequested = true;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Switch to Game Viewport (Pure Runtime Play)");

            // Orta: Play / Pause / Stop butonları
            ImGui.SameLine();
            ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX() + 10f, (availWidth - buttonsWidth) / 2f));

            // 1. PLAY / STOP BUTONU
            bool isPlaying = _context.IsPlaying;
            if (isPlaying)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new NumVec4(0.85f, 0.22f, 0.22f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NumVec4(0.95f, 0.32f, 0.32f, 1f));
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new NumVec4(0.18f, 0.62f, 0.32f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NumVec4(0.25f, 0.75f, 0.4f, 1f));
            }

            if (ImGui.Button(isPlaying ? "Stop [X]" : "Play [ > ]", new NumVec2(96, 24)))
            {
                if (_context.IsPlaying)
                {
                    _context.StopPlayMode();
                }
                else
                {
                    _context.StartPlayMode();
                }
            }
            ImGui.PopStyleColor(2);

            ImGui.SameLine(0, 6);

            // 2. PAUSE BUTONU
            bool isPaused = _context.IsPaused;
            if (isPaused) ImGui.PushStyleColor(ImGuiCol.Button, new NumVec4(0.85f, 0.6f, 0.2f, 1f));

            if (ImGui.Button(isPaused ? "Resume [||]" : "Pause [||]", new NumVec2(88, 24)))
            {
                if (_context.IsPlaying)
                {
                    _context.IsPaused = !_context.IsPaused;
                    _context.Log(_context.IsPaused ? "Game paused." : "Game resumed.");
                }
            }

            if (isPaused) ImGui.PopStyleColor();

            // Sağ köşe: 3D Kalite + FPS/RAM
            ImGui.SameLine();
            ImGui.SetCursorPosX(Math.Max(ImGui.GetCursorPosX() + 10f, availWidth - 420f));

            ImGui.SetNextItemWidth(76f);
            int qIdx = (int)_context.Quality3D;
            if (ImGui.Combo("##Quality3D", ref qIdx, "Low\0Med\0High\0"))
            {
                _context.Quality3D = (LightEngine.Core.Rendering3D.QualityPreset)qIdx;
                if (Engine.Services.TryGet<LightEngine.Core.Rendering3D.IRender3DService>(out var r3d) && r3d != null)
                {
                    r3d.Quality.ApplyPreset(_context.Quality3D);
                }
                _context.Log($"3D Quality preset set to: {_context.Quality3D}");
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("3D Render Quality (Low / Medium / High)");

            ImGui.SameLine(0, 12);
            long memory = GC.GetTotalMemory(false) / (1024 * 1024);
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            ImGui.TextColored(new NumVec4(0.3f, 0.9f, 0.4f, 1f), $"FPS: {_context.FPS.ToString("F1", ci)} ({_context.FrameTimeMs.ToString("F2", ci)} ms) | RAM: {memory} MB");
        }
        ImGui.End();
    }
}
