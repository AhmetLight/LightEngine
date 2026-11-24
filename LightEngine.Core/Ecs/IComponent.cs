using LightEngine.Core.Rendering;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Entity'lere bağlanarak veri ve davranış ekleyen bileşen (Component) arayüzü.
/// İleride ECS (Entity Component System) yapısına evrilebilecek şekilde saf arayüz olarak tasarlanmıştır.
/// </summary>
public interface IComponent
{
    /// <summary>
    /// Bileşenin bağlı olduğu varlık (Entity).
    /// </summary>
    IEntity? Entity { get; }

    /// <summary>
    /// Bileşenin etkin olup olmadığı. Devre dışıyken Update ve Draw çağrılmaz.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Bileşeni bir Entity'ye bağlar.
    /// </summary>
    void Attach(IEntity entity);

    /// <summary>
    /// Bileşeni bağlı olduğu Entity'den ayırır.
    /// </summary>
    void Detach();

    /// <summary>
    /// Bileşen ilk oluşturulduğunda veya sahneye eklendiğinde çağrılır.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Her karede (frame) değişken zaman adımı ile çağrılır.
    /// </summary>
    /// <param name="deltaTime">Son kareden bu yana geçen süre (saniye).</param>
    void Update(float deltaTime);

    /// <summary>
    /// Sabit zaman adımı ile fizik ve mantık güncellemeleri için çağrılır.
    /// </summary>
    /// <param name="fixedDeltaTime">Sabit zaman adımı süresi (saniye).</param>
    void FixedUpdate(float fixedDeltaTime);

    /// <summary>
    /// 2D çizim aşamasında çağrılır.
    /// </summary>
    /// <param name="renderer">2D SpriteBatch çizim aracı.</param>
    void Draw(SpriteBatchRenderer renderer);

    /// <summary>
    /// Bileşen veya Entity yok edilirken kaynakları temizlemek için çağrılır.
    /// </summary>
    void Destroy();
}
