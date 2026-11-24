using ImGuiNET;
using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering;
using Microsoft.Xna.Framework;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor.Panels;

/// <summary>
/// Sahnedeki tüm Entity'leri listeleyen, seçmeyi, yeni nesne eklemeyi ve silmeyi sağlayan Hiyerarşi Paneli.
/// </summary>
public class HierarchyPanel
{
    private readonly EditorContext _context;
    private string _searchFilter = "";
    private Guid? _draggedEntityId;

    public HierarchyPanel(EditorContext context)

    {
        _context = context;
    }

    public void Draw()
    {
        if (ImGui.Begin("Scene Hierarchy##HierarchyPanel"))
        {
            var scene = _context.ActiveScene;
            if (scene == null)
            {
                ImGui.TextDisabled("No active scene.");
                ImGui.End();
                return;
            }

            // Üst toolbar: Arama ve Ekle butonu
            ImGui.InputTextWithHint("##SearchEntities", "Search...", ref _searchFilter, 64);
            ImGui.SameLine();

            if (ImGui.Button("+ Add Entity"))
            {
                ImGui.OpenPopup("HierarchyAddPopup");
            }

            if (ImGui.BeginPopup("HierarchyAddPopup"))
            {
                if (ImGui.MenuItem("Empty Entity"))
                {
                    var entity = scene.CreateEntity("New_Entity");
                    _context.SelectedEntity = entity;
                    _context.Log($"Created Entity: {entity.Name}");
                }

                if (ImGui.MenuItem("2D Sprite"))
                {
                    var entity = scene.CreateEntity("Sprite_Object");
                    var assetService = Engine.Services.Get<IAssetService>();
                    var tex = assetService.CreateSolidTexture(48, 48, Color.CornflowerBlue, "editor_rect");
                    entity.AddComponent(new SpriteRenderer(tex, centerOrigin: true));
                    _context.SelectedEntity = entity;
                    _context.Log($"Created 2D Sprite: {entity.Name}");
                }

                if (ImGui.MenuItem("2D Circle"))
                {
                    var entity = scene.CreateEntity("Circle_Object");
                    var assetService = Engine.Services.Get<IAssetService>();
                    var tex = assetService.CreateCircleTexture(24, Color.IndianRed, "editor_circle");
                    entity.AddComponent(new SpriteRenderer(tex, centerOrigin: true));
                    _context.SelectedEntity = entity;
                    _context.Log($"Created Circle: {entity.Name}");
                }

                ImGui.Separator();

                if (ImGui.BeginMenu("3D Object"))
                {
                    DrawCreate3DMenuItems();
                    ImGui.EndMenu();
                }

                ImGui.EndPopup();
            }

            ImGui.Separator();

            // Sahne başlığı
            ImGui.TextColored(new NumVec4(0.4f, 0.8f, 1f, 1f), $"Scene: {scene.Name} ({scene.Entities.Count} Entities)");
            ImGui.Spacing();

            // Varlık listesi
            ImGui.BeginChild("EntityList", new NumVec2(0, 0), ImGuiChildFlags.None);

            for (int i = 0; i < scene.Entities.Count; i++)
            {
                var entity = scene.Entities[i];
                if (entity.IsDestroyed) continue;

                // Filtreleme kontrolü
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !entity.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isSelected = _context.SelectedEntity == entity;

                // 1. Aktiflik / Görünürlük kutucuğu
                bool isActive = entity.IsActive;
                if (ImGui.Checkbox($"##Active_{entity.Id}", ref isActive))
                {
                    entity.IsActive = isActive;
                }
                ImGui.SameLine();

                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;

                string label = $"{entity.Name}  [{entity.Tag}]";
                bool opened = ImGui.TreeNodeEx($"##Entity_{entity.Id}", flags, label);

                // Sürükle-bırak (Drag & Drop) ile ebeveyn-çocuk ilişkisi
                if (ImGui.BeginDragDropSource())
                {
                    _draggedEntityId = entity.Id;
                    ImGui.SetDragDropPayload("HIERARCHY_ENTITY", IntPtr.Zero, 0);
                    ImGui.Text($"Move: {entity.Name}");
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload("HIERARCHY_ENTITY");
                    unsafe
                    {
                        if (payload.NativePtr != null && _draggedEntityId.HasValue && _draggedEntityId.Value != entity.Id)
                        {
                            var dragged = scene.Entities.FirstOrDefault(e => e.Id == _draggedEntityId.Value);
                            if (dragged != null)
                            {
                                var t3dDragged = dragged.GetComponent<LightEngine.Core.Rendering3D.Transform3D>();
                                var t3dTarget = entity.GetComponent<LightEngine.Core.Rendering3D.Transform3D>();
                                if (t3dDragged != null && t3dTarget != null)
                                {
                                    t3dDragged.Parent = t3dTarget;
                                    _context.Log($"Parented '{dragged.Name}' to '{entity.Name}' (3D)", LogLevel.Info);
                                }
                                else
                                {
                                    dragged.Transform.SetParent(entity.Transform);
                                    _context.Log($"Parented '{dragged.Name}' to '{entity.Name}' (2D)", LogLevel.Info);
                                }
                            }
                            _draggedEntityId = null;
                        }
                    }
                    ImGui.EndDragDropTarget();
                }

                // Tek tık: Seç
                if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                {
                    _context.SelectedEntity = entity;
                }

                // Çift tık: Kamerayı nesneye odakla (2D veya 3D)
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    FocusCameraOnEntity(scene, entity);
                }

                // Sağ tık menüsü (Context Menu)
                if (ImGui.BeginPopupContextItem($"EntityContext_{entity.Id}"))
                {
                    _context.SelectedEntity = entity;

                    if (ImGui.MenuItem("Focus Camera", "F"))
                    {
                        FocusCameraOnEntity(scene, entity);
                    }

                    if (ImGui.MenuItem("Duplicate", "Ctrl+D"))
                    {
                        _context.DuplicateEntity(entity);
                    }

                    var t3d = entity.GetComponent<LightEngine.Core.Rendering3D.Transform3D>();
                    if (entity.Transform.Parent != null || t3d?.Parent != null)
                    {
                        if (ImGui.MenuItem("Unparent (Set to Root)"))
                        {
                            entity.Transform.SetParent(null);
                            if (t3d != null) t3d.Parent = null;
                            _context.Log($"Unparented {entity.Name}", LogLevel.Info);
                        }
                    }

                    ImGui.Separator();

                    if (ImGui.MenuItem("Delete", "Delete"))
                    {
                        entity.Destroy();
                        if (_context.SelectedEntity == entity) _context.SelectedEntity = null;
                        _context.Log($"Deleted: {entity.Name}", LogLevel.Warning);
                    }

                    ImGui.EndPopup();
                }

                if (opened)
                {
                    ImGui.TreePop();
                }
            }

            // Boş alana sağ tıklandığında yeni varlık oluşturma menüsü
            if (ImGui.BeginPopupContextWindow("HierarchyEmptyAreaContext", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
            {
                if (ImGui.MenuItem("Create Empty Entity"))
                {
                    var e = scene.CreateEntity("New_Entity");
                    _context.SelectedEntity = e;
                }
                if (ImGui.MenuItem("Create 2D Sprite"))
                {
                    var e = scene.CreateEntity("Sprite_Object");
                    var assetService = Engine.Services.Get<IAssetService>();
                    var tex = assetService.CreateSolidTexture(48, 48, Color.CornflowerBlue, "editor_rect");
                    e.AddComponent(new SpriteRenderer(tex, centerOrigin: true));
                    _context.SelectedEntity = e;
                }
                if (ImGui.MenuItem("Create 2D Circle"))
                {
                    var e = scene.CreateEntity("Circle_Object");
                    var assetService = Engine.Services.Get<IAssetService>();
                    var tex = assetService.CreateCircleTexture(24, Color.IndianRed, "editor_circle");
                    e.AddComponent(new SpriteRenderer(tex, centerOrigin: true));
                    _context.SelectedEntity = e;
                }

                ImGui.Separator();

                if (ImGui.BeginMenu("3D Object"))
                {
                    DrawCreate3DMenuItems();
                    ImGui.EndMenu();
                }

                ImGui.EndPopup();
            }

            ImGui.EndChild();
        }
        ImGui.End();
    }

    private void FocusCameraOnEntity(IScene scene, IEntity entity)
    {
        var t3d = entity.GetComponent<LightEngine.Core.Rendering3D.Transform3D>();
        if (t3d != null)
        {
            _context.Is3DMode = true;
            scene.Camera3D.FocusOn(t3d.WorldPosition, 6f);
        }
        else
        {
            _context.Is3DMode = false;
            scene.Camera.Position = entity.Transform.WorldPosition;
        }
        _context.Log($"Camera focused on: {entity.Name}", LogLevel.Info);
    }

    private void DrawCreate3DMenuItems()
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
    }
}

