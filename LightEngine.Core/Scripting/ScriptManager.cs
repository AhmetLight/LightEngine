using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using LightEngine.Core.Ecs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LightEngine.Core.Scripting;

/// <summary>
/// Roslyn (Microsoft.CodeAnalysis.CSharp) ve Collectible AssemblyLoadContext kullanarak
/// çalışma zamanında C# betiklerini derleyen ve Hot Reload (canlı yenileme) sağlayan yönetici sınıf.
/// </summary>
public class ScriptManager : IScriptService
{
    private CollectibleScriptLoadContext? _loadContext;
    private Assembly? _compiledAssembly;
    private FileSystemWatcher? _watcher;

    private readonly Dictionary<string, Type> _scriptTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _availableNames = new();
    private readonly List<string> _lastDiagnostics = new();

    private DateTime _lastWatchTrigger = DateTime.MinValue;
    public bool HasPendingHotReload { get; private set; }

    public string ScriptsDirectory { get; private set; } = "";
    public IReadOnlyList<string> AvailableScriptNames => _availableNames;
    public IReadOnlyList<string> LastDiagnostics => _lastDiagnostics;

    public event Action<int>? OnScriptsReloaded;
    public event Action<IReadOnlyList<string>>? OnCompileError;

    public void Initialize(string scriptsDirectory)
    {
        ScriptsDirectory = scriptsDirectory;
        if (!Directory.Exists(ScriptsDirectory))
        {
            Directory.CreateDirectory(ScriptsDirectory);
        }

        // İlk açılışta örnek bir dönen 3D/2D betik şablonu yoksa oluştur
        string samplePath = Path.Combine(ScriptsDirectory, "AutoRotator.cs");
        if (!File.Exists(samplePath))
        {
            File.WriteAllText(samplePath, GetSampleRotatorCode());
        }

        SetupWatcher();
        ReloadScripts();
    }

