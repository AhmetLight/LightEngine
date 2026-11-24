using System.Diagnostics;
using ImGuiNET;
using LightEngine.Core;
using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Physics2D;
using LightEngine.Core.Rendering;
using LightEngine.Editor.Panels;
using Microsoft.Xna.Framework;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor;

/// <summary>
/// Light Engine'in görsel arayüze (Docking, Hierarchy, Inspector, Asset Browser, Viewport) sahip
/// profesyonel ImGui editör uygulaması.
/// Draw döngüsünde Stopwatch ile gerçek FPS/ms ölçümü, VSync/FixedTimeStep ayarları ve sahne etkileşimi sağlar.
/// </summary>
public class EditorApp : LightEngineGame
{
    private ImGuiRenderer _imGuiRenderer = null!;
    private EditorContext _context = null!;

    private HierarchyPanel _hierarchyPanel = null!;
    private InspectorPanel _inspectorPanel = null!;
    private AssetBrowserPanel _assetBrowserPanel = null!;
    private ViewportPanel _viewportPanel = null!;
    private GameViewportPanel _gameViewportPanel = null!;
    private ToolbarPanel _toolbarPanel = null!;
    private ConsolePanel _consolePanel = null!;

    // Gerçek zamanlı kare süresi ve FPS ölçüm kronometresi (Draw tabanlı)
    private readonly Stopwatch _frameStopwatch = new();
    private float _smoothedFps = 60f;
    private float _smoothedFrameTimeMs = 16.6f;

    private int _editorFrameCount;
    private bool _screenshotCaptured;
    private bool _isFirstLayout = true;
    private bool _resetLayoutRequested = false;

