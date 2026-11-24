namespace LightEngine.Core.Common;

/// <summary>
/// Havuzdan alınan ve iade edildiğinde sıfırlanması gereken nesneler için arayüz.
/// </summary>
public interface IPoolable
{
    /// <summary>
    /// Nesne havuza geri dönerken veya yeniden alınırken durumunu sıfırlar.
    /// </summary>
    void Reset();
}

/// <summary>
/// Çöp toplayıcı (GC) baskısını ve bellek tahsislerini (allocation) minimize etmek için genel nesne havuzu.
/// Mermiler, parçacıklar ve geçici hesaplama nesneleri için kullanılır.
/// </summary>
/// <typeparam name="T">Havuzlanacak nesne tipi.</typeparam>
public class ObjectPool<T> where T : class, new()
{
    private readonly Stack<T> _pool;
    private readonly Func<T>? _factory;
    private readonly Action<T>? _onGet;
    private readonly Action<T>? _onReturn;
    private readonly object _lock = new();

    /// <summary>
    /// Havuzda boşta bekleyen nesne sayısı.
    /// </summary>
    public int AvailableCount
    {
        get
        {
            lock (_lock)
            {
                return _pool.Count;
            }
        }
    }

    /// <summary>
    /// Şu anda kullanımda (havuz dışında) olan tahmini nesne sayısı.
    /// </summary>
    public int ActiveCount { get; private set; }

    /// <summary>
    /// Toplam üretilmiş nesne sayısı.
    /// </summary>
    public int TotalCreated { get; private set; }

    public ObjectPool(int initialCapacity = 32, Func<T>? factory = null, Action<T>? onGet = null, Action<T>? onReturn = null)
    {
        _pool = new Stack<T>(initialCapacity);
        _factory = factory;
        _onGet = onGet;
        _onReturn = onReturn;

        WarmUp(initialCapacity);
    }

    /// <summary>
    /// Havuzu önceden belirlenen miktarda nesneyle doldurarak ilk kare gecikmelerini (frame drop) engeller.
    /// </summary>
    public void WarmUp(int count)
    {
        lock (_lock)
        {
            for (int i = 0; i < count; i++)
            {
                var item = _factory != null ? _factory() : new T();
                TotalCreated++;
                _pool.Push(item);
            }
        }
    }

    /// <summary>
    /// Havuzdan bir nesne alır. Havuz boşsa yeni bir nesne üretir.
    /// </summary>
    public T Get()
    {
        T item;
        lock (_lock)
        {
            if (_pool.Count > 0)
            {
                item = _pool.Pop();
            }
            else
            {
                item = _factory != null ? _factory() : new T();
                TotalCreated++;
            }

            ActiveCount++;
        }

        if (item is IPoolable poolable)
        {
            poolable.Reset();
        }

        _onGet?.Invoke(item);
        return item;
    }

    /// <summary>
    /// Kullanımı biten nesneyi havuza geri verir.
    /// </summary>
    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _onReturn?.Invoke(item);

        if (item is IPoolable poolable)
        {
            poolable.Reset();
        }

        lock (_lock)
        {
            _pool.Push(item);
            if (ActiveCount > 0)
            {
                ActiveCount--;
            }
        }
    }

    /// <summary>
    /// Havuzdaki tüm nesneleri temizler.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _pool.Clear();
            ActiveCount = 0;
            TotalCreated = 0;
        }
    }
}

/// <summary>
/// Statik olarak hızlı erişim sağlayan global nesne havuzları.
/// </summary>
public static class GlobalPool<T> where T : class, new()
{
    private static readonly Lazy<ObjectPool<T>> s_instance = new(() => new ObjectPool<T>(64));

    public static ObjectPool<T> Shared => s_instance.Value;

    public static T Get() => Shared.Get();
    public static void Return(T item) => Shared.Return(item);
}
