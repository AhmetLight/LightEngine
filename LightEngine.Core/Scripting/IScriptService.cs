using LightEngine.Core.Ecs;

namespace LightEngine.Core.Scripting;

/// <summary>
/// C# betiklerini çalışma zamanında derleyen, takip eden ve Hot Reload destekleyen servis arayüzü.
/// </summary>
public interface IScriptService : IDisposable
{
    /// <summary>
    /// Betik dosyalarının izlendiği klasör yolu.
    /// </summary>
    string ScriptsDirectory { get; }

    /// <summary>
    /// Yüklü ve kullanıma hazır ScriptComponent türlerinin adları.
    /// </summary>
    IReadOnlyList<string> AvailableScriptNames { get; }

    /// <summary>
    /// Son derleme işleminde oluşan hata veya uyarı mesajları.
    /// </summary>
    IReadOnlyList<string> LastDiagnostics { get; }

    /// <summary>
    /// Betikler başarıyla yeniden derlendiğinde tetiklenir.
    /// </summary>
    event Action<int>? OnScriptsReloaded;

    /// <summary>
    /// Derleme hatası oluştuğunda tetiklenir.
    /// </summary>
    event Action<IReadOnlyList<string>>? OnCompileError;

    /// <summary>
    /// Belirtilen klasörü izlemeye başlar ve mevcut tüm .cs betiklerini derler.
    /// </summary>
    void Initialize(string scriptsDirectory);

    /// <summary>
    /// Tüm .cs betiklerini bellek içi yeni bir Collectible AssemblyLoadContext içine yeniden derler (Hot Reload).
    /// Aktif sahnedeki eski betik bileşenlerini özellik değerlerini koruyarak yeni sürümleriyle değiştirir.
    /// </summary>
    bool ReloadScripts(IScene? activeScene = null);

    /// <summary>
    /// Ada göre yeni bir ScriptComponent örneği oluşturur.
    /// </summary>
    ScriptComponent? CreateScriptInstance(string scriptTypeName);

    /// <summary>
    /// Belirtilen isimde hazır bir C# betik şablon dosyası (.cs) oluşturur.
    /// </summary>
    string CreateScriptTemplate(string scriptName);
}
