using Game.Components;
using LightEngine.Core.Animation;
using LightEngine.Core.Assets;
using LightEngine.Core.Audio;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Input;
using LightEngine.Core.Particles;
using LightEngine.Core.Physics2D;
using LightEngine.Core.Rendering;
using LightEngine.Core.Serialization;
using LightEngine.Core.Tilemaps;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Scenes;

/// <summary>
/// Aşama 2: 2D Oyun Özellikleri (SpriteSheet Animasyon + Durum Makinesi,
/// Tiled .tmx Harita + Culling, 2D AABB/SAT Fizik ve Tetikleyiciler,
/// Sıfır GC Parçacık Sistemi, Prosedürel Ses Yöneticisi ve JSON Sahne Serileştirme)
/// yeteneklerini uçtan uca sergileyen kapsamlı test sahnesi.
/// </summary>
public class DemoScene : Scene
{
    private ObjectPool<Entity>? _laserPool;
    private Texture2D? _laserTexture;
    private Texture2D? _boxTexture;
    private Texture2D? _coinTexture;

    private ParticleEmitter? _worldBurstEmitter;
    private IEntity? _playerEntity;

    private int _coinsCollected;
    private string _statusBanner = "";
    private float _statusBannerTimer;

    public int CoinsCollected => _coinsCollected;
    public string StatusBanner => _statusBanner;

    public DemoScene() : base("DemoScene_Phase2") { }

    public override void Initialize()
    {
        base.Initialize();
        ConfigureInputMappings();
    }

    public override void LoadContent()
    {
        base.LoadContent();

        var assetService = Engine.Services.Get<IAssetService>();
        var renderService = Engine.Services.Get<IRenderService>();
        var graphicsDevice = renderService.SpriteRenderer.InnerBatch.GraphicsDevice;

        // 1. DOKULAR VE GÖRSEL VARLIKLAR
        _laserTexture = assetService.CreateSolidTexture(16, 4, Color.Yellow, "tex_laser");
        _boxTexture = assetService.CreateSolidTexture(48, 48, new Color(70, 90, 120), "tex_box");
        _coinTexture = assetService.CreateCircleTexture(14, Color.Gold, "tex_coin");

        // 2. DÜNYA PATLAMA PARÇACIK YAYICISI (Particle Emitter - Zero Allocation)
        SetupWorldParticleEmitter(assetService);

        // 3. TILEMAP (.TMX OKUMA VE RENDERER)
        SetupTilemap(assetService, graphicsDevice);

        // 4. LAZER NESNE HAVUZU (Object Pool)
        SetupLaserPool();

        // 5. OYUNCU (Player) - SPRITESHEET + ANİMASYON DURUM MAKİNESİ + FİZİK
        SetupPlayer(assetService, graphicsDevice);

        // 6. FİZİK ENGELLERİ (AABB Çarpışma ve İtme Testi)
        SpawnPhysicsObstacles();

        // 7. TOPLANABİLİR ALTIN PARALAR (2D Dairesel Tetikleyici / Trigger Testi)
        SpawnCollectibleCoins();

        // Kamera başlangıcı
        if (_playerEntity != null)
        {
            Camera.Position = _playerEntity.Transform.WorldPosition;
        }
        Camera.Zoom = 1.0f;
    }

    private void ConfigureInputMappings()
    {
        var input = Engine.Services.Get<IInputService>();

        // Eksenler
        input.MapAxis("Horizontal", Keys.D, Keys.A);
        input.MapAxis("Vertical", Keys.S, Keys.W);

        // Aksiyonlar
        input.MapAction("Fire", Keys.Enter);
        input.MapAction("Fire", MouseButton.Left);
        input.MapAction("Fire", Buttons.RightTrigger);

        input.MapAction("ShakeCamera", Keys.Space);
        input.MapAction("ShakeCamera", Buttons.X);
    }