    private void SetupWatcher()
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(ScriptsDirectory, "*.cs")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        void Trigger(object sender, FileSystemEventArgs e)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastWatchTrigger).TotalMilliseconds < 350) return;
            _lastWatchTrigger = now;
            HasPendingHotReload = true;
        }

        _watcher.Changed += Trigger;
        _watcher.Created += Trigger;
        _watcher.Deleted += Trigger;
        _watcher.Renamed += (s, e) => Trigger(s, e);
    }

    /// <summary>
    /// Dosya izleyicisinden gelen bekleyen Hot Reload isteğini ana iş parçacığında güvenle işler.
    /// </summary>
    public bool ProcessPendingHotReload(IScene? activeScene)
    {
        if (!HasPendingHotReload) return false;
        HasPendingHotReload = false;
        return ReloadScripts(activeScene);
    }

    public bool ReloadScripts(IScene? activeScene = null)
    {
        _lastDiagnostics.Clear();

        if (string.IsNullOrEmpty(ScriptsDirectory) || !Directory.Exists(ScriptsDirectory))
        {
            return false;
        }

        string[] csFiles = Directory.GetFiles(ScriptsDirectory, "*.cs", SearchOption.AllDirectories);
        if (csFiles.Length == 0)
        {
            _scriptTypes.Clear();
            _availableNames.Clear();
            return true;
        }

        var syntaxTrees = new List<SyntaxTree>(csFiles.Length);
        foreach (string file in csFiles)
        {
            try
            {
                string code = File.ReadAllText(file);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(code, path: file));
            }
            catch (IOException)
            {
                // Dosya editör tarafından henüz yazılıyor olabilir
                return false;
            }
        }

        var references = CollectMetadataReferences();
        string assemblyName = $"LightEngine.UserScripts_{Guid.NewGuid():N}";

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Debug));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        if (!emitResult.Success)
        {
            foreach (var diag in emitResult.Diagnostics)
            {
                if (diag.Severity == DiagnosticSeverity.Error)
                {
                    _lastDiagnostics.Add(diag.ToString());
                }
            }
            OnCompileError?.Invoke(_lastDiagnostics);
            return false;
        }

        // Eski collectible context'i serbest bırak
        var oldContext = _loadContext;
        peStream.Position = 0;

        var newContext = new CollectibleScriptLoadContext();
        Assembly newAssembly = newContext.LoadFromStream(peStream);

        _scriptTypes.Clear();
        _availableNames.Clear();

        foreach (Type type in newAssembly.GetExportedTypes())
        {
            if (!type.IsAbstract && typeof(ScriptComponent).IsAssignableFrom(type))
            {
                _scriptTypes[type.Name] = type;
                _availableNames.Add(type.Name);
            }
        }

        _loadContext = newContext;
        _compiledAssembly = newAssembly;

        // Aktif sahnedeki mevcut betik bileşenlerini değerlerini koruyarak canlı değiştir (Hot Reload)
        if (activeScene != null)
        {
            HotSwapSceneScripts(activeScene);
        }

        oldContext?.Unload();
        OnScriptsReloaded?.Invoke(_availableNames.Count);
        return true;
    }

    private void HotSwapSceneScripts(IScene scene)
    {
        var entities = scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (entity.IsDestroyed) continue;

            var oldScripts = entity.Components.OfType<ScriptComponent>().ToList();
            foreach (var oldScript in oldScripts)
            {
                string typeName = oldScript.GetType().Name;
                if (!_scriptTypes.TryGetValue(typeName, out Type? newType)) continue;

                // Eski bileşenin public alan ve özellik değerlerini kopyala
                var savedValues = CaptureSerializableState(oldScript);
                bool wasEnabled = oldScript.IsEnabled;

                entity.RemoveComponent(oldScript);

                if (Activator.CreateInstance(newType) is ScriptComponent newScript)
                {
                    newScript.IsEnabled = wasEnabled;
                    RestoreSerializableState(newScript, savedValues);
                    entity.AddComponent(newScript);
                }
            }
        }
    }

    public ScriptComponent? CreateScriptInstance(string scriptTypeName)
    {
        if (_scriptTypes.TryGetValue(scriptTypeName, out Type? type))
        {
            return Activator.CreateInstance(type) as ScriptComponent;
        }
        return null;
    }

    public string CreateScriptTemplate(string scriptName)
    {
        string safeName = new string(scriptName.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrEmpty(safeName) || char.IsDigit(safeName[0]))
        {
            safeName = "NewScript";
        }

        string filePath = Path.Combine(ScriptsDirectory, $"{safeName}.cs");
        string template = $@"using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;

namespace GameScripts;

/// <summary>
/// {safeName} özel oyun bileşeni.
/// </summary>
public class {safeName} : ScriptComponent
{{
    public float Speed = 45f;

    public override void OnStart()
    {{
        // Başlangıç mantığı
    }}

    public override void OnUpdate(float deltaTime)
    {{
        if (Transform3D != null)
        {{
            Transform3D.Rotate(new Vector3(0f, Speed * deltaTime, 0f));
        }}
        else if (Entity != null)
        {{
            Entity.Transform.LocalRotation += MathHelper.ToRadians(Speed) * deltaTime;
        }}
    }}
}}
";
        File.WriteAllText(filePath, template);
        return filePath;
    }

    private static unsafe List<MetadataReference> CollectMetadataReferences()
    {
        var refs = new List<MetadataReference>();
        var addedSimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. AppDomain'de yüklü olan tüm assembly'lerin bellek görüntüsünden metadata referansı al
        // (Single-File publish modunda bile %100 çalışır, diskte DLL olmasına gerek kalmaz)
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.IsDynamic) continue;
            string? name = asm.GetName().Name;
            if (string.IsNullOrEmpty(name) || !addedSimpleNames.Add(name)) continue;

            try
            {
                if (System.Reflection.Metadata.AssemblyExtensions.TryGetRawMetadata(asm, out byte* blob, out int length))
                {
                    var mod = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
                    var asmMeta = AssemblyMetadata.Create(mod);
                    refs.Add(asmMeta.GetReference());
                    continue;
                }
            }
            catch { }

            // Bellek raw metadata alınamadıysa dosya yolunu dene
            string? loc = null;
            try { loc = asm.Location; } catch { }
            if (!string.IsNullOrEmpty(loc) && File.Exists(loc) && addedPaths.Add(loc))
            {
                if (IsManagedAssembly(loc))
                {
                    try { refs.Add(MetadataReference.CreateFromFile(loc)); } catch { }
                }
            }
        }

        // 2. Base directory'de sadece ve sadece yönetilen (.NET managed) DLL'leri tara
        // (SDL2.dll, cimgui.dll, openal.dll gibi C/C++ native PE dosyaları kesinlikle filtrelenir!)
        string baseDir = AppContext.BaseDirectory;
        if (Directory.Exists(baseDir))
        {
            foreach (var dll in Directory.GetFiles(baseDir, "*.dll"))
            {
                string fn = Path.GetFileNameWithoutExtension(dll);
                if (addedSimpleNames.Contains(fn) || !addedPaths.Add(dll)) continue;

                if (IsManagedAssembly(dll))
                {
                    try
                    {
                        refs.Add(MetadataReference.CreateFromFile(dll));
                        addedSimpleNames.Add(fn);
                    }
                    catch { }
                }
            }
        }

        // 3. .NET Runtime ana sistem kütüphaneleri (fallback)
        string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        if (Directory.Exists(runtimeDir))
        {
            string[] coreDlls = ["System.Runtime.dll", "System.Collections.dll", "System.Private.CoreLib.dll", "netstandard.dll", "mscorlib.dll"];
            foreach (var coreDll in coreDlls)
            {
                string fn = Path.GetFileNameWithoutExtension(coreDll);
                if (addedSimpleNames.Contains(fn)) continue;

                string p = Path.Combine(runtimeDir, coreDll);
                if (File.Exists(p) && addedPaths.Add(p) && IsManagedAssembly(p))
                {
                    try
                    {
                        refs.Add(MetadataReference.CreateFromFile(p));
                        addedSimpleNames.Add(fn);
                    }
                    catch { }
                }
            }
        }

        return refs;
    }

    private static bool IsManagedAssembly(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var peReader = new PEReader(stream);
            return peReader.HasMetadata;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, object?> CaptureSerializableState(ScriptComponent script)
    {
        var dict = new Dictionary<string, object?>();
        Type t = script.GetType();
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            dict[$"F:{f.Name}"] = f.GetValue(script);
        }
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.CanRead && p.CanWrite && p.DeclaringType == t)
            {
                dict[$"P:{p.Name}"] = p.GetValue(script);
            }
        }
        return dict;
    }

    private static void RestoreSerializableState(ScriptComponent script, Dictionary<string, object?> state)
    {
        Type t = script.GetType();
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (state.TryGetValue($"F:{f.Name}", out object? val) && val != null && f.FieldType.IsAssignableFrom(val.GetType()))
            {
                f.SetValue(script, val);
            }
        }
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.CanRead && p.CanWrite && p.DeclaringType == t &&
                state.TryGetValue($"P:{p.Name}", out object? val) && val != null && p.PropertyType.IsAssignableFrom(val.GetType()))
            {
                p.SetValue(script, val);
            }
        }
    }

    private static string GetSampleRotatorCode() => @"using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;

