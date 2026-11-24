using LightEngine.Core.Assets;
using LightEngine.Core.Audio;
using LightEngine.Core.Common;
using LightEngine.Core.Diagnostics;
using LightEngine.Core.Ecs;
using LightEngine.Core.Input;
using LightEngine.Core.Physics2D;
using LightEngine.Core.Physics3D;
using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;
using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LightEngine.Core;

/// <summary>
/// LightEngine motorunun ana oyun sunucusu (Game Host).
/// Sabit zaman adımlı (Fixed Timestep) oyun döngüsünü, alt servisleri ve 2D+3D sahneleri yönetir.
/// </summary>
public class LightEngineGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly TimeManager _timeManager;
    private readonly InputManager _inputManager;
    private readonly SceneManager _sceneManager;
    private readonly PerformanceOverlay _performanceOverlay;
    private readonly AudioManager _audioManager;
    private readonly Physics2DManager _physicsManager;
    private readonly Physics3DManager _physics3DManager;
    private readonly ScriptManager _scriptManager;

    private AssetManager? _assetManager;
    private RenderManager? _renderManager;
    private Renderer3D? _renderer3D;
    private float _fixedAccumulator;

    /// <summary>
    /// Ekran temizleme rengi.
    /// </summary>
    public Color ClearColor { get; set; } = new(32, 34, 40);

    /// <summary>
    /// Grafik cihaz yöneticisi (VSync, çözünürlük ve multisampling ayarları için).
    /// </summary>
    public GraphicsDeviceManager GraphicsManager => _graphics;

    /// <summary>
    /// Performans (FPS / Frame time) katmanı.
    /// </summary>
    public PerformanceOverlay PerformanceOverlay => _performanceOverlay;

    public LightEngineGame(string windowTitle = "Light Engine (2D+3D)", int width = 1280, int height = 720)
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = width,
            PreferredBackBufferHeight = height,
            PreferredDepthStencilFormat = DepthFormat.Depth24,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = windowTitle;

        // Sabit zaman adımı konfigürasyonu (60 Hz / 16.66 ms)
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);

        // Temel servislerin ilklendirilmesi
        Engine.Services = new ServiceLocator();

        _timeManager = new TimeManager();
        _inputManager = new InputManager();
        _sceneManager = new SceneManager();
        _performanceOverlay = new PerformanceOverlay();
        _audioManager = new AudioManager();
        _physicsManager = new Physics2DManager();
        _physics3DManager = new Physics3DManager();
        _scriptManager = new ScriptManager();

        Engine.Services.Register<ITimeService>(_timeManager);
        Engine.Services.Register<IInputService>(_inputManager);
        Engine.Services.Register<ISceneService>(_sceneManager);
        Engine.Services.Register<IAudioService>(_audioManager);
        Engine.Services.Register<IPhysics2DService>(_physicsManager);
        Engine.Services.Register<IPhysics3DService>(_physics3DManager);
        Engine.Services.Register<IScriptService>(_scriptManager);
    }

    protected override void Initialize()
    {
        base.Initialize();
        OnEngineInitialized();
    }

    protected override void LoadContent()
    {
        _assetManager = new AssetManager(GraphicsDevice, Content);
        _renderManager = new RenderManager(GraphicsDevice, _assetManager);
        _renderer3D = new Renderer3D(GraphicsDevice);

        Engine.Services.Register<IAssetService>(_assetManager);
        Engine.Services.Register<IRenderService>(_renderManager);
        Engine.Services.Register<IRender3DService>(_renderer3D);

        OnEngineContentLoad();
    }

    protected override void Update(GameTime gameTime)
    {
        // 1. Zamanı güncelle
        _timeManager.Update(gameTime);
        float dt = _timeManager.DeltaTime;

        // 2. Giriş durumlarını güncelle
        _inputManager.Update();

        // 3. Sabit zaman adımı ile mantık/fizik güncellemeleri (FixedUpdate)
        _fixedAccumulator += dt;
        float fixedDt = _timeManager.FixedDeltaTime;
        while (_fixedAccumulator >= fixedDt)
        {
            _physicsManager.FixedUpdate(fixedDt);
            _physics3DManager.FixedUpdate(fixedDt);
            _sceneManager.FixedUpdate(fixedDt);
            OnEngineFixedUpdate(fixedDt);
            _fixedAccumulator -= fixedDt;
        }

        // 4. Değişken zaman adımı ile sahne güncellemesi (Update)
        _sceneManager.Update(dt);
        _performanceOverlay.Update(dt);

        OnEngineUpdate(dt);

        if (_inputManager.IsKeyPressed(Microsoft.Xna.Framework.Input.Keys.F12))
        {
            RequestScreenshot($"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (_renderManager == null) return;

        // Ekranı temizle
        _renderManager.Clear(ClearColor);

        // 1. DÜNYA PASSI: Aktif sahneyi 3D + 2D hibrit olarak çiz
        var activeScene = _sceneManager.ActiveScene;
        if (activeScene != null)
        {
            // 1a. 3D Pass (Eğer sahnede 3D mesh varsa)
            if (_renderer3D != null)
            {
                activeScene.Camera3D.AspectRatio = GraphicsDevice.Viewport.AspectRatio;
                _renderer3D.RenderScene(activeScene, activeScene.Camera3D, drawEditorGrid: false);
            }

            // 1b. 2D Pass: Kamera merkezini viewport merkezine hizala (Origin ayarlanmamışsa)
            if (activeScene.Camera.Origin == Vector2.Zero)
            {
                activeScene.Camera.Origin = new Vector2(
                    GraphicsDevice.Viewport.Width / 2f,
                    GraphicsDevice.Viewport.Height / 2f);
            }

            _renderManager.BeginWorld(activeScene.Camera);
            _sceneManager.Draw(_renderManager.SpriteRenderer);
            OnEngineWorldDraw(_renderManager.SpriteRenderer);
            _renderManager.End();
        }

        // 2. EKRAN PASSI: HUD, UI ve FPS Performance Overlay'i çiz (Kamera dönüşümsüz)
        _renderManager.BeginScreen();
        _performanceOverlay.Draw(_renderManager.SpriteRenderer);
        OnEngineScreenDraw(_renderManager.SpriteRenderer);
        _renderManager.End();

        base.Draw(gameTime);

        ProcessPendingScreenshots();
    }

    protected void ProcessPendingScreenshots()
    {
        if (_pendingScreenshotPaths.Count > 0)
        {
            var paths = _pendingScreenshotPaths.ToArray();
            _pendingScreenshotPaths.Clear();
            foreach (var path in paths)
            {
                SaveScreenshot(path);
            }
        }
    }

    private readonly List<string> _pendingScreenshotPaths = new();

    /// <summary>
    /// Ekranın o anki görüntüsünü PNG dosyası olarak kaydetmek üzere sıraya alır.
    /// </summary>
    public void RequestScreenshot(string filePath)
    {
        _pendingScreenshotPaths.Add(filePath);
    }

    private void SaveScreenshot(string filePath)
    {
        try
        {
            int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int h = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new Color[w * h];
            GraphicsDevice.GetBackBufferData(data);
            using var tex = new Texture2D(GraphicsDevice, w, h);
            tex.SetData(data);

            string? dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var stream = File.Create(filePath);
            tex.SaveAsPng(stream, w, h);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LightEngine] Ekran görüntüsü kaydetme hatası: {ex.Message}");
        }
    }

    protected override void UnloadContent()
    {
        _sceneManager.ActiveScene?.UnloadContent();
        _scriptManager.Dispose();
        _physics3DManager.Dispose();
        _renderer3D?.Dispose();
        _assetManager?.Dispose();
        _renderManager?.Dispose();

        base.UnloadContent();
    }

    #region Türetilebilir Kanca (Hook) Metotlar

    protected virtual void OnEngineInitialized() { }
    protected virtual void OnEngineContentLoad() { }
    protected virtual void OnEngineUpdate(float deltaTime) { }
    protected virtual void OnEngineFixedUpdate(float fixedDeltaTime) { }
    protected virtual void OnEngineWorldDraw(SpriteBatchRenderer renderer) { }
    protected virtual void OnEngineScreenDraw(SpriteBatchRenderer renderer) { }

    #endregion
}
