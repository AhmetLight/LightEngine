using Microsoft.Xna.Framework.Audio;

namespace LightEngine.Core.Audio;

/// <summary>
/// Ses efektleri (SFX) ve arka plan müziklerini yöneten ses servisi arayüzü.
/// </summary>
public interface IAudioService
{
    float MasterVolume { get; set; }
    float SfxVolume { get; set; }
    float MusicVolume { get; set; }
    bool IsMuted { get; set; }

    /// <summary>
    /// İsmi veya yolu verilen ses efektini çalar.
    /// </summary>
    SoundEffectInstance? PlaySound(string soundPath, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f);

    /// <summary>
    /// Verilen ses efektini 3D uzayda dinleyici ve kaynak konumuna göre uzamsal (spatial) olarak çalar.
    /// </summary>
    SoundEffectInstance? PlaySound3D(string soundPath, Microsoft.Xna.Framework.Vector3 emitterPosition, Microsoft.Xna.Framework.Vector3 listenerPosition, Microsoft.Xna.Framework.Vector3 listenerForward, Microsoft.Xna.Framework.Vector3 listenerUp, float volume = 1.0f, float maxDistance = 30.0f);

    /// <summary>
    /// Prosedürel sentezlenmiş lazer ateşleme sesini çalar.
    /// </summary>
    void PlayLaserSound();

    /// <summary>
    /// Prosedürel sentezlenmiş patlama sesini çalar.
    /// </summary>
    void PlayExplosionSound();

    /// <summary>
    /// Prosedürel sentezlenmiş eşya/para toplama zil sesini çalar.
    /// </summary>
    void PlayCoinSound();

    /// <summary>
    /// Arka plan müziği başlatır.
    /// </summary>
    void PlayMusic(string musicPath, bool isRepeating = true);

    void StopMusic();
    void PauseMusic();
    void ResumeMusic();
}