namespace GameScripts;

/// <summary>
/// Bağlı olduğu 3D veya 2D nesneyi sürekli döndüren örnek C# betiği.
/// Bu dosyayı düzenleyip kaydettiğinizde Hot Reload ile otomatik yenilenir.
/// </summary>
public class AutoRotator : ScriptComponent
{
    public float RotationSpeed = 55f;
    public float BobAmplitude = 0.25f;
    public float BobSpeed = 2.0f;

    private float _elapsed;
    private float _startY;

    public override void OnStart()
    {
        if (Transform3D != null)
        {
            _startY = Transform3D.LocalPosition.Y;
        }
    }

    public override void OnUpdate(float deltaTime)
    {
        _elapsed += deltaTime;

        if (Transform3D != null)
        {
            Transform3D.Rotate(new Vector3(0f, RotationSpeed * deltaTime, 0f));
            var pos = Transform3D.LocalPosition;
            pos.Y = _startY + System.MathF.Sin(_elapsed * BobSpeed) * BobAmplitude;
            Transform3D.LocalPosition = pos;
        }
        else if (Entity != null)
        {
            Entity.Transform.LocalRotation += MathHelper.ToRadians(RotationSpeed) * deltaTime;
        }
    }
}
";

    public void Dispose()
    {
        _watcher?.Dispose();
        _loadContext?.Unload();
        _compiledAssembly = null;
    }

    private sealed class CollectibleScriptLoadContext : AssemblyLoadContext
    {
        public CollectibleScriptLoadContext() : base(isCollectible: true) { }
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
