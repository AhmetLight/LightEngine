using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Oyun dünyasını, varlıkları (Entity) ve katmanları barındıran sahne arayüzü.
/// </summary>
public interface IScene
{
    /// <summary>
    /// Sahnenin tanımlayıcı adı.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sahnede yer alan tüm varlıkların listesi.
    /// </summary>
    IReadOnlyList<IEntity> Entities { get; }

    /// <summary>
    /// Sahneye ait kamera (2D görünüm matrisi ve takibi için).
    /// </summary>
    Camera2D Camera { get; }

    /// <summary>
    /// Sahneye ait 3D perspektif kamera.
    /// </summary>
    Camera3D Camera3D { get; }

    /// <summary>
    /// Yeni bir Entity üretip sahneye ekler.
    /// </summary>
    IEntity CreateEntity(string name = "Entity");

    /// <summary>
    /// Mevcut bir Entity'yi sahneye dahil eder.
    /// </summary>
    void AddEntity(IEntity entity);

    /// <summary>
    /// Belirtilen Entity'yi sahneden çıkarır.
    /// </summary>
    bool RemoveEntity(IEntity entity);

    /// <summary>
    /// Ada göre ilk eşleşen varlığı bulur.
    /// </summary>
    IEntity? FindEntityByName(string name);

    /// <summary>
    /// Etikete (Tag) göre eşleşen tüm varlıkları getirir.
    /// </summary>
    IEnumerable<IEntity> FindEntitiesByTag(string tag);

    /// <summary>
    /// Sahne başlatıldığında çağrılır.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Sahneye özel grafik ve ses kaynaklarını yükler.
    /// </summary>
    void LoadContent();

    /// <summary>
    /// Sahne kapatılırken kaynakları boşaltır.
    /// </summary>
    void UnloadContent();

    /// <summary>
    /// Sahnedeki tüm varlıkları değişken zaman adımı ile günceller.
    /// </summary>
    void Update(float deltaTime);

    /// <summary>
    /// Sahnedeki tüm varlıkları sabit zaman adımı ile günceller.
    /// </summary>
    void FixedUpdate(float fixedDeltaTime);

    /// <summary>
    /// Sahnedeki varlıkları 2D olarak çizer.
    /// </summary>
    void Draw(SpriteBatchRenderer renderer);

    /// <summary>
    /// Sahnedeki tüm varlıkları temizler.
    /// </summary>
    void Clear();
}