    // Export Game (.exe / binary) pencere durumu
    private bool _showExportDialog;
    private string _exportOutputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "LightEngine_Build");
    private bool _exportSingleFile = true;
    private bool _exportSelfContained = true;
    private bool _isExporting;
    private string _exportStatus = "Ready to export standalone executable.";
    private int _exportTargetIndex;
    private readonly string[] _exportTargetNames = new[] { "Windows x64 (.exe)", "Linux x64 (ELF standalone)", "macOS ARM64 (Apple Silicon)", "macOS x64 (Intel)" };
    private readonly string[] _exportTargetRids = new[] { "win-x64", "linux-x64", "osx-arm64", "osx-x64" };

    public EditorApp() : base("Light Engine Editor v1.0 - [2D + 3D Hybrid]", 1600, 900)
    {
        Window.AllowUserResizing = true;
        ClearColor = new Color(18, 19, 24);
        PerformanceOverlay.IsVisible = false;

        // Kullanıcı isteği: Varsayılan olarak sabit zaman adımı kapalı
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        _context = new EditorContext();
        _context.Log("Light Engine Editörü başlatıldı.", LogLevel.Info);
        _context.Log(".NET 9 + MonoGame DesktopGL + ImGui.NET Docking aktif.", LogLevel.Info);

        // Grafik ve VSync tercihlerini uygula
        GraphicsManager.PreferMultiSampling = true;
        GraphicsManager.SynchronizeWithVerticalRetrace = _context.VSync;
        GraphicsManager.ApplyChanges();
        IsFixedTimeStep = _context.IsFixedTimeStep;

        _context.OnPreferencesChanged += ApplyPreferences;

        base.Initialize();
        _frameStopwatch.Start();
    }

    private void ApplyPreferences()
    {
        GraphicsManager.SynchronizeWithVerticalRetrace = _context.VSync;
        GraphicsManager.ApplyChanges();
        IsFixedTimeStep = _context.IsFixedTimeStep;
    }

    protected override void LoadContent()
    {
        base.LoadContent();

        _imGuiRenderer = new ImGuiRenderer(this);

        _hierarchyPanel = new HierarchyPanel(_context);
        _inspectorPanel = new InspectorPanel(_context);
        _assetBrowserPanel = new AssetBrowserPanel(_context);
        _viewportPanel = new ViewportPanel(GraphicsDevice, _imGuiRenderer, _context);
        _gameViewportPanel = new GameViewportPanel(GraphicsDevice, _imGuiRenderer, _context);
        _toolbarPanel = new ToolbarPanel(_context);
        _consolePanel = new ConsolePanel(_context);

        // C# Scripting & Hot Reload sistemini başlat
        var scriptService = Engine.Services.Get<LightEngine.Core.Scripting.IScriptService>();
        string scriptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Scripts");
        scriptService.OnScriptsReloaded += count => _context.Log($"[Hot Reload] {count} C# script(s) compiled & ready.", LogLevel.Info);
        scriptService.OnCompileError += errors =>
        {
            foreach (var err in errors) _context.Log($"[Script Error] {err}", LogLevel.Error);
        };
        scriptService.Initialize(scriptsDir);

        // Editör için zengin hibrit 2D + 3D örnek sahne oluştur
        SetupEditorScene();
    }

    private void SetupEditorScene()
    {
        var sceneService = Engine.Services.Get<ISceneService>();
        var assetService = Engine.Services.Get<IAssetService>();
        var render3D = Engine.Services.Get<LightEngine.Core.Rendering3D.IRender3DService>();
        var scriptService = Engine.Services.Get<LightEngine.Core.Scripting.IScriptService>();

        var scene = new Scene("HybridScene_2D_3D");

        // --- 3D SAHNE VARLIKLARI ---
        // 1. Güneş Işığı (Directional Light)
        var sun = scene.CreateEntity("Sun_DirectionalLight");
        sun.Tag = "Light";
        sun.AddComponent(new LightEngine.Core.Rendering3D.Transform3D
        {
            LocalPosition = new Vector3(0f, 8f, 0f),
            LocalRotationDegrees = new Vector3(-45f, 35f, 0f)
        });
        sun.AddComponent(new LightEngine.Core.Rendering3D.Light3D
        {
            LightType = LightEngine.Core.Rendering3D.LightType3D.Directional,
            Color = new Color(255, 245, 228),
            Intensity = 1.05f
        });

        // 2. Zemin Düzlemi (Ground Plane)
        var floor = scene.CreateEntity("Floor_Plane_3D");
        floor.Tag = "Ground";
        floor.AddComponent(new LightEngine.Core.Rendering3D.Transform3D
        {
            LocalPosition = new Vector3(0f, 0f, 0f),
            LocalScale = new Vector3(1.5f, 1f, 1.5f)
        });
        floor.AddComponent(new LightEngine.Core.Rendering3D.MeshRenderer3D(
            render3D.GetPrimitiveMesh(LightEngine.Core.Rendering3D.PrimitiveType3D.Plane),
            new LightEngine.Core.Rendering3D.Material3D { DiffuseColor = new Color(75, 84, 98), SpecularPower = 12f }));
        floor.AddComponent(new LightEngine.Core.Physics3D.RigidBody3D(
            LightEngine.Core.Physics3D.BodyType3D.Static,
            LightEngine.Core.Physics3D.ColliderShape3D.Box,
            mass: 0f) { BoxSize = new Vector3(15f, 0.2f, 15f) });

        // 3. Dönen Ana Küp (AutoRotator C# Betiği bağlı!)
        var heroCube = scene.CreateEntity("Hero_Cube_3D");
        heroCube.Tag = "Player3D";
        heroCube.AddComponent(new LightEngine.Core.Rendering3D.Transform3D
        {
            LocalPosition = new Vector3(0f, 1.1f, 0f),
            LocalScale = new Vector3(1.2f, 1.2f, 1.2f)
        });
        heroCube.AddComponent(new LightEngine.Core.Rendering3D.MeshRenderer3D(
            render3D.GetPrimitiveMesh(LightEngine.Core.Rendering3D.PrimitiveType3D.Cube),
            new LightEngine.Core.Rendering3D.Material3D { DiffuseColor = new Color(70, 165, 245), SpecularPower = 48f, Roughness = 0.25f }));
        var rotatorScript = scriptService.CreateScriptInstance("AutoRotator");
        if (rotatorScript != null) heroCube.AddComponent(rotatorScript);

        // 4. Kırmızı Fizikli Küre (BepuPhysics v2 Dinamik Küre)
        var sphere = scene.CreateEntity("Metal_Sphere_3D");
        sphere.Tag = "Prop3D";
        sphere.AddComponent(new LightEngine.Core.Rendering3D.Transform3D
        {
            LocalPosition = new Vector3(2.8f, 2.5f, -1.2f),
            LocalScale = new Vector3(1.4f, 1.4f, 1.4f)
        });
        sphere.AddComponent(new LightEngine.Core.Rendering3D.MeshRenderer3D(
            render3D.GetPrimitiveMesh(LightEngine.Core.Rendering3D.PrimitiveType3D.Sphere),
            new LightEngine.Core.Rendering3D.Material3D { DiffuseColor = new Color(235, 95, 85), SpecularPower = 96f, Metallic = 0.65f, Roughness = 0.2f }));
        sphere.AddComponent(new LightEngine.Core.Physics3D.RigidBody3D(
            LightEngine.Core.Physics3D.BodyType3D.Dynamic,
            LightEngine.Core.Physics3D.ColliderShape3D.Sphere,
            mass: 2.0f) { Radius = 0.5f });

        // 5. Yeşil Silindir (Cylinder)
        var pillar = scene.CreateEntity("Pillar_Cylinder_3D");
        pillar.Tag = "Prop3D";
        pillar.AddComponent(new LightEngine.Core.Rendering3D.Transform3D
        {
            LocalPosition = new Vector3(-2.8f, 1.0f, -0.8f),
            LocalScale = new Vector3(1.0f, 1.4f, 1.0f)
        });
        pillar.AddComponent(new LightEngine.Core.Rendering3D.MeshRenderer3D(
            render3D.GetPrimitiveMesh(LightEngine.Core.Rendering3D.PrimitiveType3D.Cylinder),
            new LightEngine.Core.Rendering3D.Material3D { DiffuseColor = new Color(85, 205, 135), SpecularPower = 32f }));

        // --- 2D SAHNE VARLIKLARI (2D Moduna geçildiğinde görünür) ---
        var player = scene.CreateEntity("Oyuncu_Gemisi_2D");
        player.Tag = "Player";
        player.Transform.LocalPosition = new Vector2(0, 0);
        player.Transform.LocalScale = new Vector2(1.2f, 1.2f);
        var playerTex = assetService.CreateSolidTexture(40, 40, new Color(40, 200, 255), "editor_player");
        player.AddComponent(new SpriteRenderer(playerTex, centerOrigin: true));
        player.AddComponent(new BoxCollider2D(40, 40) { Layer = CollisionLayer.Player });

        var enemy = scene.CreateEntity("Dusman_UFO_2D");
        enemy.Tag = "Enemy";
        enemy.Transform.LocalPosition = new Vector2(180, -90);
        var enemyTex = assetService.CreateCircleTexture(28, new Color(240, 80, 80), "editor_enemy");
        enemy.AddComponent(new SpriteRenderer(enemyTex, centerOrigin: true));
        enemy.AddComponent(new CircleCollider2D(28) { Layer = CollisionLayer.Enemy });

        // Kamera Başlangıçları
        scene.Camera.Position = Vector2.Zero;
        scene.Camera.Zoom = 1.0f;
        scene.Camera3D.Position = new Vector3(4.5f, 3.8f, 7.2f);
        scene.Camera3D.LookAt(new Vector3(0f, 0.8f, 0f));

        scene.Initialize();
        scene.LoadContent();

        sceneService.LoadScene(scene);
        _context.ActiveScene = scene;
        _context.SelectedEntity = heroCube;

        _context.Log("Hybrid 2D/3D scene initialized with C# AutoRotator script.");
    }

    protected override void Update(GameTime gameTime)
    {
        // Editör girdilerini ve ImGui durumunu her zaman güncelle
        _imGuiRenderer.Update(gameTime);
        Engine.Services.Get<LightEngine.Core.Input.IInputService>().Update();

        // Dosya izleyicisinden gelen C# Hot Reload isteklerini ana thread'de güvenle uygula
        if (Engine.Services.TryGet<LightEngine.Core.Scripting.IScriptService>(out var svc) && svc is LightEngine.Core.Scripting.ScriptManager sm)
        {
            sm.ProcessPendingHotReload(_context.ActiveScene);
        }

        // Yalnızca Play modunda sahne mantığı güncellenir
        if (_context.IsPlaying && !_context.IsPaused)
        {
            base.Update(gameTime);
        }

        // Global Editör Kısayol Tuşları (Metin kutusuna yazılmıyorken)
        var io = ImGui.GetIO();
        if (!io.WantTextInput)
        {
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
            {
                SaveActiveScene();
            }
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Z))
            {
                _context.Undo();
            }
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.Y))
            {
                _context.Redo();
            }
            if (io.KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.D) && _context.SelectedEntity != null)
            {
                _context.DuplicateEntity(_context.SelectedEntity);
            }
        }

        _editorFrameCount++;

        // Editör penceresinin ekran görüntüsünü kaydet
        if (!_screenshotCaptured && _editorFrameCount >= 30)
        {
            _screenshotCaptured = true;
            RequestScreenshot("editor_screenshot.png");

            string artifactDir = @"C:\Users\ahmet\.gemini\antigravity\brain\5b00d67c-e274-4d62-ad5f-4c66396fb6ab";
            if (Directory.Exists(artifactDir))
            {
                RequestScreenshot(Path.Combine(artifactDir, "editor_preview.png"));
            }
        }

        if (_editorFrameCount >= 75 && Environment.GetEnvironmentVariable("LIGHTENGINE_AUTO_EXIT") == "1")
        {
            Exit();
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        // 0. GERÇEK KARE SÜRESİ VE FPS ÖLÇÜMÜ (Stopwatch ile, hem Edit hem Play modunda sürekli çalışır)
        float elapsedSeconds = (float)_frameStopwatch.Elapsed.TotalSeconds;
        _frameStopwatch.Restart();

        if (elapsedSeconds > 0.00005f)
        {
            float currentFps = 1.0f / elapsedSeconds;
            float currentMs = elapsedSeconds * 1000f;

            // Yumuşatılmış ortalama (Exponential Moving Average)
            _smoothedFps = MathHelper.Lerp(_smoothedFps, currentFps, 0.08f);
            _smoothedFrameTimeMs = MathHelper.Lerp(_smoothedFrameTimeMs, currentMs, 0.08f);

            _context.FPS = _smoothedFps;
            _context.FrameTimeMs = _smoothedFrameTimeMs;
        }

        // 1. Sahneyi Viewport ve Game Viewport RenderTarget2D dokularına çiz
        if (_context.ActiveScene != null)
        {
            var renderService = Engine.Services.Get<IRenderService>();
            if (_context.ShowSceneViewport)
            {
                _viewportPanel.RenderSceneToTarget(_context.ActiveScene, renderService.SpriteRenderer);
            }
            if (_context.ShowGameViewport)
            {
                _gameViewportPanel.RenderGameToTarget(_context.ActiveScene, renderService.SpriteRenderer);
            }
        }

        // 2. Ana ekranı editör arka plan rengiyle temizle
        GraphicsDevice.Clear(ClearColor);

        // 3. ImGui Arayüzünü Çiz
        _imGuiRenderer.BeforeLayout();

        DrawEditorLayout();

        _imGuiRenderer.AfterLayout();

        // 4. Ekran görüntüsü kuyruğunu işle
        ProcessPendingScreenshots();
    }

    private void DrawEditorLayout()
    {
        // Ana Menü Çubuğu (Main Menu Bar)
        DrawMainMenuBar();

        // Üst Araç Çubuğu (Play/Pause/Stop & Gizmo & Stopwatch FPS Toolbar)
        _toolbarPanel.Draw();

        // ImGui DockSpace: Tüm paneller tek bir çalışma alanı içinde yerleştirilir ve sürüklenebilir
        var viewport = ImGui.GetMainViewport();
        uint dockspaceId = ImGui.GetID("EditorDockSpace");

        ImGui.DockSpaceOverViewport(dockspaceId, viewport, ImGuiDockNodeFlags.None);

        // İlk açılışta veya kullanıcı Reset Layout istediğinde yerleşimi sıfırdan ve hatasız inşa et
        if (_isFirstLayout || _resetLayoutRequested)
        {
            _isFirstLayout = false;
            _resetLayoutRequested = false;
            BuildDefaultDockSpace(dockspaceId, viewport);
        }

        // Play moduna basıldığında Game, Stop basıldığında Scene sekmesine odaklan
        if (_context.FocusGameTabRequested)
        {
            ImGui.SetWindowFocus("Game Viewport##GameViewportPanel");
            _context.FocusGameTabRequested = false;
        }
        else if (_context.FocusSceneTabRequested)
        {
            ImGui.SetWindowFocus("Scene Viewport##ViewportPanel");
            _context.FocusSceneTabRequested = false;
        }

        // Panelleri çiz (Window menüsündeki görünürlük durumlarına bağlı)
        if (_context.ShowHierarchy)
        {
            _hierarchyPanel.Draw();
        }

        if (_context.ShowSceneViewport)
        {
            _viewportPanel.Draw();
        }

        if (_context.ShowGameViewport)
        {
            _gameViewportPanel.Draw();
        }

        if (_context.ShowInspector)
        {
            _inspectorPanel.Draw();
        }

        if (_context.ShowAssetBrowser)
        {
            _assetBrowserPanel.Draw();
        }

        if (_context.ShowConsole)
        {
            _consolePanel.Draw();
        }

        // Tercihler (Preferences) Penceresi
        if (_context.ShowPreferences)
        {
            DrawPreferencesDialog();
        }

        // Oyun Dışa Aktarma (.exe / binary Export) Penceresi
        if (_showExportDialog)
        {
            DrawExportDialog();
        }

        // Alt Durum Çubuğu (Bottom Status Bar)
        DrawBottomStatusBar();
    }

    private void DrawMainMenuBar()
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (ImGui.MenuItem("New Scene", "Ctrl+N"))
                {
                    var scene = new Scene("New_Scene");
                    _context.ActiveScene = scene;
                    _context.SelectedEntity = null;
                    _context.Log("New empty scene created.");
                }

                if (ImGui.MenuItem("Save Scene", "Ctrl+S"))
                {
                    SaveActiveScene();
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Export Game (.exe)...", "Ctrl+Shift+B"))
                {
                    _showExportDialog = true;
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Exit", "Alt+F4"))
                {
                    Exit();
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Edit"))
            {
                if (ImGui.MenuItem("Undo", "Ctrl+Z", false, _context.CanUndo))
                {
                    _context.Undo();
                }
                if (ImGui.MenuItem("Redo", "Ctrl+Y", false, _context.CanRedo))
                {
                    _context.Redo();
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Preferences...", ""))
                {
                    _context.ShowPreferences = true;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("GameObject"))
            {
                if (ImGui.MenuItem("Create Empty"))
                {
                    _context.ActiveScene?.CreateEntity("New_Entity");
                }
                if (ImGui.MenuItem("Create 2D Sprite"))
                {
                    var e = _context.ActiveScene?.CreateEntity("New_Sprite");
                    var asset = Engine.Services.Get<IAssetService>();
                    e?.AddComponent(new SpriteRenderer(asset.GetWhitePixel()));
                }

                ImGui.Separator();

                if (ImGui.BeginMenu("3D Object"))
                {
                    if (ImGui.MenuItem("Cube")) _context.Create3DPrimitive(LightEngine.Core.Rendering3D.PrimitiveType3D.Cube);
                    if (ImGui.MenuItem("Sphere")) _context.Create3DPrimitive(LightEngine.Core.Rendering3D.PrimitiveType3D.Sphere);
                    if (ImGui.MenuItem("Plane")) _context.Create3DPrimitive(LightEngine.Core.Rendering3D.PrimitiveType3D.Plane);
                    if (ImGui.MenuItem("Cylinder")) _context.Create3DPrimitive(LightEngine.Core.Rendering3D.PrimitiveType3D.Cylinder);
                    if (ImGui.MenuItem("Cone")) _context.Create3DPrimitive(LightEngine.Core.Rendering3D.PrimitiveType3D.Cone);
                    if (ImGui.MenuItem("Pyramid")) _context.Create3DPrimitive(LightEngine.Core.Rendering3D.PrimitiveType3D.Pyramid);
                    ImGui.Separator();
                    if (ImGui.MenuItem("Physics Dynamic Cube")) _context.Create3DPhysicsObject(LightEngine.Core.Rendering3D.PrimitiveType3D.Cube);
                    if (ImGui.MenuItem("Physics Dynamic Sphere")) _context.Create3DPhysicsObject(LightEngine.Core.Rendering3D.PrimitiveType3D.Sphere);
                    if (ImGui.MenuItem("3D Character Controller")) _context.Create3DCharacterController();
                    if (ImGui.MenuItem("3D Audio Emitter")) _context.Create3DAudioEmitter();
                    if (ImGui.MenuItem("Terrain (Heightmap)")) _context.Create3DTerrain();
                    if (ImGui.MenuItem("3D Particle Emitter")) _context.Create3DParticleEmitter();
                    ImGui.Separator();
                    if (ImGui.MenuItem("Directional Light")) _context.Create3DLight(LightEngine.Core.Rendering3D.LightType3D.Directional);
                    if (ImGui.MenuItem("Point Light")) _context.Create3DLight(LightEngine.Core.Rendering3D.LightType3D.Point);
                    ImGui.EndMenu();
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Scripts"))
            {
                if (ImGui.MenuItem("Create New C# Script"))
                {
                    if (Engine.Services.TryGet<LightEngine.Core.Scripting.IScriptService>(out var s) && s != null)
                    {
                        string path = s.CreateScriptTemplate($"CustomScript_{DateTime.Now:HHmmss}");
                        s.ReloadScripts(_context.ActiveScene);
                        _context.Log($"Created & compiled script: {Path.GetFileName(path)}", LogLevel.Info);
                    }
                }
                if (ImGui.MenuItem("Hot Reload All Scripts", "F5"))
                {
                    if (Engine.Services.TryGet<LightEngine.Core.Scripting.IScriptService>(out var s) && s != null)
                    {
                        s.ReloadScripts(_context.ActiveScene);
                    }
                }
                if (ImGui.MenuItem("Open Scripts Folder"))
                {
                    if (Engine.Services.TryGet<LightEngine.Core.Scripting.IScriptService>(out var s) && s != null)
                    {
                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{s.ScriptsDirectory}\"") { UseShellExecute = true }); } catch { }
                    }
                }
                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Rendering"))
            {
                if (ImGui.MenuItem("Bake Static Lightmaps (HL2 Radiosity)"))
                {
                    if (_context.ActiveScene != null && Engine.Services.TryGet<LightEngine.Core.Rendering3D.IRender3DService>(out var r3d) && r3d != null)
                    {
                        r3d.BakeStaticLightmaps(_context.ActiveScene, 128);
                        _context.Log("Static Lightmaps baked (128x128 soft shadows).", LogLevel.Info);
                    }
                }

                ImGui.Separator();

                if (Engine.Services.TryGet<LightEngine.Core.Rendering3D.IRender3DService>(out var render3D) && render3D != null)
                {
                    var pp = render3D.PostProcessor.Settings;
                    bool bloom = pp.BloomEnabled;
                    if (ImGui.MenuItem("Bloom", "", ref bloom)) pp.BloomEnabled = bloom;

                    bool fxaa = pp.FxaaEnabled;
                    if (ImGui.MenuItem("FXAA Anti-Aliasing", "", ref fxaa)) pp.FxaaEnabled = fxaa;

                    bool tm = pp.ToneMappingEnabled;
                    if (ImGui.MenuItem("Tone Mapping", "", ref tm)) pp.ToneMappingEnabled = tm;

                    bool ssao = pp.SsaoEnabled;
                    if (ImGui.MenuItem("SSAO Ambient Occlusion", "", ref ssao)) pp.SsaoEnabled = ssao;

                    bool csm = render3D.ShadowMap.IsEnabled;
                    if (ImGui.MenuItem("Cascaded Shadow Maps", "", ref csm)) render3D.ShadowMap.IsEnabled = csm;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Window"))
            {
                bool showH = _context.ShowHierarchy;
                if (ImGui.MenuItem("Hierarchy", "", ref showH)) _context.ShowHierarchy = showH;

                bool showI = _context.ShowInspector;
                if (ImGui.MenuItem("Inspector", "", ref showI)) _context.ShowInspector = showI;

                bool showV = _context.ShowSceneViewport;
                if (ImGui.MenuItem("Scene Viewport", "", ref showV)) _context.ShowSceneViewport = showV;

                bool showG = _context.ShowGameViewport;
                if (ImGui.MenuItem("Game Viewport", "", ref showG)) _context.ShowGameViewport = showG;

                bool showA = _context.ShowAssetBrowser;
                if (ImGui.MenuItem("Asset Browser", "", ref showA)) _context.ShowAssetBrowser = showA;

                bool showC = _context.ShowConsole;
                if (ImGui.MenuItem("Console", "", ref showC)) _context.ShowConsole = showC;

                ImGui.Separator();

                if (ImGui.MenuItem("Reset Layout"))
                {
                    ResetDefaultLayout();
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Help"))
            {
                if (ImGui.MenuItem("About Light Engine"))
                {
                    _context.Log("Light Engine v0.1 — Open-source 2D/3D Hybrid Game Engine.", LogLevel.Info);
                }
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }

    private void DrawPreferencesDialog()
    {
        bool open = true;
        ImGui.SetNextWindowSize(new NumVec2(400, 240), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Preferences##SettingsModal", ref open))
        {
            ImGui.TextColored(new NumVec4(0.3f, 0.8f, 1f, 1f), "Render & Engine Preferences");
            ImGui.Separator();
            ImGui.Spacing();

            // VSync Ayarı
            bool vsync = _context.VSync;
            if (ImGui.Checkbox("Vertical Sync (VSync)", ref vsync))
            {
                _context.VSync = vsync;
                _context.NotifyPreferencesChanged();
                _context.Log($"VSync set to: {vsync}");
            }

            // Fixed Timestep Ayarı
            bool fixedStep = _context.IsFixedTimeStep;
            if (ImGui.Checkbox("Fixed Timestep (60 Hz Lock)", ref fixedStep))
            {
                _context.IsFixedTimeStep = fixedStep;
                _context.NotifyPreferencesChanged();
                _context.Log($"IsFixedTimeStep set to: {fixedStep}");
            }

            ImGui.Spacing();
            ImGui.Separator();

            // Gerçek zamanlı teşhis
            ImGui.TextDisabled($"Measured FPS: {_context.FPS:0.0} ({_context.FrameTimeMs:0.00} ms)");
            ImGui.TextDisabled($"Multi-Sampling (MSAA): Enabled");

            ImGui.Spacing();
            if (ImGui.Button("Close", new NumVec2(100, 26)))
            {
                open = false;
            }
        }
        ImGui.End();

        if (!open)
        {
            _context.ShowPreferences = false;
        }
    }

    private void DrawExportDialog()
    {
        bool open = true;
        ImGui.SetNextWindowSize(new NumVec2(560, 310), ImGuiCond.FirstUseEver);

        if (ImGui.Begin("Export Game (Standalone Executable)##ExportModal", ref open))
        {
            ImGui.TextColored(new NumVec4(0.35f, 0.85f, 1f, 1f), "Package Game as Standalone Executable (Windows / Linux / macOS)");
            ImGui.Separator();
            ImGui.Spacing();

            // Hedef İşletim Sistemi / Platform Seçimi
            ImGui.Combo("Target OS / Architecture", ref _exportTargetIndex, _exportTargetNames, _exportTargetNames.Length);
            string selectedRid = _exportTargetRids[_exportTargetIndex];
            ImGui.TextDisabled($"Runtime Identifier (RID): {selectedRid} - Fully self-contained .NET 9 binary");
            ImGui.Spacing();

            ImGui.InputText("Output Folder", ref _exportOutputDir, 260);
            ImGui.Checkbox("Single File Bundle", ref _exportSingleFile);
            ImGui.SameLine();
            ImGui.Checkbox("Self-Contained (Include Runtime)", ref _exportSelfContained);

            ImGui.Spacing();
            ImGui.TextWrapped(_exportStatus);
            ImGui.Spacing();
            ImGui.Separator();

            if (!_isExporting)
            {
                if (ImGui.Button($"Build & Export ({selectedRid})", new NumVec2(220, 28)))
                {
                    _isExporting = true;
                    _exportStatus = $"Exporting standalone executable for {selectedRid}... Please wait.";
                    _context.Log($"Starting game export for {selectedRid}...", LogLevel.Info);

                    var options = new Export.ExportOptions
                    {
                        GameProjectPath = Export.GameExporter.FindGameProjectPath(),
                        OutputDirectory = _exportOutputDir,
                        RuntimeIdentifier = selectedRid,
                        PublishSingleFile = _exportSingleFile,
                        SelfContained = _exportSelfContained
                    };

                    _ = Export.GameExporter.ExportAsync(options, line => _context.Log(line)).ContinueWith(t =>
                    {
                        _isExporting = false;
                        _exportStatus = t.Result.Summary;
                        _context.Log(t.Result.Summary, t.Result.Success ? LogLevel.Info : LogLevel.Error);
                    });
                }
            }
            else
            {
                ImGui.TextColored(new NumVec4(1f, 0.8f, 0.2f, 1f), $"Building Release executable for {selectedRid}...");
            }

            ImGui.SameLine();
            if (ImGui.Button("Close", new NumVec2(90, 28)))
            {
                open = false;
            }
        }
        ImGui.End();

        if (!open) _showExportDialog = false;
    }

    private void DrawBottomStatusBar()
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        const float statusBarHeight = 24f;
        ImGui.SetNextWindowPos(new NumVec2(viewport.Pos.X, viewport.Pos.Y + viewport.Size.Y - statusBarHeight));
        ImGui.SetNextWindowSize(new NumVec2(viewport.Size.X, statusBarHeight));

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration |
                                 ImGuiWindowFlags.NoScrollbar |
                                 ImGuiWindowFlags.NoScrollWithMouse |
                                 ImGuiWindowFlags.NoDocking |
                                 ImGuiWindowFlags.NoNav |
                                 ImGuiWindowFlags.NoFocusOnAppearing;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NumVec2(10, 3));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new NumVec4(0.08f, 0.09f, 0.11f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Border, new NumVec4(0.22f, 0.25f, 0.32f, 1.0f));

        if (ImGui.Begin("##EditorBottomStatusBar", flags))
        {
            // 1. Mod Göstergesi [3D PERSPECTIVE] veya [2D ORTHO]
            if (_context.Is3DMode)
            {
                ImGui.TextColored(new NumVec4(0.2f, 0.8f, 1f, 1f), "[3D PERSPECTIVE]");
            }
            else
            {
                ImGui.TextColored(new NumVec4(0.4f, 0.9f, 0.4f, 1f), "[2D ORTHO]");
            }

            ImGui.SameLine(0, 15);
            ImGui.TextDisabled("|");
            ImGui.SameLine(0, 15);

            // 2. Kamera Koordinatları
            if (_context.ActiveScene != null)
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                if (_context.Is3DMode && _context.ActiveScene.Camera3D != null)
                {
                    var c = _context.ActiveScene.Camera3D.Position;
                    ImGui.TextUnformatted($"Cam: ({c.X.ToString("F1", inv)}, {c.Y.ToString("F1", inv)}, {c.Z.ToString("F1", inv)})");
                }
                else if (!_context.Is3DMode && _context.ActiveScene.Camera != null)
                {
                    var c = _context.ActiveScene.Camera.Position;
                    ImGui.TextUnformatted($"Cam: ({c.X.ToString("F0", inv)}, {c.Y.ToString("F0", inv)}) Zoom: {_context.ActiveScene.Camera.Zoom.ToString("F2", inv)}x");
                }
            }

            ImGui.SameLine(0, 15);
            ImGui.TextDisabled("|");
            ImGui.SameLine(0, 15);

            // 3. Sahne ve Varlık Sayısı
            int entityCount = _context.ActiveScene?.Entities.Count ?? 0;
            ImGui.TextUnformatted($"Scene: {_context.ActiveScene?.Name ?? "None"} ({entityCount} entities)");

            ImGui.SameLine(0, 15);
            ImGui.TextDisabled("|");
            ImGui.SameLine(0, 15);

            // 4. Seçili Varlık
            if (_context.SelectedEntity != null)
            {
                ImGui.TextColored(new NumVec4(1f, 0.85f, 0.3f, 1f), $"Selected: {_context.SelectedEntity.Name}");
                ImGui.SameLine(0, 4);
                ImGui.TextDisabled($"[{_context.SelectedEntity.Tag}]");
            }
            else
            {
                ImGui.TextDisabled("No selection");
            }

            ImGui.SameLine(0, 15);
            ImGui.TextDisabled("|");
            ImGui.SameLine(0, 15);

            // 5. Fizik Durumu
            ImGui.TextUnformatted(_context.Is3DMode ? "Physics: Bepu v2" : "Physics: 2D SAT/AABB");

            // 6. Sağ taraf: FPS, Frame Time ve Console Hata Sayısı
            float fpsWidth = 260f;
            float avail = ImGui.GetContentRegionAvail().X;
            if (avail > fpsWidth)
            {
                ImGui.SameLine(ImGui.GetWindowWidth() - fpsWidth);
            }
            else
            {
                ImGui.SameLine(0, 15);
            }

            int errorCount = 0;
            int warningCount = 0;
            for (int i = 0; i < _context.Logs.Count; i++)
            {
                if (_context.Logs[i].Level == LogLevel.Error) errorCount++;
                else if (_context.Logs[i].Level == LogLevel.Warning) warningCount++;
            }

            if (errorCount > 0)
            {
                ImGui.TextColored(new NumVec4(1f, 0.35f, 0.35f, 1f), $"● {errorCount} Err");
                ImGui.SameLine(0, 8);
            }
            if (warningCount > 0)
            {
                ImGui.TextColored(new NumVec4(1f, 0.8f, 0.2f, 1f), $"▲ {warningCount} Warn");
                ImGui.SameLine(0, 8);
            }

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            ImGui.TextColored(new NumVec4(0.3f, 0.9f, 0.4f, 1f), $"{_context.FPS.ToString("F1", ci)} FPS");
            ImGui.SameLine(0, 4);
            ImGui.TextDisabled($"({_context.FrameTimeMs.ToString("F2", ci)} ms)");
        }
        ImGui.End();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
    }

    private void SaveActiveScene()
    {
        if (_context.ActiveScene == null) return;
        try
        {
            string assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Scenes");
            Directory.CreateDirectory(assetsDir);
            string filePath = Path.Combine(assetsDir, $"{_context.ActiveScene.Name}.scene");
            LightEngine.Core.Serialization.SceneSerializer.SaveToFile(_context.ActiveScene, filePath);
            _context.Log($"Scene saved successfully to: {filePath}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _context.Log($"Failed to save scene: {ex.Message}", LogLevel.Error);
        }
    }

    private void BuildDefaultDockSpace(uint dockspaceId, ImGuiViewportPtr viewport)
    {
        try
        {
            ImGuiDockBuilder.igDockBuilderRemoveNode(dockspaceId);
            ImGuiDockBuilder.igDockBuilderAddNode(dockspaceId, ImGuiDockNodeFlags.None);
            ImGuiDockBuilder.igDockBuilderSetNodeSize(dockspaceId, viewport.WorkSize);
            ImGuiDockBuilder.igDockBuilderSetNodePos(dockspaceId, viewport.WorkPos);

            uint dock_main_id = dockspaceId;
            uint dock_id_left = ImGuiDockBuilder.igDockBuilderSplitNode(dock_main_id, ImGuiDir.Left, 0.18f, out _, out dock_main_id);
            uint dock_id_right = ImGuiDockBuilder.igDockBuilderSplitNode(dock_main_id, ImGuiDir.Right, 0.22f, out _, out dock_main_id);
            uint dock_id_bottom = ImGuiDockBuilder.igDockBuilderSplitNode(dock_main_id, ImGuiDir.Down, 0.28f, out _, out dock_main_id);

            // Solda Hierarchy (%18)
            ImGuiDockBuilder.igDockBuilderDockWindow("Scene Hierarchy##HierarchyPanel", dock_id_left);
            // Sağda Inspector (%22)
            ImGuiDockBuilder.igDockBuilderDockWindow("Inspector##InspectorPanel", dock_id_right);
            // Altta Asset Browser ve Console sekmeli olarak (%28)
            ImGuiDockBuilder.igDockBuilderDockWindow("Asset Browser##AssetBrowserPanel", dock_id_bottom);
            ImGuiDockBuilder.igDockBuilderDockWindow("Console##ConsolePanel", dock_id_bottom);
            // Ortada en büyük ana alanda hem Scene Viewport hem de Game Viewport sekmeli (Central Node)
            ImGuiDockBuilder.igDockBuilderDockWindow("Scene Viewport##ViewportPanel", dock_main_id);
            ImGuiDockBuilder.igDockBuilderDockWindow("Game Viewport##GameViewportPanel", dock_main_id);

            ImGuiDockBuilder.igDockBuilderFinish(dockspaceId);
            _context.Log("DockSpace layout successfully built with central Scene & Game Viewports.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _context.Log($"DockBuilder error: {ex.Message}", LogLevel.Warning);
        }
    }

    public void ResetDefaultLayout()
    {
        _context.ShowHierarchy = true;
        _context.ShowInspector = true;
        _context.ShowSceneViewport = true;
        _context.ShowGameViewport = true;
        _context.ShowAssetBrowser = true;
        _context.ShowConsole = true;
        _resetLayoutRequested = true;

        try
        {
            if (File.Exists("imgui.ini"))
            {
                File.Delete("imgui.ini");
            }
        }
        catch { }

        _context.Log("Window layout reset to default docking configuration.", LogLevel.Info);
    }

    protected override void UnloadContent()
    {
        _imGuiRenderer.Dispose();
        _viewportPanel.Dispose();
        _gameViewportPanel.Dispose();
        base.UnloadContent();
    }
}
