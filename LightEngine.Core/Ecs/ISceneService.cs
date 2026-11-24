using LightEngine.Core.Rendering;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Sahneler arası geçiş ve sahne yaşam döngüsünü yöneten servis arayüzü.
/// </summary>
public interface ISceneService
{
    /// <summary>
    /// Şu anda çalışan aktif sahne.
    /// </summary>
    IScene? ActiveScene { get; }

    /// <summary>
    /// İsme bağlı sahne oluşturucu fonksiyon kaydeder.
    /// </summary>
    void RegisterScene(string name, Func<IScene> sceneFactory);

    /// <summary>
    /// Kayıtlı adı verilen sahneye geçiş yapar.
    /// </summary>
    void LoadScene(string name);

    /// <summary>
    /// Belirtilen sahne örneğini doğrudan aktif sahne yapar.
    /// </summary>
    void LoadScene(IScene scene);

    /// <summary>
    /// Aktif sahneyi değişken zaman adımı ile günceller.
    /// </summary>
    void Update(float deltaTime);

    /// <summary>
    /// Aktif sahneyi sabit zaman adımı ile günceller.
    /// </summary>
    void FixedUpdate(float fixedDeltaTime);

    /// <summary>
    /// Aktif sahneyi çizer.
    /// </summary>
    void Draw(SpriteBatchRenderer renderer);
}