    private void SetupWorldParticleEmitter(IAssetService assetService)
    {
        var burstEntity = CreateEntity("WorldParticleBurst");
        burstEntity.Tag = "Particles";
        burstEntity.Transform.WorldPosition = Vector2.Zero;

        _worldBurstEmitter = burstEntity.AddComponent(new ParticleEmitter(maxParticles: 800));
        _worldBurstEmitter.IsEmitting = false; // Yalnızca burst ile tetiklenir
        _worldBurstEmitter.MinLifetime = 0.3f;
        _worldBurstEmitter.MaxLifetime = 0.9f;
        _worldBurstEmitter.MinSpeed = 80f;
        _worldBurstEmitter.MaxSpeed = 260f;
        _worldBurstEmitter.StartSize = 10f;
        _worldBurstEmitter.EndSize = 1.5f;
        _worldBurstEmitter.StartColor = Color.Gold;
        _worldBurstEmitter.EndColor = new Color(255, 60, 0, 0);
    }

    private void SetupTilemap(IAssetService assetService, GraphicsDevice graphicsDevice)
    {
        try
        {
            // Tileset dokusunun varlığını garanti et
            EnsureTilesetTextureExists(assetService, graphicsDevice);

            string tmxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Tilemaps", "demo_level.tmx");
            if (File.Exists(tmxPath))
            {
                var map = TiledMap.LoadFromFile(tmxPath, assetService);
                var mapEntity = CreateEntity("TiledMap_Background");
                mapEntity.Transform.WorldPosition = new Vector2(-map.TotalWidthPixels / 2f, -map.TotalHeightPixels / 2f);
                mapEntity.AddComponent(new TilemapRenderer(map));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LightEngine] Tilemap yükleme uyarısı: {ex.Message}");
        }
    }

    private void SetupPlayer(IAssetService assetService, GraphicsDevice graphicsDevice)
    {
        _playerEntity = CreateEntity("Player");
        _playerEntity.Tag = "Player";
        _playerEntity.Transform.WorldPosition = Vector2.Zero;

        // A. Prosedürel Sprite Sheet üret (Idle ve Walk satırları)
        var spriteSheet = CreateAnimatedPlayerSpriteSheet(graphicsDevice);

        // B. SpriteRenderer bileşeni (Varsayılan kare ile)
        var playerRenderer = _playerEntity.AddComponent(new SpriteRenderer(spriteSheet.Texture, centerOrigin: true));
        playerRenderer.LayerDepth = 0.5f;

        // C. Animasyon Klipleri
        var idleClip = spriteSheet.CreateRowClip("Idle", rowIndex: 0, frameDuration: 0.16f, isLooping: true);
        var walkClip = spriteSheet.CreateRowClip("Walk", rowIndex: 1, frameDuration: 0.08f, isLooping: true);

        // D. Animasyon Durum Makinesi (Animation State Machine)
        var stateMachine = _playerEntity.AddComponent<AnimationStateMachine>();
        stateMachine.AddState("Idle", idleClip);
        stateMachine.AddState("Walk", walkClip);

        // Geçiş Kuralları
        stateMachine.AddTransition("Idle", "Walk", sm => sm.GetFloat("Speed") > 10f);
        stateMachine.AddTransition("Walk", "Idle", sm => sm.GetFloat("Speed") <= 10f);

        // E. Sürekli Motor Alevi Parçacık Yayıcısı (Thruster Trail)
        var thrusterEmitter = _playerEntity.AddComponent(new ParticleEmitter(maxParticles: 150));
        thrusterEmitter.IsEmitting = false;
        thrusterEmitter.EmissionRate = 60f;
        thrusterEmitter.MinLifetime = 0.15f;
        thrusterEmitter.MaxLifetime = 0.35f;
        thrusterEmitter.MinSpeed = 40f;
        thrusterEmitter.MaxSpeed = 90f;
        thrusterEmitter.SpreadAngle = 40f;
        thrusterEmitter.StartSize = 7f;
        thrusterEmitter.EndSize = 1.5f;
        thrusterEmitter.StartColor = Color.Orange;
        thrusterEmitter.EndColor = new Color(255, 40, 0, 0);

        // F. 2D AABB Çarpıştırıcı (Fizik katmanı: Player)
        var playerCollider = _playerEntity.AddComponent(new BoxCollider2D(28f, 28f));
        playerCollider.Layer = CollisionLayer.Player;
        playerCollider.IsStatic = false;

        // G. Oyuncu Kontrolcüsü
        var controller = _playerEntity.AddComponent<PlayerController>();
        if (_laserPool != null && _laserTexture != null)
        {
            controller.SetupLaserPool(_laserPool, _laserTexture);
        }
        if (_worldBurstEmitter != null)
        {
            controller.SetWorldBurstEmitter(_worldBurstEmitter);
        }
    }

