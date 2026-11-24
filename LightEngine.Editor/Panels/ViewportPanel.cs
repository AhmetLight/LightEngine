using ImGuiNET;
using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Physics2D;
using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor.Panels;

/// <summary>
/// Hibrit 2D / 3D Sahne ve Oyun Görünümü (Viewport).
/// Sahneyi Depth24 destekli RenderTarget2D üzerine fiziksel piksel çözünürlüğünde çizer;
/// 2D modunda tıklama/gizmo/pan/zoom, 3D modunda ise Blinn-Phong render, FPS/Orbit kamera,
/// 3D Raycast nesne seçimi ve 3D sürükleme sağlar.
/// </summary>
public class ViewportPanel : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ImGuiRenderer _imGuiRenderer;
    private readonly EditorContext _context;

    private RenderTarget2D? _renderTarget;
    private IntPtr _renderTargetTextureId;
    private int _targetWidth = 1280;
    private int _targetHeight = 720;

    // 2D Etkileşim değişkenleri
    private bool _isDraggingGizmo;
    private bool _isDraggingBody;
    private int _dragAxis; // 1: X, 2: Y, 3: Center (XY), 4: Rotation, 5: Scale
    private Vector2 _lastDragWorldPos;

    // Undo / Redo için 2D sürükleme başlangıç değerleri
    private Vector2 _dragStartPos;
    private float _dragStartRot;
    private Vector2 _dragStartScale;
    private IEntity? _draggedEntity;

    private bool _isPanningCamera;
    private Vector2 _lastPanScreenPos;

    // 3D Etkileşim değişkenleri
    private bool _isLooking3D;
    private bool _isPanning3D;
    private bool _isDragging3D;
    private int _drag3DAxis; // 0: Center, 1: X (Red), 2: Y (Green), 3: Z (Blue)
    private NumVec2 _lastMouse3D;
    private NumVec2 _rightMouseDownPos;
    private Vector3 _contextMenuSpawnPos;
    private Vector3 _drag3DStartPos;
    private Vector3 _drag3DStartRot;
    private Vector3 _drag3DStartScale;
    private Vector3 _drag3DOffset;
    private float _drag3DPlaneY;
    private Transform3D? _draggedTransform3D;

    public NumVec2 ViewportSize { get; private set; }
    public bool IsHovered { get; private set; }

    public ViewportPanel(GraphicsDevice graphicsDevice, ImGuiRenderer imGuiRenderer, EditorContext context)
    {
        _graphicsDevice = graphicsDevice;
        _imGuiRenderer = imGuiRenderer;
        _context = context;

        RecreateRenderTarget(_targetWidth, _targetHeight);
    }

    private void RecreateRenderTarget(int width, int height)
    {
        if (width <= 0 || height <= 0) return;

        if (_renderTargetTextureId != IntPtr.Zero)
        {
            _imGuiRenderer.UnbindTexture(_renderTargetTextureId);
        }

        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(
            _graphicsDevice,
            width,
            height,
            false,
            SurfaceFormat.Color,
            DepthFormat.Depth24,
            0,
            RenderTargetUsage.DiscardContents);

        _renderTargetTextureId = _imGuiRenderer.BindTexture(_renderTarget);
        _targetWidth = width;
        _targetHeight = height;
    }

    /// <summary>
    /// Sahneyi ImGui çiziminden önce RenderTarget içine çizer (2D veya 3D moda göre).
    /// </summary>
    public void RenderSceneToTarget(IScene scene, SpriteBatchRenderer renderer)
    {
        if (_renderTarget == null || scene == null) return;

        _graphicsDevice.SetRenderTarget(_renderTarget);

        if (_context.Is3DMode)
        {
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, new Color(24, 28, 36), 1.0f, 0);

            if (Engine.Services.TryGet<IRender3DService>(out var r3d) && r3d != null)
            {
                scene.Camera3D.AspectRatio = (float)_targetWidth / Math.Max(1, _targetHeight);
                r3d.RenderScene(scene, scene.Camera3D, drawEditorGrid: true, selectedEntity: _context.SelectedEntity);
            }

            _graphicsDevice.SetRenderTarget(null);
            return;
        }

        _graphicsDevice.Clear(new Color(24, 26, 32));

        scene.Camera.Origin = new Vector2(_targetWidth / 2f, _targetHeight / 2f);

        renderer.Begin(transformMatrix: scene.Camera.ViewMatrix);

        // 1. Izgara (Grid) Çizgileri
        Color gridColor = new(45, 50, 65, 80);
        int gridSize = 64;
        for (int x = -1600; x <= 1600; x += gridSize)
            renderer.DrawLine(new Vector2(x, -1600), new Vector2(x, 1600), gridColor, 1f);
        for (int y = -1600; y <= 1600; y += gridSize)
            renderer.DrawLine(new Vector2(-1600, y), new Vector2(1600, y), gridColor, 1f);

        // Eksen çizgileri (X Kırmızı, Y Yeşil)
        renderer.DrawLine(new Vector2(-1600, 0), new Vector2(1600, 0), new Color(220, 70, 70, 160), 2f);
        renderer.DrawLine(new Vector2(0, -1600), new Vector2(0, 1600), new Color(70, 220, 70, 160), 2f);

        // 2. Sahne Varlıkları
        scene.Draw(renderer);

        // 3. Seçili nesne vurgusu ve Gizmo kolları
        if (_context.SelectedEntity != null && !_context.SelectedEntity.IsDestroyed)
        {
            DrawSelectionAndGizmo(renderer, _context.SelectedEntity, scene.Camera);
        }

        renderer.End();
        _graphicsDevice.SetRenderTarget(null);
    }

    private void DrawSelectionAndGizmo(SpriteBatchRenderer renderer, IEntity entity, Camera2D camera)
    {
        var pos = entity.Transform.WorldPosition;
        Rectangle bounds = GetEntityWorldBounds(entity);

        // Seçim çerçevesi (Kontur çizgisi)
        renderer.DrawRectangle(bounds, Color.Gold, 1.5f);

        // Köşe işaretleri
        int markerSize = 6;
        renderer.DrawFilledRectangle(new Rectangle(bounds.Left - markerSize / 2, bounds.Top - markerSize / 2, markerSize, markerSize), Color.Cyan);
        renderer.DrawFilledRectangle(new Rectangle(bounds.Right - markerSize / 2, bounds.Top - markerSize / 2, markerSize, markerSize), Color.Cyan);
        renderer.DrawFilledRectangle(new Rectangle(bounds.Left - markerSize / 2, bounds.Bottom - markerSize / 2, markerSize, markerSize), Color.Cyan);
        renderer.DrawFilledRectangle(new Rectangle(bounds.Right - markerSize / 2, bounds.Bottom - markerSize / 2, markerSize, markerSize), Color.Cyan);

        // Gizmo Çizimi
        float armLength = 60f / camera.Zoom;
        float centerBoxSize = 12f / camera.Zoom;

        switch (_context.ActiveGizmo)
        {
            case GizmoType.Translate:
            {
                // Merkez kare (XY Serbest taşıma)
                renderer.DrawFilledRectangle(new Rectangle((int)(pos.X - centerBoxSize / 2), (int)(pos.Y - centerBoxSize / 2), (int)centerBoxSize, (int)centerBoxSize), Color.Yellow);

                // X Ekseni (Kırmızı Ok)
                Vector2 xEnd = pos + new Vector2(armLength, 0);
                renderer.DrawLine(pos, xEnd, Color.Crimson, 3f);
                renderer.DrawFilledCircle(xEnd, 5f / camera.Zoom, Color.Crimson, 12);

                // Y Ekseni (Yeşil Ok)
                Vector2 yEnd = pos + new Vector2(0, -armLength);
                renderer.DrawLine(pos, yEnd, Color.LimeGreen, 3f);
                renderer.DrawFilledCircle(yEnd, 5f / camera.Zoom, Color.LimeGreen, 12);
                break;
            }

            case GizmoType.Rotate:
            {
                // Döndürme Halkası
                float ringRadius = 45f / camera.Zoom;
                renderer.DrawCircle(pos, ringRadius, Color.DeepSkyBlue, 32, 2.5f);
                Vector2 rotIndicator = pos + new Vector2(MathF.Cos(entity.Transform.WorldRotation) * ringRadius, MathF.Sin(entity.Transform.WorldRotation) * ringRadius);
                renderer.DrawFilledCircle(rotIndicator, 6f / camera.Zoom, Color.Gold, 12);
                break;
            }

            case GizmoType.Scale:
            {
                // Merkez ölçekleme kutusu
                renderer.DrawFilledRectangle(new Rectangle((int)(pos.X - centerBoxSize / 2), (int)(pos.Y - centerBoxSize / 2), (int)centerBoxSize, (int)centerBoxSize), Color.Gold);

                // X Ekseni Ölçek Kolu
                Vector2 xEnd = pos + new Vector2(armLength, 0);
                renderer.DrawLine(pos, xEnd, Color.Crimson, 3f);
                renderer.DrawFilledRectangle(new Rectangle((int)(xEnd.X - 4 / camera.Zoom), (int)(xEnd.Y - 4 / camera.Zoom), (int)(8 / camera.Zoom), (int)(8 / camera.Zoom)), Color.Crimson);

                // Y Ekseni Ölçek Kolu
                Vector2 yEnd = pos + new Vector2(0, -armLength);
                renderer.DrawLine(pos, yEnd, Color.LimeGreen, 3f);
                renderer.DrawFilledRectangle(new Rectangle((int)(yEnd.X - 4 / camera.Zoom), (int)(yEnd.Y - 4 / camera.Zoom), (int)(8 / camera.Zoom), (int)(8 / camera.Zoom)), Color.LimeGreen);
                break;
            }
        }
    }

    public void Draw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new NumVec2(0, 0));

        // Viewport penceresi DockSpace içinde ortada yer alır; içerik kaydırmaları için NoScrollbar bayrağı kullanılır
        if (ImGui.Begin("Scene Viewport##ViewportPanel", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (_context.FocusSceneTabRequested)
            {
                ImGui.SetWindowFocus();
                _context.FocusSceneTabRequested = false;
            }

            var availSize = ImGui.GetContentRegionAvail();

            int desiredW = Math.Max((int)availSize.X, 320);
            int desiredH = Math.Max((int)availSize.Y, 240);

            if (desiredW != _targetWidth || desiredH != _targetHeight)
            {
                RecreateRenderTarget(desiredW, desiredH);
            }

            ViewportSize = availSize;
            NumVec2 imageStart = ImGui.GetCursorScreenPos();

            // 1. Sahne görüntüsü
            if (_renderTarget != null && _renderTargetTextureId != IntPtr.Zero)
            {
                ImGui.Image(_renderTargetTextureId, availSize);
            }

            // 2. Görselin üzerine tam oturan InvisibleButton (Pencerenin sürüklenmesini ve odak kaçmasını tamamen engeller)
            ImGui.SetCursorScreenPos(imageStart);
            ImGui.InvisibleButton("##ViewportCanvas", availSize, ImGuiButtonFlags.MouseButtonLeft | ImGuiButtonFlags.MouseButtonRight | ImGuiButtonFlags.MouseButtonMiddle);
            bool isCanvasActive = ImGui.IsItemActive();
            bool isCanvasHovered = ImGui.IsItemHovered();
            IsHovered = isCanvasHovered;

            // Asset Browser'dan Viewport'a sürükle-bırak (Drag & Drop Target)
            if (ImGui.BeginDragDropTarget())
            {
                var modelPayload = ImGui.AcceptDragDropPayload("ASSET_MODEL_PATH");
                unsafe
                {
                    if (modelPayload.NativePtr != null && !string.IsNullOrEmpty(_context.SelectedAssetPath) && _context.ActiveScene != null)
                    {
                        var scene = _context.ActiveScene;
                        var mouse = ImGui.GetMousePos();
                        float normX = Math.Clamp((mouse.X - imageStart.X) / Math.Max(1f, availSize.X), 0f, 1f);
                        float normY = Math.Clamp((mouse.Y - imageStart.Y) / Math.Max(1f, availSize.Y), 0f, 1f);
                        Vector2 screenPx = new(normX * _targetWidth, normY * _targetHeight);

                        if (_context.Is3DMode)
                        {
                            Ray ray = scene.Camera3D.ScreenPointToRay(screenPx, _targetWidth, _targetHeight);
                            Vector3 spawnPos = IntersectHorizontalPlane(ray, 0f, out var hit) ? hit : Vector3.Zero;

                            var renderService3D = Engine.Services.Get<IRender3DService>();
                            var mesh = renderService3D.LoadModel(_context.SelectedAssetPath);
                            var entity = scene.CreateEntity(Path.GetFileNameWithoutExtension(_context.SelectedAssetPath));
                            entity.AddComponent(new Transform3D { LocalPosition = spawnPos });
                            entity.AddComponent(new MeshRenderer3D(mesh));
                            _context.SelectedEntity = entity;
                            _context.Log($"Spawned 3D Model '{entity.Name}' at ({spawnPos.X:0.1}, {spawnPos.Y:0.1}, {spawnPos.Z:0.1})", LogLevel.Info);
                        }
                    }
                }

                var texPayload = ImGui.AcceptDragDropPayload("ASSET_TEXTURE_PATH");
                unsafe
                {
                    if (texPayload.NativePtr != null && !string.IsNullOrEmpty(_context.SelectedAssetPath) && _context.ActiveScene != null)
                    {
                        var scene = _context.ActiveScene;
                        var mouse = ImGui.GetMousePos();
                        float normX = Math.Clamp((mouse.X - imageStart.X) / Math.Max(1f, availSize.X), 0f, 1f);
                        float normY = Math.Clamp((mouse.Y - imageStart.Y) / Math.Max(1f, availSize.Y), 0f, 1f);
                        Vector2 screenPx = new(normX * _targetWidth, normY * _targetHeight);

                        var assetService = Engine.Services.Get<IAssetService>();
                        var tex = assetService.LoadTexture(_context.SelectedAssetPath);
                        var entity = scene.CreateEntity(Path.GetFileNameWithoutExtension(_context.SelectedAssetPath));
                        entity.Transform.LocalPosition = scene.Camera.ScreenToWorld(screenPx);
                        entity.AddComponent(new SpriteRenderer(tex, centerOrigin: true));
                        _context.SelectedEntity = entity;
                        _context.Log($"Spawned 2D Sprite '{entity.Name}' at {entity.Transform.LocalPosition}", LogLevel.Info);
                    }
                }
                ImGui.EndDragDropTarget();
            }

            // 3. Girdi ve etkileşimleri işle (2D veya 3D moda göre)
            if (_context.Is3DMode)
            {
                HandleViewportInteraction3D(imageStart, availSize, isCanvasActive, isCanvasHovered);
            }
            else
            {
                HandleViewportInteraction(imageStart, availSize, isCanvasActive, isCanvasHovered);
            }

            // Viewport üzeri sol üst rozet (Çözünürlük, 2D/3D ve durum)
            ImGui.SetCursorPos(new NumVec2(10, 30));
            string dimTag = _context.Is3DMode ? "[3D PERSPECTIVE]" : "[2D ORTHO]";
            ImGui.TextColored(new NumVec4(0.4f, 0.9f, 0.5f, 0.85f), $"{dimTag} {_targetWidth}x{_targetHeight} px");

            ImGui.SetCursorPos(new NumVec2(10, 48));
            string modeText = _context.IsPlaying ? "MODE: [SCENE PREVIEW - PLAYING]" : "MODE: [SCENE EDITING]";
            NumVec4 modeColor = _context.IsPlaying ? new NumVec4(0.3f, 0.9f, 0.4f, 0.9f) : new NumVec4(1f, 0.8f, 0.2f, 0.9f);
            ImGui.TextColored(modeColor, modeText);

            if (_context.Is3DMode && Engine.Services.TryGet<IRender3DService>(out var r3d) && r3d != null)
            {
                ImGui.SetCursorPos(new NumVec2(10, 66));
                ImGui.TextColored(new NumVec4(0.65f, 0.82f, 1.0f, 0.85f),
                    $"Meshes: {r3d.RenderedMeshCount} (Culled: {r3d.CulledMeshCount}) | Tris: {r3d.RenderedTriangleCount}");

                ImGui.SetCursorPos(new NumVec2(10, availSize.Y - 24));
                ImGui.TextDisabled("3D Controls: RMB+Drag Look | RMB+WASD/QE Fly | MMB Pan | Wheel Zoom | LMB Select/Move | F Focus");
            }

            // Sağ üst: Switch to Game View & Gizmo Modu göstergesi
            ImGui.SetCursorPos(new NumVec2(Math.Max(180f, availSize.X - 350f), 30));
            if (ImGui.SmallButton("Switch to Game View >>"))
            {
                _context.FocusGameTabRequested = true;
            }

            ImGui.SameLine(0, 15);
            ImGui.TextColored(new NumVec4(0.9f, 0.7f, 0.2f, 0.9f), $"Tool: {_context.ActiveGizmo} [W/E/R]");
        }
        ImGui.End();

        ImGui.PopStyleVar();
    }

    private void HandleViewportInteraction3D(NumVec2 imageStart, NumVec2 imageSize, bool isCanvasActive, bool isCanvasHovered)
    {
        var scene = _context.ActiveScene;
        if (scene == null) return;

        var camera = scene.Camera3D;
        var io = ImGui.GetIO();
        NumVec2 mousePos = ImGui.GetMousePos();
        float dt = Math.Clamp(io.DeltaTime, 0.001f, 0.05f);

        float normX = Math.Clamp((mousePos.X - imageStart.X) / Math.Max(1f, imageSize.X), 0f, 1f);
        float normY = Math.Clamp((mousePos.Y - imageStart.Y) / Math.Max(1f, imageSize.Y), 0f, 1f);
        Vector2 screenPx = new(normX * _targetWidth, normY * _targetHeight);

        // 1. Sağ Tık ile 3D Serbest Uçuş (FPS Look + WASD/QE) veya Tek Tık Sağ Menü
        if (ImGui.IsMouseDown(ImGuiMouseButton.Right) && (isCanvasHovered || _isLooking3D))
        {
            if (!_isLooking3D)
            {
                _isLooking3D = true;
                _lastMouse3D = mousePos;
                _rightMouseDownPos = mousePos;
            }
            else
            {
                float dx = mousePos.X - _lastMouse3D.X;
                float dy = mousePos.Y - _lastMouse3D.Y;
                _lastMouse3D = mousePos;

                camera.Yaw -= dx * 0.005f;
                camera.Pitch -= dy * 0.005f;
            }

            // Sağ tık basılıyken WASD + Q/E ile 3D uçuş
            float moveSpeed = (io.KeyShift ? 16f : 6.5f) * dt;
            if (ImGui.IsKeyDown(ImGuiKey.W)) camera.Position += camera.Forward * moveSpeed;
            if (ImGui.IsKeyDown(ImGuiKey.S)) camera.Position -= camera.Forward * moveSpeed;
            if (ImGui.IsKeyDown(ImGuiKey.D)) camera.Position += camera.Right * moveSpeed;
            if (ImGui.IsKeyDown(ImGuiKey.A)) camera.Position -= camera.Right * moveSpeed;
            if (ImGui.IsKeyDown(ImGuiKey.E)) camera.Position += Vector3.Up * moveSpeed;
            if (ImGui.IsKeyDown(ImGuiKey.Q)) camera.Position -= Vector3.Up * moveSpeed;

            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            return;
        }
        else
        {
            if (_isLooking3D)
            {
                if (NumVec2.Distance(mousePos, _rightMouseDownPos) < 6f && isCanvasHovered)
                {
                    Ray ray = camera.ScreenPointToRay(screenPx, _targetWidth, _targetHeight);
                    _contextMenuSpawnPos = IntersectHorizontalPlane(ray, 0f, out var hit) ? hit : Vector3.Zero;
                    ImGui.OpenPopup("Viewport3DContextMenu");
                }
                _isLooking3D = false;
            }
        }

        // 2. Orta Fare Tuşu ile 3D Kamera Pan
        if (ImGui.IsMouseDown(ImGuiMouseButton.Middle) && (isCanvasHovered || _isPanning3D))
        {
            if (!_isPanning3D)
            {
                _isPanning3D = true;
                _lastMouse3D = mousePos;
            }
            else
            {
                float dx = mousePos.X - _lastMouse3D.X;
                float dy = mousePos.Y - _lastMouse3D.Y;
                _lastMouse3D = mousePos;

                camera.Position -= camera.Right * (dx * 0.02f);
                camera.Position += camera.Up * (dy * 0.02f);
            }
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            return;
        }
        else
        {
            _isPanning3D = false;
        }

        // 3. Kısayollar ve Tekerlek ile 3D Dolly Zoom
        if (isCanvasHovered && !io.WantTextInput)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) _context.ActiveGizmo = GizmoType.Translate;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) _context.ActiveGizmo = GizmoType.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) _context.ActiveGizmo = GizmoType.Scale;

            if (ImGui.IsKeyPressed(ImGuiKey.F) && _context.SelectedEntity != null)
            {
                var t3d = _context.SelectedEntity.GetComponent<Transform3D>();
                if (t3d != null)
                {
                    camera.FocusOn(t3d.WorldPosition, 6f);
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Delete) && _context.SelectedEntity != null)
            {
                _context.Log($"Deleted: {_context.SelectedEntity.Name}", LogLevel.Warning);
                _context.SelectedEntity.Destroy();
                _context.SelectedEntity = null;
            }

            float wheel = io.MouseWheel;
            if (wheel != 0f)
            {
                camera.Position += camera.Forward * (wheel * 1.2f);
            }
        }

        // 4. Seçili 3D Varlık için 2D Gizmo Eksen İzdüşümü ve Çizimi
        NumVec2 orig2D = NumVec2.Zero;
        NumVec2 x2D = NumVec2.Zero;
        NumVec2 y2D = NumVec2.Zero;
        NumVec2 z2D = NumVec2.Zero;
        bool gizmoVisible = false;

        if (_context.SelectedEntity != null && !_context.SelectedEntity.IsDestroyed)
        {
            var selT3d = _context.SelectedEntity.GetComponent<Transform3D>();
            if (selT3d != null)
            {
                Vector3 origin3D = selT3d.WorldPosition;
                Vector3 screenOrig = camera.WorldToScreen(origin3D, _targetWidth, _targetHeight);
                if (screenOrig.Z >= 0.0f && screenOrig.Z <= 1.0f)
                {
                    gizmoVisible = true;
                    orig2D = new NumVec2(imageStart.X + (screenOrig.X / _targetWidth) * imageSize.X, imageStart.Y + (screenOrig.Y / _targetHeight) * imageSize.Y);

                    float gizmoLen = 1.6f;
                    Vector3 screenX = camera.WorldToScreen(origin3D + Vector3.UnitX * gizmoLen, _targetWidth, _targetHeight);
                    Vector3 screenY = camera.WorldToScreen(origin3D + Vector3.UnitY * gizmoLen, _targetWidth, _targetHeight);
                    Vector3 screenZ = camera.WorldToScreen(origin3D + Vector3.UnitZ * gizmoLen, _targetWidth, _targetHeight);

                    x2D = new NumVec2(imageStart.X + (screenX.X / _targetWidth) * imageSize.X, imageStart.Y + (screenX.Y / _targetHeight) * imageSize.Y);
                    y2D = new NumVec2(imageStart.X + (screenY.X / _targetWidth) * imageSize.X, imageStart.Y + (screenY.Y / _targetHeight) * imageSize.Y);
                    z2D = new NumVec2(imageStart.X + (screenZ.X / _targetWidth) * imageSize.X, imageStart.Y + (screenZ.Y / _targetHeight) * imageSize.Y);

                    var dl = ImGui.GetWindowDrawList();

                    // X Ekseni (Kırmızı)
                    dl.AddLine(orig2D, x2D, 0xFF3535E8, 3.5f);
                    dl.AddCircleFilled(x2D, 5.5f, 0xFF3535E8);
                    dl.AddText(x2D + new NumVec2(4, -4), 0xFFFFFFFF, "X");

                    // Y Ekseni (Yeşil)
                    dl.AddLine(orig2D, y2D, 0xFF35E835, 3.5f);
                    dl.AddCircleFilled(y2D, 5.5f, 0xFF35E835);
                    dl.AddText(y2D + new NumVec2(4, -4), 0xFFFFFFFF, "Y");

                    // Z Ekseni (Mavi)
                    dl.AddLine(orig2D, z2D, 0xFFE88535, 3.5f);
                    dl.AddCircleFilled(z2D, 5.5f, 0xFFE88535);
                    dl.AddText(z2D + new NumVec2(4, -4), 0xFFFFFFFF, "Z");

                    // Merkez (Sarı)
                    dl.AddCircleFilled(orig2D, 6f, 0xFF30EEEE);
                }
            }
        }

        // 5. Sol Tık: Gizmo Eksenleri Hit-Test veya 3D Raycast Seçim
        if (isCanvasHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            bool hitGizmo3D = false;
            Ray ray = camera.ScreenPointToRay(screenPx, _targetWidth, _targetHeight);

            if (gizmoVisible && _context.SelectedEntity != null)
            {
                var selT3d = _context.SelectedEntity.GetComponent<Transform3D>();
                if (selT3d != null)
                {
                    float dCenter = NumVec2.Distance(mousePos, orig2D);
                    float dX = DistanceToSegment(mousePos, orig2D, x2D);
                    float dY = DistanceToSegment(mousePos, orig2D, y2D);
                    float dZ = DistanceToSegment(mousePos, orig2D, z2D);

                    if (dCenter <= 12f)
                    {
                        hitGizmo3D = true;
                        _drag3DAxis = 0; // Merkez Serbest Taşıma
                    }
                    else if (dX <= 10f || NumVec2.Distance(mousePos, x2D) <= 12f)
                    {
                        hitGizmo3D = true;
                        _drag3DAxis = 1; // X Ekseni (Kırmızı)
                    }
                    else if (dY <= 10f || NumVec2.Distance(mousePos, y2D) <= 12f)
                    {
                        hitGizmo3D = true;
                        _drag3DAxis = 2; // Y Ekseni (Yeşil)
                    }
                    else if (dZ <= 10f || NumVec2.Distance(mousePos, z2D) <= 12f)
                    {
                        hitGizmo3D = true;
                        _drag3DAxis = 3; // Z Ekseni (Mavi)
                    }

                    if (hitGizmo3D)
                    {
                        _isDragging3D = true;
                        _draggedTransform3D = selT3d;
                        _drag3DStartPos = selT3d.LocalPosition;
                        _drag3DStartRot = selT3d.LocalRotationDegrees;
                        _drag3DStartScale = selT3d.LocalScale;
                        _drag3DPlaneY = selT3d.WorldPosition.Y;
                        _lastMouse3D = mousePos;
                        if (IntersectHorizontalPlane(ray, _drag3DPlaneY, out Vector3 hp))
                        {
                            _drag3DOffset = selT3d.LocalPosition - hp;
                        }
                    }
                }
            }

            if (!hitGizmo3D)
            {
                IEntity? closestEntity = null;
                float closestDist = float.MaxValue;

                var entities = scene.Entities;
                for (int i = 0; i < entities.Count; i++)
                {
                    var e = entities[i];
                    if (!e.IsActive || e.IsDestroyed) continue;

                    var mr = e.GetComponent<MeshRenderer3D>();
                    if (mr == null || mr.Mesh == null) continue;

                    BoundingBox box = mr.GetWorldBoundingBox();
                    float? dist = ray.Intersects(box);
                    if (dist.HasValue && dist.Value < closestDist)
                    {
                        closestDist = dist.Value;
                        closestEntity = e;
                    }
                }

                _context.SelectedEntity = closestEntity;

                if (closestEntity != null)
                {
                    var t3d = closestEntity.GetComponent<Transform3D>();
                    if (t3d != null)
                    {
                        _isDragging3D = true;
                        _drag3DAxis = 0;
                        _draggedTransform3D = t3d;
                        _drag3DStartPos = t3d.LocalPosition;
                        _drag3DStartRot = t3d.LocalRotationDegrees;
                        _drag3DStartScale = t3d.LocalScale;
                        _drag3DPlaneY = t3d.WorldPosition.Y;

                        if (IntersectHorizontalPlane(ray, _drag3DPlaneY, out Vector3 hitPoint))
                        {
                            _drag3DOffset = t3d.LocalPosition - hitPoint;
                        }
                        else
                        {
                            _drag3DOffset = Vector3.Zero;
                        }
                        _lastMouse3D = mousePos;
                    }
                    _context.Log($"Selected 3D entity: {closestEntity.Name}");
                }
            }
        }

        // 6. Aktif 3D Sürükleme (Translate / Rotate / Scale)
        if (_isDragging3D && ImGui.IsMouseDown(ImGuiMouseButton.Left) && _draggedTransform3D != null)
        {
            float dx = mousePos.X - _lastMouse3D.X;
            float dy = mousePos.Y - _lastMouse3D.Y;
            _lastMouse3D = mousePos;

            float camDist = Math.Max(1f, Vector3.Distance(camera.Position, _draggedTransform3D.WorldPosition));
            float worldMoveFactor = camDist * 0.003f;

            if (_context.ActiveGizmo == GizmoType.Translate)
            {
                if (_drag3DAxis == 1) // X Axis
                {
                    NumVec2 dir2D = x2D - orig2D;
                    float len = dir2D.Length();
                    if (len > 0.001f) dir2D /= len;
                    float proj = dx * dir2D.X + dy * dir2D.Y;
                    var pos = _draggedTransform3D.LocalPosition;
                    pos.X += proj * worldMoveFactor;
                    if (_context.SnapToGrid || io.KeyCtrl) pos.X = MathF.Round(pos.X * 2f) / 2f;
                    _draggedTransform3D.LocalPosition = pos;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
                }
                else if (_drag3DAxis == 2) // Y Axis
                {
                    NumVec2 dir2D = y2D - orig2D;
                    float len = dir2D.Length();
                    if (len > 0.001f) dir2D /= len;
                    float proj = dx * dir2D.X + dy * dir2D.Y;
                    var pos = _draggedTransform3D.LocalPosition;
                    pos.Y += proj * worldMoveFactor;
                    if (_context.SnapToGrid || io.KeyCtrl) pos.Y = MathF.Round(pos.Y * 2f) / 2f;
                    _draggedTransform3D.LocalPosition = pos;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
                }
                else if (_drag3DAxis == 3) // Z Axis
                {
                    NumVec2 dir2D = z2D - orig2D;
                    float len = dir2D.Length();
                    if (len > 0.001f) dir2D /= len;
                    float proj = dx * dir2D.X + dy * dir2D.Y;
                    var pos = _draggedTransform3D.LocalPosition;
                    pos.Z += proj * worldMoveFactor;
                    if (_context.SnapToGrid || io.KeyCtrl) pos.Z = MathF.Round(pos.Z * 2f) / 2f;
                    _draggedTransform3D.LocalPosition = pos;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
                }
                else // Center (XZ Plane)
                {
                    Ray ray = camera.ScreenPointToRay(screenPx, _targetWidth, _targetHeight);
                    if (IntersectHorizontalPlane(ray, _drag3DPlaneY, out Vector3 hitPoint))
                    {
                        Vector3 newPos = hitPoint + _drag3DOffset;
                        newPos.Y = _drag3DPlaneY;
                        if (_context.SnapToGrid || io.KeyCtrl)
                        {
                            newPos.X = MathF.Round(newPos.X * 2f) / 2f;
                            newPos.Z = MathF.Round(newPos.Z * 2f) / 2f;
                        }
                        _draggedTransform3D.LocalPosition = newPos;
                    }
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
            else if (_context.ActiveGizmo == GizmoType.Rotate)
            {
                if (_drag3DAxis == 1) _draggedTransform3D.Rotate(new Vector3(-dy * 0.8f, 0, 0));
                else if (_drag3DAxis == 2) _draggedTransform3D.Rotate(new Vector3(0, dx * 0.8f, 0));
                else if (_drag3DAxis == 3) _draggedTransform3D.Rotate(new Vector3(0, 0, dx * 0.8f));
                else _draggedTransform3D.Rotate(new Vector3(dy * 0.6f, dx * 0.6f, 0f));
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
            }
            else if (_context.ActiveGizmo == GizmoType.Scale)
            {
                float factor = Math.Clamp(1f + (dx - dy) * 0.01f, 0.1f, 10f);
                if (_drag3DAxis == 1) _draggedTransform3D.LocalScale = new Vector3(_draggedTransform3D.LocalScale.X * factor, _draggedTransform3D.LocalScale.Y, _draggedTransform3D.LocalScale.Z);
                else if (_drag3DAxis == 2) _draggedTransform3D.LocalScale = new Vector3(_draggedTransform3D.LocalScale.X, _draggedTransform3D.LocalScale.Y * factor, _draggedTransform3D.LocalScale.Z);
                else if (_drag3DAxis == 3) _draggedTransform3D.LocalScale = new Vector3(_draggedTransform3D.LocalScale.X, _draggedTransform3D.LocalScale.Y, _draggedTransform3D.LocalScale.Z * factor);
                else _draggedTransform3D.LocalScale = Vector3.Max(new Vector3(0.05f), _draggedTransform3D.LocalScale * factor);
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
            }
            return;
        }

        // 7. Sürükleme Bittiğinde Tek Bir 3D Undo Kaydı Oluştur
        if (_isDragging3D && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (_draggedTransform3D != null && _draggedTransform3D.Entity != null && !_draggedTransform3D.Entity.IsDestroyed)
            {
                bool changed = _draggedTransform3D.LocalPosition != _drag3DStartPos ||
                               _draggedTransform3D.LocalRotationDegrees != _drag3DStartRot ||
                               _draggedTransform3D.LocalScale != _drag3DStartScale;
                if (changed)
                {
                    _context.RecordCommand(new Commands.Transform3DCommand(
                        _draggedTransform3D,
                        _drag3DStartPos, _drag3DStartRot, _drag3DStartScale,
                        _draggedTransform3D.LocalPosition, _draggedTransform3D.LocalRotationDegrees, _draggedTransform3D.LocalScale));
                }
            }
            _isDragging3D = false;
            _draggedTransform3D = null;
        }

        // 8. 3D Boş Alana Sağ Tık Menüsü (Create 3D Object at Cursor)
        if (ImGui.BeginPopup("Viewport3DContextMenu"))
        {
            ImGui.TextDisabled($"Create 3D Object at ({_contextMenuSpawnPos.X:0.1}, {_contextMenuSpawnPos.Y:0.1}, {_contextMenuSpawnPos.Z:0.1})");
            ImGui.Separator();
            if (ImGui.MenuItem("Cube")) _context.Create3DPrimitive(PrimitiveType3D.Cube, _contextMenuSpawnPos);
            if (ImGui.MenuItem("Sphere")) _context.Create3DPrimitive(PrimitiveType3D.Sphere, _contextMenuSpawnPos);
            if (ImGui.MenuItem("Plane")) _context.Create3DPrimitive(PrimitiveType3D.Plane, _contextMenuSpawnPos);
            if (ImGui.MenuItem("Cylinder")) _context.Create3DPrimitive(PrimitiveType3D.Cylinder, _contextMenuSpawnPos);
            if (ImGui.MenuItem("Cone")) _context.Create3DPrimitive(PrimitiveType3D.Cone, _contextMenuSpawnPos);
            if (ImGui.MenuItem("Pyramid")) _context.Create3DPrimitive(PrimitiveType3D.Pyramid, _contextMenuSpawnPos);
            ImGui.Separator();
            if (ImGui.MenuItem("Physics Dynamic Cube")) _context.Create3DPhysicsObject(PrimitiveType3D.Cube, _contextMenuSpawnPos + Vector3.Up * 1.5f);
            if (ImGui.MenuItem("Physics Dynamic Sphere")) _context.Create3DPhysicsObject(PrimitiveType3D.Sphere, _contextMenuSpawnPos + Vector3.Up * 1.5f);
            if (ImGui.MenuItem("3D Character Controller")) _context.Create3DCharacterController(_contextMenuSpawnPos);
            if (ImGui.MenuItem("3D Audio Emitter")) _context.Create3DAudioEmitter(_contextMenuSpawnPos);
            if (ImGui.MenuItem("Terrain (Heightmap)")) _context.Create3DTerrain(_contextMenuSpawnPos);
            if (ImGui.MenuItem("3D Particle Emitter")) _context.Create3DParticleEmitter(_contextMenuSpawnPos);
            ImGui.Separator();
            if (ImGui.MenuItem("Directional Light")) _context.Create3DLight(LightType3D.Directional);
            if (ImGui.MenuItem("Point Light")) _context.Create3DLight(LightType3D.Point);
            ImGui.EndPopup();
        }
    }

    private static bool IntersectHorizontalPlane(Ray ray, float planeY, out Vector3 hitPoint)
    {
        if (MathF.Abs(ray.Direction.Y) < 0.0001f)
        {
            hitPoint = Vector3.Zero;
            return false;
        }
        float t = (planeY - ray.Position.Y) / ray.Direction.Y;
        if (t < 0f || t > 500f)
        {
            hitPoint = Vector3.Zero;
            return false;
        }
        hitPoint = ray.Position + ray.Direction * t;
        return true;
    }

    private static float DistanceToSegment(NumVec2 p, NumVec2 a, NumVec2 b)
    {
        NumVec2 ab = b - a;
        float l2 = ab.LengthSquared();
        if (l2 < 0.0001f) return NumVec2.Distance(p, a);
        float t = Math.Clamp(NumVec2.Dot(p - a, ab) / l2, 0f, 1f);
        NumVec2 proj = a + ab * t;
        return NumVec2.Distance(p, proj);
    }

    private void HandleViewportInteraction(NumVec2 imageStart, NumVec2 imageSize, bool isCanvasActive, bool isCanvasHovered)
    {
        var scene = _context.ActiveScene;
        if (scene == null) return;

        NumVec2 mousePos = ImGui.GetMousePos();
        var io = ImGui.GetIO();

        // Fareyi render target piksel koordinatına ve dünya koordinatına dönüştür
        float normX = Math.Clamp((mousePos.X - imageStart.X) / imageSize.X, 0f, 1f);
        float normY = Math.Clamp((mousePos.Y - imageStart.Y) / imageSize.Y, 0f, 1f);
        Vector2 screenPx = new(normX * _targetWidth, normY * _targetHeight);
        Vector2 worldMouse = scene.Camera.ScreenToWorld(screenPx);

        // 1. KISAYOLLAR (Yalnızca metin kutusuna yazılmıyorken ve viewport aktif/hovered iken)
        if (isCanvasHovered && !io.WantTextInput)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.W)) _context.ActiveGizmo = GizmoType.Translate;
            if (ImGui.IsKeyPressed(ImGuiKey.E)) _context.ActiveGizmo = GizmoType.Rotate;
            if (ImGui.IsKeyPressed(ImGuiKey.R)) _context.ActiveGizmo = GizmoType.Scale;

            if (ImGui.IsKeyPressed(ImGuiKey.F) && _context.SelectedEntity != null)
            {
                scene.Camera.Position = _context.SelectedEntity.Transform.WorldPosition;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Delete) && _context.SelectedEntity != null)
            {
                _context.Log($"Deleted: {_context.SelectedEntity.Name}", LogLevel.Warning);
                _context.SelectedEntity.Destroy();
                _context.SelectedEntity = null;
            }

            // Fare imlecine doğru akıllı zoom (Google Maps / Blender stili)
            float wheel = io.MouseWheel;
            if (wheel != 0f)
            {
                float oldZoom = scene.Camera.Zoom;
                float zoomFactor = MathF.Pow(1.15f, wheel);
                float newZoom = Math.Clamp(oldZoom * zoomFactor, 0.1f, 10f);
                if (MathF.Abs(newZoom - oldZoom) > 0.0001f)
                {
                    scene.Camera.Position = worldMouse - (screenPx - scene.Camera.Origin) / newZoom;
                    scene.Camera.Zoom = newZoom;
                }
            }
        }

        // 2. KAMERA PANNING (Orta tuş veya Space + Sol tık)
        bool isPanRequested = ImGui.IsMouseDown(ImGuiMouseButton.Middle) || (ImGui.IsKeyDown(ImGuiKey.Space) && ImGui.IsMouseDown(ImGuiMouseButton.Left));
        if (isPanRequested && !_isDraggingGizmo && !_isDraggingBody)
        {
            if (!_isPanningCamera)
            {
                if (isCanvasHovered || isCanvasActive)
                {
                    _isPanningCamera = true;
                    _lastPanScreenPos = screenPx;
                }
            }
            else
            {
                Vector2 panDeltaWorld = scene.Camera.ScreenToWorld(screenPx) - scene.Camera.ScreenToWorld(_lastPanScreenPos);
                scene.Camera.Position -= panDeltaWorld;
                _lastPanScreenPos = screenPx;
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            return;
        }
        else
        {
            _isPanningCamera = false;
        }

        // Space tuşu basılıyken pan hazırlığı (El imleci)
        if (isCanvasHovered && ImGui.IsKeyDown(ImGuiKey.Space))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        // 3. TIKLAMA VE SÜRÜKLEME BAŞLATMA (Gizmo ve Nesne Hit-Test)
        if (isCanvasHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsKeyDown(ImGuiKey.Space))
        {
            bool hitGizmo = false;

            // A) Önce Gizmo kollarını kontrol et (GİZMO ÖNCELİKLİ)
            if (_context.SelectedEntity != null && !_context.SelectedEntity.IsDestroyed)
            {
                var selEntity = _context.SelectedEntity;
                var pos = selEntity.Transform.WorldPosition;
                float handleRadius = 16f / scene.Camera.Zoom;
                float armLen = 60f / scene.Camera.Zoom;

                if (_context.ActiveGizmo == GizmoType.Translate)
                {
                    // Merkez kare (XY Serbest Taşıma)
                    if (Vector2.Distance(worldMouse, pos) <= handleRadius)
                    {
                        hitGizmo = true;
                        _dragAxis = 3;
                    }
                    // X Ekseni (Kırmızı)
                    else if (Vector2.Distance(worldMouse, pos + new Vector2(armLen, 0)) <= handleRadius ||
                             (worldMouse.X >= pos.X && worldMouse.X <= pos.X + armLen && MathF.Abs(worldMouse.Y - pos.Y) <= handleRadius * 0.6f))
                    {
                        hitGizmo = true;
                        _dragAxis = 1;
                    }
                    // Y Ekseni (Yeşil)
                    else if (Vector2.Distance(worldMouse, pos + new Vector2(0, -armLen)) <= handleRadius ||
                             (worldMouse.Y <= pos.Y && worldMouse.Y >= pos.Y - armLen && MathF.Abs(worldMouse.X - pos.X) <= handleRadius * 0.6f))
                    {
                        hitGizmo = true;
                        _dragAxis = 2;
                    }
                }
                else if (_context.ActiveGizmo == GizmoType.Rotate)
                {
                    float ringRadius = 45f / scene.Camera.Zoom;
                    if (MathF.Abs(Vector2.Distance(worldMouse, pos) - ringRadius) <= handleRadius)
                    {
                        hitGizmo = true;
                        _dragAxis = 4;
                    }
                }
                else if (_context.ActiveGizmo == GizmoType.Scale)
                {
                    if (Vector2.Distance(worldMouse, pos) <= handleRadius)
                    {
                        hitGizmo = true;
                        _dragAxis = 3; // Merkez orantılı ölçek
                    }
                    else if (Vector2.Distance(worldMouse, pos + new Vector2(armLen, 0)) <= handleRadius)
                    {
                        hitGizmo = true;
                        _dragAxis = 1; // X ölçek
                    }
                    else if (Vector2.Distance(worldMouse, pos + new Vector2(0, -armLen)) <= handleRadius)
                    {
                        hitGizmo = true;
                        _dragAxis = 2; // Y ölçek
                    }
                }

                if (hitGizmo)
                {
                    _isDraggingGizmo = true;
                    _draggedEntity = selEntity;
                    _dragStartPos = selEntity.Transform.LocalPosition;
                    _dragStartRot = selEntity.Transform.LocalRotation;
                    _dragStartScale = selEntity.Transform.LocalScale;
                    _lastDragWorldPos = worldMouse;
                }
            }

            // B) Gizmo vurulmadıysa Nesne Gövdesini Hit-Test Yap (Gövde sürükleme ve Seçim)
            if (!hitGizmo)
            {
                IEntity? hitEntity = null;

                // Önce seçili nesneyi kontrol et
                if (_context.SelectedEntity != null && !_context.SelectedEntity.IsDestroyed &&
                    GetEntityWorldBounds(_context.SelectedEntity).Contains((int)worldMouse.X, (int)worldMouse.Y))
                {
                    hitEntity = _context.SelectedEntity;
                }
                else
                {
                    // Sahnedeki tüm nesneleri önden arkaya kontrol et
                    for (int i = scene.Entities.Count - 1; i >= 0; i--)
                    {
                        var e = scene.Entities[i];
                        if (!e.IsActive || e.IsDestroyed) continue;

                        Rectangle bounds = GetEntityWorldBounds(e);
                        if (bounds.Contains((int)worldMouse.X, (int)worldMouse.Y))
                        {
                            hitEntity = e;
                            break;
                        }
                    }
                }

                if (hitEntity != null)
                {
                    // Nesneye tıklandı: Seç ve gövde sürüklemeyi başlat (Serbest taşıma)
                    _context.SelectedEntity = hitEntity;
                    _isDraggingBody = true;
                    _draggedEntity = hitEntity;
                    _dragStartPos = hitEntity.Transform.LocalPosition;
                    _dragStartRot = hitEntity.Transform.LocalRotation;
                    _dragStartScale = hitEntity.Transform.LocalScale;
                    _lastDragWorldPos = worldMouse;
                    _context.Log($"Selected entity: {hitEntity.Name}");
                }
                else
                {
                    // Boş alana tıklandı: Seçimi kaldır
                    _context.SelectedEntity = null;
                }
            }
        }

        // 4. AKTİF SÜRÜKLEME GÜNCELLEMESİ (Sol fare tuşu basılı kaldığı sürece devam eder)
        if ((_isDraggingGizmo || _isDraggingBody) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (_draggedEntity == null || _draggedEntity.IsDestroyed)
            {
                _isDraggingGizmo = false;
                _isDraggingBody = false;
                return;
            }

            Vector2 delta = worldMouse - _lastDragWorldPos;

            if (_isDraggingBody)
            {
                // Gövdeden tutarak serbest XY taşıma
                Vector2 newPos = _draggedEntity.Transform.LocalPosition + delta;
                if (_context.SnapToGrid || io.KeyCtrl)
                {
                    float snap = _context.GridSnapSize;
                    newPos.X = MathF.Round(newPos.X / snap) * snap;
                    newPos.Y = MathF.Round(newPos.Y / snap) * snap;
                    _draggedEntity.Transform.LocalPosition = newPos;
                }
                else
                {
                    _draggedEntity.Transform.Translate(delta);
                }
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            else if (_isDraggingGizmo)
            {
                if (_context.ActiveGizmo == GizmoType.Translate)
                {
                    if (_dragAxis == 1) delta.Y = 0; // Yalnızca X
                    if (_dragAxis == 2) delta.X = 0; // Yalnızca Y

                    Vector2 newPos = _draggedEntity.Transform.LocalPosition + delta;
                    if (_context.SnapToGrid || io.KeyCtrl)
                    {
                        float snap = _context.GridSnapSize;
                        newPos.X = MathF.Round(newPos.X / snap) * snap;
                        newPos.Y = MathF.Round(newPos.Y / snap) * snap;
                        _draggedEntity.Transform.LocalPosition = newPos;
                    }
                    else
                    {
                        _draggedEntity.Transform.Translate(delta);
                    }
                    ImGui.SetMouseCursor(_dragAxis == 1 ? ImGuiMouseCursor.ResizeEW : (_dragAxis == 2 ? ImGuiMouseCursor.ResizeNS : ImGuiMouseCursor.ResizeAll));
                }
                else if (_context.ActiveGizmo == GizmoType.Rotate)
                {
                    Vector2 toMouse = worldMouse - _draggedEntity.Transform.WorldPosition;
                    float angle = MathF.Atan2(toMouse.Y, toMouse.X);
                    if (_context.SnapToGrid || io.KeyCtrl)
                    {
                        float step = MathHelper.ToRadians(15f); // 15 derece ızgara
                        angle = MathF.Round(angle / step) * step;
                    }
                    _draggedEntity.Transform.WorldRotation = angle;
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNESW);
                }
                else if (_context.ActiveGizmo == GizmoType.Scale)
                {
                    float scaleDelta = delta.X * 0.01f;
                    if (_dragAxis == 1)
                    {
                        _draggedEntity.Transform.LocalScale = new Vector2(Math.Max(0.05f, _draggedEntity.Transform.LocalScale.X + scaleDelta), _draggedEntity.Transform.LocalScale.Y);
                    }
                    else if (_dragAxis == 2)
                    {
                        float yDelta = -delta.Y * 0.01f;
                        _draggedEntity.Transform.LocalScale = new Vector2(_draggedEntity.Transform.LocalScale.X, Math.Max(0.05f, _draggedEntity.Transform.LocalScale.Y + yDelta));
                    }
                    else
                    {
                        float factor = 1.0f + scaleDelta;
                        _draggedEntity.Transform.LocalScale = Vector2.Max(new Vector2(0.05f), _draggedEntity.Transform.LocalScale * factor);
                    }
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                }
            }

            _lastDragWorldPos = worldMouse;
            return;
        }

        // 5. SÜRÜKLEME BİTTİĞİNDE TEK BİR UNDO KAYDI OLUŞTUR (Fare bırakıldığında)
        if ((_isDraggingGizmo || _isDraggingBody) && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (_draggedEntity != null && !_draggedEntity.IsDestroyed)
            {
                bool changed = (_draggedEntity.Transform.LocalPosition != _dragStartPos) ||
                               (MathF.Abs(_draggedEntity.Transform.LocalRotation - _dragStartRot) > 0.001f) ||
                               (_draggedEntity.Transform.LocalScale != _dragStartScale);

                if (changed)
                {
                    var cmd = new Commands.TransformCommand(
                        _draggedEntity,
                        _dragStartPos, _dragStartRot, _dragStartScale,
                        _draggedEntity.Transform.LocalPosition, _draggedEntity.Transform.LocalRotation, _draggedEntity.Transform.LocalScale);
                    _context.RecordCommand(cmd);
                }
            }

            _isDraggingGizmo = false;
            _isDraggingBody = false;
            _dragAxis = 0;
            _draggedEntity = null;
        }

        // 6. BOŞTA İKEN FARE İMLECİ VURGULARI (Hover Cursors)
        if (isCanvasHovered && !_isDraggingGizmo && !_isDraggingBody && !_isPanningCamera)
        {
            if (_context.SelectedEntity != null && !_context.SelectedEntity.IsDestroyed)
            {
                var pos = _context.SelectedEntity.Transform.WorldPosition;
                float handleRadius = 16f / scene.Camera.Zoom;
                float armLen = 60f / scene.Camera.Zoom;

                if (Vector2.Distance(worldMouse, pos) <= handleRadius)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                }
                else if (Vector2.Distance(worldMouse, pos + new Vector2(armLen, 0)) <= handleRadius)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
                }
                else if (Vector2.Distance(worldMouse, pos + new Vector2(0, -armLen)) <= handleRadius)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
                }
                else if (GetEntityWorldBounds(_context.SelectedEntity).Contains((int)worldMouse.X, (int)worldMouse.Y))
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
            }
        }
    }

    public static Rectangle GetEntityWorldBounds(IEntity entity)
    {
        var pos = entity.Transform.WorldPosition;
        var scale = entity.Transform.WorldScale;

        var sr = entity.GetComponent<SpriteRenderer>();
        if (sr != null && sr.Texture != null)
        {
            int w = Math.Max((int)(sr.Texture.Width * MathF.Abs(scale.X)), 16);
            int h = Math.Max((int)(sr.Texture.Height * MathF.Abs(scale.Y)), 16);
            return new Rectangle((int)pos.X - w / 2, (int)pos.Y - h / 2, w, h);
        }

        var box = entity.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            return box.GetAABB();
        }

        var circle = entity.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            int r = (int)circle.ScaledRadius;
            return new Rectangle((int)circle.Center.X - r, (int)circle.Center.Y - r, r * 2, r * 2);
        }

        int defaultSize = (int)(36 * MathF.Max(scale.X, scale.Y));
        return new Rectangle((int)pos.X - defaultSize / 2, (int)pos.Y - defaultSize / 2, defaultSize, defaultSize);
    }

    public void Dispose()
    {
        if (_renderTargetTextureId != IntPtr.Zero)
        {
            _imGuiRenderer.UnbindTexture(_renderTargetTextureId);
        }
        _renderTarget?.Dispose();
    }
}
