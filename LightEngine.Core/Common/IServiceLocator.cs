namespace LightEngine.Core.Common;

/// <summary>
/// Motor modülleri ve servisleri için servis bulucu (Service Locator) arayüzü.
/// Modüllerin birbirine sıkı sıkıya bağlanmasını (tight coupling) engeller.
/// </summary>
public interface IServiceLocator
{
    /// <summary>
    /// Belirtilen tipte bir servisi kaydeder.
    /// </summary>
    /// <typeparam name="T">Servis arayüzü veya sınıfı.</typeparam>
    /// <param name="service">Kaydedilecek servis örneği.</param>
    void Register<T>(T service) where T : class;

    /// <summary>
    /// Kayıtlı bir servisi getirir. Bulunamazsa istisna fırlatır.
    /// </summary>
    /// <typeparam name="T">İstenen servis tipi.</typeparam>
    /// <returns>Servis örneği.</returns>
    T Get<T>() where T : class;

    /// <summary>
    /// Kayıtlı bir servisi getirmeyi dener.
    /// </summary>
    /// <typeparam name="T">İstenen servis tipi.</typeparam>
    /// <param name="service">Bulunan servis veya null.</param>
    /// <returns>Servis bulunduysa true, aksi halde false.</returns>
    bool TryGet<T>(out T? service) where T : class;

    /// <summary>
    /// Kayıtlı bir servisi kaldırır.
    /// </summary>
    /// <typeparam name="T">Kaldırılacak servis tipi.</typeparam>
    void Unregister<T>() where T : class;

    /// <summary>
    /// Tüm kayıtlı servisleri temizler.
    /// </summary>
    void Clear();
}
