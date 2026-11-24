using LightEngine.Core.Rendering;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Sahnedeki oyun nesnesi (Entity) arayüzü.
/// Bileşenleri (Component) barındırır ve yaşam döngüsünü (Lifecycle) yönetir.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Varlığın benzersiz kimliği.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Varlığın okunabilir adı.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// Varlığın etiket türü (örn: "Player", "Enemy", "Obstacle").
    /// </summary>
    string Tag { get; set; }

    /// <summary>
    /// Varlığın aktif olup olmadığı. Pasif nesneler güncellenmez ve çizilmez.
    /// </summary>
    bool IsActive { get; set; }

    /// <summary>
    /// Varlığın yok edilmek üzere işaretlenip işaretlenmediği.
    /// </summary>
    bool IsDestroyed { get; }

    /// <summary>
    /// Varlığın bağlı olduğu sahne.
    /// </summary>
    IScene? Scene { get; }

    /// <summary>
    /// Varlığın 2D konum ve dönüşüm bilgisi.
    /// </summary>
    Transform2D Transform { get; }

    /// <summary>
    /// Varlığa ekli bileşenlerin listesi.
    /// </summary>
    IReadOnlyList<IComponent> Components { get; }

    /// <summary>
    /// Varlığa yeni bir bileşen örneği oluşturup ekler.
    /// </summary>
    T AddComponent<T>() where T : Component, new();

    /// <summary>
    /// Varlığa önceden oluşturulmuş bir bileşeni ekler.
    /// </summary>
    T AddComponent<T>(T component) where T : IComponent;

    /// <summary>
    /// Belirtilen tipteki ilk bileşeni getirir.
    /// </summary>
    T? GetComponent<T>() where T : class, IComponent;

    /// <summary>
    /// Belirtilen tipteki bileşeni güvenli şekilde almaya çalışır.
    /// </summary>
    bool TryGetComponent<T>(out T? component) where T : class, IComponent;

    /// <summary>
    /// Belirtilen tipteki tüm bileşenleri listeler.
    /// </summary>
    IEnumerable<T> GetComponents<T>() where T : class, IComponent;

    /// <summary>
    /// Bileşeni varlıktan kaldırır.
    /// </summary>
    bool RemoveComponent(IComponent component);

    /// <summary>
    /// Belirtilen tipteki ilk bileşeni varlıktan kaldırır.
    /// </summary>
    bool RemoveComponent<T>() where T : class, IComponent;

    /// <summary>
    /// Varlıkta belirtilen tipte bileşen olup olmadığını kontrol eder.
    /// </summary>
    bool HasComponent<T>() where T : class, IComponent;

    /// <summary>
    /// Varlığı ve bileşenlerini başlatır.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Varlığı ve bileşenlerini değişken zaman adımı ile günceller.
    /// </summary>
    void Update(float deltaTime);

    /// <summary>
    /// Varlığı ve bileşenlerini sabit zaman adımı ile günceller.
    /// </summary>
    void FixedUpdate(float fixedDeltaTime);

    /// <summary>
    /// Varlığa ait görsel bileşenleri çizer.
    /// </summary>
    void Draw(SpriteBatchRenderer renderer);

    /// <summary>
    /// Varlığı sahneden silinmek üzere işaretler.
    /// </summary>
    void Destroy();
}
