using ImGuiNET;
using NumVec2 = System.Numerics.Vector2;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor.Panels;

/// <summary>
/// Oyun içi dokuları (Texture), modelleri (glTF/FBX), sesleri ve sahneleri
/// yönetmeyi ve içe aktarmayı sağlayan Varlık Tarayıcısı (Asset Browser).
/// </summary>
public class AssetBrowserPanel
{
    private readonly EditorContext _context;
    private string _currentDirectory;
    private string _filterText = "";
    private string _selectedFilterType = "All";

    public AssetBrowserPanel(EditorContext context)
    {
        _context = context;
        _currentDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
        if (!Directory.Exists(_currentDirectory))
        {
            Directory.CreateDirectory(_currentDirectory);
            Directory.CreateDirectory(Path.Combine(_currentDirectory, "Textures"));
            Directory.CreateDirectory(Path.Combine(_currentDirectory, "Models"));
            Directory.CreateDirectory(Path.Combine(_currentDirectory, "Audio"));
            Directory.CreateDirectory(Path.Combine(_currentDirectory, "Scenes"));
            Directory.CreateDirectory(Path.Combine(_currentDirectory, "Scripts"));
        }
    }

    public void Draw()
    {
        if (ImGui.Begin("Asset Browser##AssetBrowserPanel"))
        {
            DrawContent();
        }
        ImGui.End();
    }