    private void SetupLaserPool()
    {
        _laserPool = new ObjectPool<Entity>(
            initialCapacity: 32,
            factory: () =>
            {
                var laserEntity = new Entity("PooledLaser");
                laserEntity.Tag = "Projectile";
                laserEntity.IsActive = false;

                if (_laserTexture != null)
                {
                    var sr = laserEntity.AddComponent(new SpriteRenderer(_laserTexture, centerOrigin: true));
                    sr.Color = Color.Yellow;
                    sr.LayerDepth = 0.6f;
                }

                laserEntity.AddComponent<PooledLaser>();
                AddEntity(laserEntity);
                return laserEntity;
            },
            onGet: (e) => { e.IsActive = true; },
            onReturn: (e) => { e.IsActive = false; }
        );
    }

    private void SpawnPhysicsObstacles()
    {
        // Sabit, geçilemez AABB kutu engeller (Statik fizik duvarları)
        Vector2[] obstaclePositions =
        {
            new(-200, -120),
            new(-200, 120),
            new(200, -120),
            new(200, 120),
            new(0, -220),
            new(0, 220)
        };

        for (int i = 0; i < obstaclePositions.Length; i++)
        {
            var obstacle = CreateEntity($"SolidWall_{i}");
            obstacle.Tag = "Obstacle";
            obstacle.Transform.WorldPosition = obstaclePositions[i];

            if (_boxTexture != null)
            {
                var sr = obstacle.AddComponent(new SpriteRenderer(_boxTexture, centerOrigin: true));
                sr.Color = new Color(85, 110, 150);
                sr.LayerDepth = 0.4f;
            }

            // Statik AABB çarpıştırıcı (Oyuncu içinden geçemez, pushback ile ayrılır)
            var collider = obstacle.AddComponent(new BoxCollider2D(48f, 48f));
            collider.Layer = CollisionLayer.Obstacle;
            collider.IsStatic = true;
        }
    }

