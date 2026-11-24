namespace LightEngine.Core.Common;

/// <summary>
/// Motor alt sistemlerine (Services, Time vb.) global ve modüler erişim sağlayan ana statik sınıf.
/// </summary>
public static class Engine
{
    private static IServiceLocator? s_services;

    /// <summary>
    /// Motor servislerinin ilklendirilip ilklendirilmediğini belirtir.
    /// </summary>
    public static bool IsInitialized => s_services != null;

    /// <summary>
    /// Motorun modüler servis yöneticisi (Render, Physics, Audio, Input vb.).
    /// </summary>
    public static IServiceLocator Services
    {
        get => s_services ?? throw new InvalidOperationException("[LightEngine] Engine.Services henüz başlatılmadı.");
        set => s_services = value;
    }

    /// <summary>
    /// Zaman ve delta yönetimi servisine hızlı erişim.
    /// </summary>
    public static ITimeService Time => Services.Get<ITimeService>();
}