    public void DrawContent()
    {
        // Üst Çubuk: Geri butonu, Geçerli Yol ve İçe Aktar
        if (ImGui.Button("< Back"))
        {
            var parent = Directory.GetParent(_currentDirectory);
            if (parent != null && parent.Exists)
            {
                _currentDirectory = parent.FullName;
            }
        }

        ImGui.SameLine();
        ImGui.TextColored(new NumVec4(0.8f, 0.85f, 0.9f, 1f), Path.GetFileName(_currentDirectory));

        ImGui.SameLine();
        ImGui.InputTextWithHint("##AssetFilter", "Filter...", ref _filterText, 64);

        ImGui.SameLine();
        if (ImGui.Button("+ Import Asset"))
        {
            _context.Log("Import Asset dialog requested.", LogLevel.Info);
        }

        // Kategori filtre butonları
        string[] categories = { "All", "Textures (.png)", "Models (.gltf)", "Audio (.wav)", "Scenes (.json)" };
        for (int i = 0; i < categories.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            bool isSelected = _selectedFilterType == categories[i];
            if (isSelected) ImGui.PushStyleColor(ImGuiCol.Button, new NumVec4(0.2f, 0.55f, 0.9f, 1f));

            if (ImGui.SmallButton(categories[i]))
            {
                _selectedFilterType = categories[i];
            }

            if (isSelected) ImGui.PopStyleColor();
        }

        ImGui.Separator();

        // Dosya ve Klasör Listesi Grid / Tablo Görünümü
        if (ImGui.BeginTable("AssetTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableHeadersRow();

            // 1. Alt Klasörler
            if (Directory.Exists(_currentDirectory))
            {
                var dirs = Directory.GetDirectories(_currentDirectory);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dirName = Path.GetFileName(dirs[i]);
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    // Klasöre çift tıklayınca içine gir
                    if (ImGui.Selectable($"[Folder] {dirName}", false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                    }
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        _currentDirectory = dirs[i];
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextDisabled("Directory");
                    ImGui.TableSetColumnIndex(2);
                    ImGui.TextDisabled("-");
                }

                // 2. Dosyalar
                var files = Directory.GetFiles(_currentDirectory);
                for (int i = 0; i < files.Length; i++)
                {
                    string filePath = files[i];
                    string fileName = Path.GetFileName(filePath);
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();

                    if (!string.IsNullOrEmpty(_filterText) &&
                        !fileName.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    bool isSelected = _context.SelectedAssetPath == filePath;
                    string iconTag = GetIconTag(ext);

                    if (ImGui.Selectable($"{iconTag} {fileName}", isSelected, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        _context.SelectedAssetPath = filePath;
                        _context.Log($"Asset selected: {fileName}");
                    }

                    // Sürükle-Bırak Kaynağı (Viewport'a model veya resim bırakma)
                    if (ImGui.BeginDragDropSource())
                    {
                        _context.SelectedAssetPath = filePath;
                        if (ext == ".obj" || ext == ".gltf" || ext == ".glb")
                        {
                            ImGui.SetDragDropPayload("ASSET_MODEL_PATH", IntPtr.Zero, 0);
                            ImGui.Text($"[3D Model] {fileName}");
                        }
                        else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            ImGui.SetDragDropPayload("ASSET_TEXTURE_PATH", IntPtr.Zero, 0);
                            ImGui.Text($"[Texture] {fileName}");
                        }
                        ImGui.EndDragDropSource();
                    }

                    // Çift tıklama aksiyonu
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        OpenAsset(filePath, ext);
                    }

                    // Sağ tık menüsü
                    if (ImGui.BeginPopupContextItem($"AssetContext_{i}"))
                    {
                        _context.SelectedAssetPath = filePath;

                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            if (ImGui.MenuItem("Instantiate Sprite in Scene"))
                            {
                                InstantiateSprite(filePath);
                            }
                        }
                        else if (ext == ".scene" || ext == ".json")
                        {
                            if (ImGui.MenuItem("Load Scene in Editor"))
                            {
                                LoadSceneAsset(filePath);
                            }
                        }
                        else if (ext == ".obj" || ext == ".gltf" || ext == ".glb")
                        {
                            if (ImGui.MenuItem("Instantiate 3D Model in Scene"))
                            {
                                InstantiateModel3D(filePath);
                            }
                        }
                        else if (ext == ".cs")
                        {
                            if (ImGui.MenuItem("Attach Script to Selected Entity"))
                            {
                                AttachScriptAsset(filePath);
                            }
                        }

                        if (ImGui.MenuItem("Show in File Explorer"))
                        {
                            ShowInExplorer(filePath);
                        }

                        ImGui.Separator();

                        if (ImGui.MenuItem("Delete"))
                        {
                            try
                            {
                                File.Delete(filePath);
                                _context.Log($"Deleted file: {fileName}");
                            }
                            catch (Exception ex)
                            {
                                _context.Log($"Delete error: {ex.Message}", LogLevel.Error);
                            }
                        }

                        ImGui.EndPopup();
                    }

                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(GetTypeName(ext));

                    ImGui.TableSetColumnIndex(2);
                    var fileInfo = new FileInfo(filePath);
                    ImGui.Text($"{fileInfo.Length / 1024f:0.0} KB");
                }
            }

            ImGui.EndTable();
        }
    }

    private void OpenAsset(string filePath, string ext)
    {
        if (ext == ".scene" || ext == ".json")
        {
            LoadSceneAsset(filePath);
        }
        else if (ext == ".png" || ext == ".jpg")
        {
            InstantiateSprite(filePath);
        }
        else if (ext == ".obj" || ext == ".gltf" || ext == ".glb")
        {
            InstantiateModel3D(filePath);
        }
        else if (ext == ".cs")
        {
            AttachScriptAsset(filePath);
        }
        else
        {
            ShowInExplorer(filePath);
        }
    }

