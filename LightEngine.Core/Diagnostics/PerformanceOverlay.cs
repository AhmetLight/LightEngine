using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Input;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace LightEngine.Core.Diagnostics;

/// <summary>
/// Oyun performansını (FPS, kare süresi, bellek kullanımı, nesne sayısı) anlık gösteren performans katmanı.
/// Varsayılan olarak F1 tuşu ile açılıp kapatılabilir.
/// </summary>
public class PerformanceOverlay
{
    private float _minFps = 9999f;
    private float _maxFps = 0f;
    private float _resetStatsTimer;

    /// <summary>
    /// Katmanın ekranda çizilip çizilmeyeceği.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Katmanı açıp kapatacak kısayol tuşu.
    /// </summary>
    public Keys ToggleKey { get; set; } = Keys.F1;

    public void Update(float deltaTime)
    {
        if (Engine.Services.TryGet<IInputService>(out var input) && input != null)
        {
            if (input.IsKeyPressed(ToggleKey))
            {
                IsVisible = !IsVisible;
            }
        }

        // Min/Max FPS istatistiklerini her 3 saniyede bir sıfırlayarak güncel tut
        _resetStatsTimer += deltaTime;
        if (_resetStatsTimer >= 3.0f)
        {
            _minFps = 9999f;
            _maxFps = 0f;
            _resetStatsTimer = 0f;
        }

        float currentFps = Engine.Time.FPS;
        if (currentFps > 0f)
        {
            if (currentFps < _minFps) _minFps = currentFps;
            if (currentFps > _maxFps) _maxFps = currentFps;
        }
    }

    public void Draw(SpriteBatchRenderer renderer)
    {
        if (!IsVisible) return;

        var time = Engine.Time;
        long memoryBytes = GC.GetTotalMemory(false);
        float memoryMb = memoryBytes / (1024f * 1024f);

        int entityCount = 0;
        string sceneName = "None";
        if (Engine.Services.TryGet<ISceneService>(out var sceneService) && sceneService?.ActiveScene != null)
        {
            entityCount = sceneService.ActiveScene.Entities.Count;
            sceneName = sceneService.ActiveScene.Name;
        }

        // Bilgi metni
        string fpsText = $"FPS: {time.FPS:0} (Avg: {time.AverageFPS:0} | Min: {_minFps:0} | Max: {_maxFps:0})";
        string frameTimeText = $"Frame Time: {time.FrameTimeMs:0.00} ms (Fixed: {time.FixedDeltaTime * 1000f:0.0} ms)";
        string memoryText = $"Memory: {memoryMb:0.00} MB | Draw Calls: {renderer.DrawCallCount}";
        string sceneText = $"Scene: {sceneName} | Entities: {entityCount}";
        string toggleHint = $"[F1] Overlay Ac/Kapat";

        // Yarı saydam arka plan kutusu
        Rectangle bgRect = new(10, 10, 340, 92);
        renderer.DrawFilledRectangle(bgRect, new Color(0, 0, 0, 180));
        renderer.DrawRectangle(bgRect, new Color(80, 160, 255, 200), 1);

        // Metinleri DebugFont ile çiz
        Vector2 textPos = new(18, 16);
        float lineHeight = 14f;

        renderer.DrawDebugText("Light Engine v0.1 - Core 2D", textPos, Color.Gold);
        textPos.Y += lineHeight;

        Color fpsColor = time.FPS >= 55f ? Color.LightGreen : (time.FPS >= 30f ? Color.Orange : Color.Red);
        renderer.DrawDebugText(fpsText, textPos, fpsColor);
        textPos.Y += lineHeight;

        renderer.DrawDebugText(frameTimeText, textPos, Color.LightCyan);
        textPos.Y += lineHeight;

        renderer.DrawDebugText(memoryText, textPos, Color.White);
        textPos.Y += lineHeight;

        renderer.DrawDebugText(sceneText, textPos, Color.LightGray);
        textPos.Y += lineHeight;

        renderer.DrawDebugText(toggleHint, textPos, Color.DarkGray);
    }
}