    private void SpawnCollectibleCoins()
    {
        var rand = new Random(42);

        // Toplanabilir altın paralar (Dairesel tetikleyiciler)
        for (int i = 0; i < 10; i++)
        {
            float x = (float)(rand.NextDouble() * 700.0 - 350.0);
            float y = (float)(rand.NextDouble() * 500.0 - 250.0);

            // Başlangıç noktasına çok yakın olmasın
            if (MathF.Abs(x) < 70f && MathF.Abs(y) < 70f) x += 120f;

            var coin = CreateEntity($"Coin_{i}");
            coin.Tag = "Collectible";
            coin.Transform.WorldPosition = new Vector2(x, y);

            if (_coinTexture != null)
            {
                var sr = coin.AddComponent(new SpriteRenderer(_coinTexture, centerOrigin: true));
                sr.Color = Color.Gold;
                sr.LayerDepth = 0.45f;
            }

            // Dairesel tetikleyici (IsTrigger = true)
            var circleCollider = coin.AddComponent(new CircleCollider2D(14f));
            circleCollider.Layer = CollisionLayer.Trigger;
            circleCollider.IsTrigger = true;

            // Para toplama olayını bağla
            circleCollider.OnTriggerEnter += (other) =>
            {
                if (other.Entity?.Tag == "Player" && !coin.IsDestroyed)
                {
                    _coinsCollected++;

                    // Ses efekti çal
                    if (Engine.Services.TryGet<IAudioService>(out var audio) && audio != null)
                    {
                        audio.PlayCoinSound();
                    }

                    // Parçacık patlaması üret
                    if (_worldBurstEmitter?.Entity != null)
                    {
                        _worldBurstEmitter.Entity.Transform.WorldPosition = coin.Transform.WorldPosition;
                        _worldBurstEmitter.EmitBurst(25);
                    }

                    _statusBanner = $"Altin Toplandi! Toplam: {_coinsCollected}";
                    _statusBannerTimer = 2.0f;

                    // Parayı sahneden kaldır
                    coin.Destroy();
                }
            };
        }
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        // Durum bildirim zamanlayıcısı
        if (_statusBannerTimer > 0f)
        {
            _statusBannerTimer -= deltaTime;
            if (_statusBannerTimer <= 0f)
            {
                _statusBanner = "";
            }
        }

        var input = Engine.Services.Get<IInputService>();
        var assetService = Engine.Services.Get<IAssetService>();

        // 8. SAHNEYİ JSON OLARAK KAYDET (F5)
        if (input.IsKeyPressed(Keys.F5))
        {
            try
            {
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scene_save.json");
                SceneSerializer.SaveToFile(this, savePath);
                _statusBanner = "Sahne 'scene_save.json' olarak basariyla kaydedildi! (F5)";
                _statusBannerTimer = 3.5f;
            }
            catch (Exception ex)
            {
                _statusBanner = $"Kayit hatasi: {ex.Message}";
                _statusBannerTimer = 4.0f;
            }
        }

        // 9. SAHNEYİ JSON DOSYASINDAN YÜKLE (F6)
        if (input.IsKeyPressed(Keys.F6))
        {
            try
            {
                string savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scene_save.json");
                if (File.Exists(savePath))
                {
                    var loadedScene = SceneSerializer.LoadFromFile(savePath, assetService);
                    var sceneService = Engine.Services.Get<ISceneService>();
                    sceneService.RegisterScene("LoadedDemoScene", () => loadedScene);
                    sceneService.LoadScene("LoadedDemoScene");
                }
                else
                {
                    _statusBanner = "Kayitli 'scene_save.json' dosyasi bulunamadi! Once F5'e basiniz.";
                    _statusBannerTimer = 3.5f;
                }
            }
            catch (Exception ex)
            {
                _statusBanner = $"Yukleme hatasi: {ex.Message}";
                _statusBannerTimer = 4.0f;
            }
        }
    }

    public override void Draw(SpriteBatchRenderer renderer)
    {
        // 1. Arka plan ızgarası
        DrawWorldGrid(renderer);

        // 2. Sahne varlıkları
        base.Draw(renderer);

        // 3. Çarpıştırıcıların görsel hata ayıklama (Debug Collider) çerçeveleri
        DrawDebugColliders(renderer);
    }

    private void DrawDebugColliders(SpriteBatchRenderer renderer)
    {
        for (int i = 0; i < Entities.Count; i++)
        {
            var e = Entities[i];
            if (!e.IsActive || e.IsDestroyed) continue;

            var box = e.GetComponent<BoxCollider2D>();
            if (box != null && box.IsEnabled)
            {
                var rect = box.GetAABB();
                Color boxColor = box.IsStatic ? new Color(100, 200, 255, 140) : new Color(0, 255, 120, 180);
                renderer.DrawRectangle(rect, boxColor, 1.5f);
            }

            var circle = e.GetComponent<CircleCollider2D>();
            if (circle != null && circle.IsEnabled)
            {
                renderer.DrawCircle(circle.Center, circle.ScaledRadius, new Color(255, 220, 0, 180), segments: 16, thickness: 1.5f);
            }
        }
    }

    private void DrawWorldGrid(SpriteBatchRenderer renderer)
    {
        Color gridColor = new(40, 45, 60, 60);
        int gridSize = 100;
        int min = -1000;
        int max = 1000;

        for (int x = min; x <= max; x += gridSize)
        {
            renderer.DrawLine(new Vector2(x, min), new Vector2(x, max), gridColor, 1f);
        }
        for (int y = min; y <= max; y += gridSize)
        {
            renderer.DrawLine(new Vector2(min, y), new Vector2(max, y), gridColor, 1f);
        }
    }