    private void InstantiateModel3D(string filePath)
    {
        if (_context.ActiveScene == null) return;
        try
        {
            var r3d = LightEngine.Core.Common.Engine.Services.Get<LightEngine.Core.Rendering3D.IRender3DService>();
            var mesh = r3d.LoadModel(filePath);
            var entity = _context.ActiveScene.CreateEntity(Path.GetFileNameWithoutExtension(filePath));
            entity.Tag = "3DModel";
            entity.AddComponent(new LightEngine.Core.Rendering3D.Transform3D());
            entity.AddComponent(new LightEngine.Core.Rendering3D.MeshRenderer3D(mesh));
            _context.Is3DMode = true;
            _context.SelectedEntity = entity;
            _context.Log($"Loaded 3D model '{entity.Name}' ({mesh.PrimitiveCount} triangles).", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _context.Log($"3D model load error: {ex.Message}", LogLevel.Error);
        }
    }

    private void AttachScriptAsset(string filePath)
    {
        if (_context.SelectedEntity == null)
        {
            _context.Log("Select an entity first to attach a C# script.", LogLevel.Warning);
            return;
        }
        try
        {
            var scriptSvc = LightEngine.Core.Common.Engine.Services.Get<LightEngine.Core.Scripting.IScriptService>();
            string scriptName = Path.GetFileNameWithoutExtension(filePath);
            var instance = scriptSvc.CreateScriptInstance(scriptName);
            if (instance == null)
            {
                scriptSvc.ReloadScripts(_context.ActiveScene);
                instance = scriptSvc.CreateScriptInstance(scriptName);
            }
            if (instance != null)
            {
                _context.SelectedEntity.AddComponent(instance);
                _context.Log($"Attached C# script '{scriptName}' to {_context.SelectedEntity.Name}.", LogLevel.Info);
            }
            else
            {
                _context.Log($"Could not find ScriptComponent class '{scriptName}'.", LogLevel.Warning);
            }
        }
        catch (Exception ex)
        {
            _context.Log($"Script attach error: {ex.Message}", LogLevel.Error);
        }
    }

    private void InstantiateSprite(string filePath)
    {
        if (_context.ActiveScene == null) return;
        try
        {
            var assetService = LightEngine.Core.Common.Engine.Services.Get<LightEngine.Core.Assets.IAssetService>();
            var tex = assetService.LoadTexture(filePath);
            var entity = _context.ActiveScene.CreateEntity(Path.GetFileNameWithoutExtension(filePath));
            entity.AddComponent(new LightEngine.Core.Rendering.SpriteRenderer(tex, centerOrigin: true));
            _context.SelectedEntity = entity;
            _context.Log($"Instantiated sprite '{entity.Name}' from asset.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _context.Log($"Sprite instantiate error: {ex.Message}", LogLevel.Error);
        }
    }

    private void LoadSceneAsset(string filePath)
    {
        try
        {
            var assetService = LightEngine.Core.Common.Engine.Services.Get<LightEngine.Core.Assets.IAssetService>();
            var sceneService = LightEngine.Core.Common.Engine.Services.Get<LightEngine.Core.Ecs.ISceneService>();
            var scene = LightEngine.Core.Serialization.SceneSerializer.LoadFromFile(filePath, assetService);
            scene.LoadContent();
            _context.ActiveScene = scene;
            _context.SelectedEntity = null;
            sceneService.LoadScene(scene);
            _context.Log($"Loaded scene: {scene.Name}", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _context.Log($"Load scene error: {ex.Message}", LogLevel.Error);
        }
    }

    private static void ShowInExplorer(string path)
    {
        try
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                string argument = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                string dir = File.Exists(path) ? (Path.GetDirectoryName(path) ?? path) : path;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", $"\"{dir}\"") { UseShellExecute = true });
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                string argument = File.Exists(path) ? $"-R \"{path}\"" : $"\"{path}\"";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("open", argument) { UseShellExecute = true });
            }
        }
        catch { }
    }

    private static string GetIconTag(string ext) => ext switch
    {
        ".png" or ".jpg" or ".jpeg" or ".bmp" => "[IMG]",
        ".gltf" or ".glb" or ".fbx" or ".obj" => "[3D]",
        ".wav" or ".ogg" or ".mp3" => "[SND]",
        ".json" or ".scene" => "[SCN]",
        ".cs" => "[SCR]",
        _ => "[DOC]"
    };

    private static string GetTypeName(string ext) => ext switch
    {
        ".png" or ".jpg" or ".jpeg" => "Texture",
        ".gltf" or ".glb" or ".fbx" => "3D Model",
        ".wav" or ".ogg" => "Audio",
        ".json" or ".scene" => "Scene File",
        ".cs" => "C# Script",
        _ => "File"
    };
}
