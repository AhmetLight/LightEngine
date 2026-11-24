using Microsoft.Xna.Framework;

namespace LightEngine.Core.Common;

/// <summary>
/// Motor zaman ve kare hızı (frame rate) servisi arayüzü.
/// </summary>
public interface ITimeService
{
    /// <summary>
    /// Son kareden bu yana geçen ölçeklenmiş süre (saniye).
    /// </summary>
    float DeltaTime { get; }

    /// <summary>
    /// Zaman ölçeğinden (TimeScale) etkilenmeyen ham delta süresi (saniye).
    /// </summary>
    float UnscaledDeltaTime { get; }

    /// <summary>
    /// Sabit fizik ve mantık güncellemeleri için sabit zaman adımı (saniye, varsayılan: 1/60 sn).
    /// </summary>
    float FixedDeltaTime { get; set; }

    /// <summary>
    /// Oyun başladığından beri geçen toplam süre (saniye).
    /// </summary>
    float TotalTime { get; }

    /// <summary>
    /// Zaman akış hızı katsayısı (1.0 = normal, 0.5 = ağır çekim, 2.0 = hızlı).
    /// </summary>
    float TimeScale { get; set; }

    /// <summary>
    /// İşlenen toplam kare sayısı.
    /// </summary>
    long FrameCount { get; }

    /// <summary>
    /// Anlık saniyedeki kare hızı (FPS).
    /// </summary>
    float FPS { get; }

    /// <summary>
    /// Yumuşatılmış ortalama FPS değeri.
    /// </summary>
    float AverageFPS { get; }

    /// <summary>
    /// Son ölçülen kare işleme süresi (milisaniye cinsinden).
    /// </summary>
    float FrameTimeMs { get; }
}

/// <summary>
/// Sabit zaman adımı (fixed timestep) ve değişken kare güncellemelerini yöneten sınıf.
/// </summary>
public class TimeManager : ITimeService
{
    private const int FpsSampleSize = 60;
    private readonly float[] _fpsBuffer = new float[FpsSampleSize];
    private int _fpsBufferIndex;
    private int _fpsBufferCount;

    public float DeltaTime { get; private set; }
    public float UnscaledDeltaTime { get; private set; }
    public float FixedDeltaTime { get; set; } = 1.0f / 60.0f;
    public float TotalTime { get; private set; }
    public float TimeScale { get; set; } = 1.0f;
    public long FrameCount { get; private set; }

    public float FPS { get; private set; }
    public float AverageFPS { get; private set; }
    public float FrameTimeMs => UnscaledDeltaTime * 1000f;

    /// <summary>
    /// MonoGame Update döngüsünden çağrılarak zaman ve FPS değerlerini günceller.
    /// </summary>
    public void Update(GameTime gameTime)
    {
        UnscaledDeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Çok uzun süren karelerde (örneğin pencere sürüklendiğinde) fizik patlamasını önlemek için sınırlama (clamping)
        if (UnscaledDeltaTime > 0.25f)
        {
            UnscaledDeltaTime = 0.25f;
        }

        DeltaTime = UnscaledDeltaTime * TimeScale;
        TotalTime = (float)gameTime.TotalGameTime.TotalSeconds;
        FrameCount++;

        // FPS hesabı
        FPS = UnscaledDeltaTime > 0.00001f ? 1.0f / UnscaledDeltaTime : 0f;

        _fpsBuffer[_fpsBufferIndex] = FPS;
        _fpsBufferIndex = (_fpsBufferIndex + 1) % FpsSampleSize;
        if (_fpsBufferCount < FpsSampleSize)
        {
            _fpsBufferCount++;
        }

        float sum = 0f;
        for (int i = 0; i < _fpsBufferCount; i++)
        {
            sum += _fpsBuffer[i];
        }
        AverageFPS = _fpsBufferCount > 0 ? sum / _fpsBufferCount : FPS;
    }
}
