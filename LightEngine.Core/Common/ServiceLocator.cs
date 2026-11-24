using System.Collections.Concurrent;

namespace LightEngine.Core.Common;

/// <summary>
/// Motor modülleri arasındaki bağımsızlığı sağlayan iş parçacığı güvenli (thread-safe) servis sağlayıcı.
/// Modüller (Render, Audio, Physics vb.) bu sınıf üzerinden birbirine bağlanmadan haberleşir.
/// </summary>
public class ServiceLocator : IServiceLocator
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    /// <summary>
    /// Belirtilen tipte servisi sisteme ekler.
    /// </summary>
    public void Register<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    /// <summary>
    /// Kayıtlı servisi döndürür. Servis bulunamazsa <see cref="InvalidOperationException"/> fırlatır.
    /// </summary>
    public T Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }

        throw new InvalidOperationException($"[LightEngine] '{typeof(T).FullName}' servisi henüz kaydedilmemiş.");
    }

    /// <summary>
    /// Kayıtlı servisi güvenli bir şekilde getirmeyi dener.
    /// </summary>
    public bool TryGet<T>(out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var found))
        {
            service = (T)found;
            return true;
        }

        service = null;
        return false;
    }

    /// <summary>
    /// Belirtilen servisi sistemden kaldırır.
    /// </summary>
    public void Unregister<T>() where T : class
    {
        _services.TryRemove(typeof(T), out _);
    }

    /// <summary>
    /// Tüm servisleri sıfırlar.
    /// </summary>
    public void Clear()
    {
        _services.Clear();
    }
}