    private static SpriteSheet CreateAnimatedPlayerSpriteSheet(GraphicsDevice graphicsDevice)
    {
        int frameW = 32;
        int frameH = 32;
        int cols = 4;
        int rows = 2;
        int totalW = frameW * cols;
        int totalH = frameH * rows;

        var tex = new Texture2D(graphicsDevice, totalW, totalH);
        var data = new Color[totalW * totalH];
        Array.Fill(data, Color.Transparent);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int offsetX = c * frameW;
                int offsetY = r * frameH;
                bool isWalk = (r == 1);
                float animPhase = (float)c / cols;

                for (int y = 0; y < frameH; y++)
                {
                    float normY = (float)y / (frameH - 1);
                    float halfWidth = (normY * 0.42f + 0.06f) * frameW;
                    float centerX = frameW * 0.5f;

                    int minX = Math.Clamp((int)(centerX - halfWidth), 0, frameW - 1);
                    int maxX = Math.Clamp((int)(centerX + halfWidth), 0, frameW - 1);

                    for (int x = minX; x <= maxX; x++)
                    {
                        bool isBorder = (x == minX || x == maxX || y == frameH - 1);
                        Color col = isBorder ? new Color(0, 220, 255) : new Color(230, 240, 255);

                        // Idle: hafif parlama dalgalanması
                        if (!isWalk)
                        {
                            if (c % 2 == 1 && !isBorder)
                            {
                                col = new Color(190, 215, 255);
                            }
                        }
                        // Walk: motor alevinde animasyonlu uzama/kısalma
                        else
                        {
                            int flameHeight = 4 + (int)(MathF.Sin(animPhase * MathF.PI * 2f) * 3f + 3f);
                            if (y > frameH - flameHeight && MathF.Abs(x - centerX) < 4)
                            {
                                col = (c % 2 == 0) ? Color.OrangeRed : Color.Gold;
                            }
                        }

                        int px = offsetX + y;
                        int py = offsetY + x;
                        if (px < totalW && py < totalH)
                        {
                            data[py * totalW + px] = col;
                        }
                    }
                }
            }
        }

        tex.SetData(data);
        return new SpriteSheet(tex, frameW, frameH);
    }

    private static void EnsureTilesetTextureExists(IAssetService assetService, GraphicsDevice graphicsDevice)
    {
        string tilemapDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Tilemaps");
        if (!Directory.Exists(tilemapDir))
        {
            Directory.CreateDirectory(tilemapDir);
        }

        string tilesetPath = Path.Combine(tilemapDir, "tileset.png");
        if (!File.Exists(tilesetPath))
        {
            int w = 64;
            int h = 64;
            var tex = new Texture2D(graphicsDevice, w, h);
            var data = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = (x % 32 == 0 || x % 32 == 31 || y % 32 == 0 || y % 32 == 31);
                    Color c;
                    if (x < 32 && y < 32) // Tile 1: Zemin
                    {
                        c = isBorder ? new Color(38, 44, 58) : new Color(26, 30, 42);
                    }
                    else if (x >= 32 && y < 32) // Tile 2: Duvar
                    {
                        c = isBorder ? new Color(140, 150, 170) : new Color(80, 90, 110);
                    }
                    else if (x < 32 && y >= 32) // Tile 3: Enerji / Su
                    {
                        c = isBorder ? new Color(0, 200, 240) : new Color(20, 70, 100);
                    }
                    else // Tile 4: Yol
                    {
                        c = isBorder ? new Color(90, 80, 70) : new Color(55, 50, 45);
                    }
                    data[y * w + x] = c;
                }
            }

            tex.SetData(data);
            using var stream = File.Create(tilesetPath);
            tex.SaveAsPng(stream, w, h);
        }
    }
}
