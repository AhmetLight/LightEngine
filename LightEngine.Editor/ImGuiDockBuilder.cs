using System;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace LightEngine.Editor;

/// <summary>
/// cimgui yerel kütüphanesindeki DockBuilder C API fonksiyonlarına P/Invoke erişimi sağlar.
/// Pencerelerin (Hierarchy, Scene Viewport, Inspector, Asset Browser, Console)
/// hatasız ve stabil olarak istenilen oranlarda dock edilmesini sağlar.
/// </summary>
public static class ImGuiDockBuilder
{
    private const string DllName = "cimgui";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint igDockBuilderAddNode(uint node_id, ImGuiDockNodeFlags flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderRemoveNode(uint node_id);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderSetNodePos(uint node_id, Vector2 pos);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderSetNodeSize(uint node_id, Vector2 size);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint igDockBuilderSplitNode(uint node_id, ImGuiDir split_dir, float size_ratio_for_node_at_dir, out uint out_id_at_dir, out uint out_id_at_opposite_dir);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderDockWindow(string window_name, uint node_id);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void igDockBuilderFinish(uint node_id);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr igDockBuilderGetNode(uint node_id);
}
