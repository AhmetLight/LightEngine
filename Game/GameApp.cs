using Game.Scenes;
using LightEngine.Core;
using LightEngine.Core.Audio;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework;

namespace Game;

/// <summary>
/// LightEngine üzerine kurulu oyun uygulaması.
/// Aşama 2: 2D oyun özelliklerini (Animasyon, Tilemap, Fizik/Tetikleyici, Parçacık, Ses, JSON)
/// başlatan ve zengin HUD bilgilerini ekrana çizen ana oyun sınıfı.
/// </summary>
public class GameApp : LightEngineGame
{
    private int _frameCount;
    private bool _screenshotTaken;

    public GameApp() : base("Light Engine - Hybrid 2D/3D Sample Game (F1: 2D | F2: 3D)", 1280, 720)
    {
        ClearColor = new Color(20, 22, 28);
    }

    protected override void OnEngineContentLoad()
    {
        base.OnEngineContentLoad();

        var sceneService = Engine.Services.Get<ISceneService>();
        sceneService.RegisterScene("DemoScene", () => new DemoScene());
        sceneService.RegisterScene("DemoScene3D", () => new DemoScene3D());
        sceneService.LoadScene("DemoScene3D");
    }

    protected override void OnEngineUpdate(float deltaTime)
    {
        base.OnEngineUpdate(deltaTime);
        _frameCount++;

        var input = Engine.Services.Get<LightEngine.Core.Input.IInputService>();
        if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.F1))
        {
            Engine.Services.Get<ISceneService>().LoadScene("DemoScene");
        }
        else if (input.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.F2))
        {
            Engine.Services.Get<ISceneService>().LoadScene("DemoScene3D");
        }

        if (!_screenshotTaken && _frameCount >= 45)
        {
            _screenshotTaken = true;
            RequestScreenshot("screenshot.png");

            string artifactDir = @"C:\Users\ahmet\.gemini\antigravity\brain\5b00d67c-e274-4d62-ad5f-4c66396fb6ab";
            if (Directory.Exists(artifactDir))
            {
                RequestScreenshot(Path.Combine(artifactDir, "phase2_preview.png"));
            }
        }
    }

    protected override void OnEngineScreenDraw(SpriteBatchRenderer renderer)
    {
        base.OnEngineScreenDraw(renderer);

        int screenWidth = GraphicsDevice.Viewport.Width;

        // 1. SAĞ ÜST: KONTROLLER VE TEST REHBERİ
        int boxWidth = 390;
        int boxHeight = 190;
        int startX = screenWidth - boxWidth - 15;
        int startY = 15;

        Rectangle infoBox = new(startX, startY, boxWidth, boxHeight);
        renderer.DrawFilledRectangle(infoBox, new Color(0, 0, 0, 195));
        renderer.DrawRectangle(infoBox, new Color(90, 130, 180, 220), 1);

        Vector2 textPos = new(startX + 12, startY + 10);
        float lineStep = 15f;

        renderer.DrawDebugText("ASAMA 2: 2D OYUN OZELLIKLERI", textPos, Color.Gold);
        textPos.Y += lineStep;

        renderer.DrawDebugText("W, A, S, D       : Hareket (Anim State Machine + Trail)", textPos, Color.White);
        textPos.Y += lineStep;

        renderer.DrawDebugText("Fare Sol / Enter : Lazer Sesi + Mermi (Object Pool)", textPos, Color.Cyan);
        textPos.Y += lineStep;

        renderer.DrawDebugText("E / Space        : Patlama Sesi + Parcacik Patlamasi", textPos, Color.Orange);
        textPos.Y += lineStep;

        renderer.DrawDebugText("Fizik & Trigger  : Yesil Kutular Engel | Sari Daire Para", textPos, Color.LightGreen);
        textPos.Y += lineStep;

        renderer.DrawDebugText("F5: Sahne JSON Kaydet | F6: Sahne JSON Yukle", textPos, Color.DeepSkyBlue);
        textPos.Y += lineStep;

        bool isMuted = Engine.Services.TryGet<IAudioService>(out var audio) && audio != null && audio.IsMuted;
        string soundStatus = isMuted ? "SES: KAPALI (Mute)" : "SES: ACIK";
        Color soundColor = isMuted ? Color.Red : Color.LimeGreen;
        renderer.DrawDebugText($"M: Sesi Ac/Kapa [{soundStatus}]", textPos, soundColor);
        textPos.Y += lineStep;

        renderer.DrawDebugText("Tekerlek: Zoom | R: Zoom Reset | F1: FPS Katmani", textPos, Color.LightGray);

        // 2. SOL ALT: OYUN DURUMU (Toplanan Altın & Bildirimler)
        var sceneService = Engine.Services.Get<ISceneService>();
        if (sceneService.ActiveScene is DemoScene demo)
        {
            Rectangle statsBox = new(15, GraphicsDevice.Viewport.Height - 75, 320, 60);
            renderer.DrawFilledRectangle(statsBox, new Color(0, 0, 0, 195));
            renderer.DrawRectangle(statsBox, Color.Gold, 1);

            Vector2 statsPos = new(statsBox.X + 12, statsBox.Y + 8);
            renderer.DrawDebugText($"TOPLANAN ALTIN : {demo.CoinsCollected} / 10", statsPos, Color.Gold);

            statsPos.Y += 18f;
            if (!string.IsNullOrEmpty(demo.StatusBanner))
            {
                renderer.DrawDebugText(demo.StatusBanner, statsPos, Color.Cyan);
            }
            else
            {
                renderer.DrawDebugText("Tilemap + AABB + Trigger + Particles Aktif", statsPos, Color.LightGray);
            }
        }
    }
}
