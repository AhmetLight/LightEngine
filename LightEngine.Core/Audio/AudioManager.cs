using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace LightEngine.Core.Audio;

/// <summary>
/// MonoGame ve OpenAL ses katmanını yöneten, harici dosya olmadan da
/// prosedürel ses sentezleyebilen somut ses servisi sınıfı.
/// </summary>
public class AudioManager : IAudioService
{
    private float _masterVolume = 1.0f;
    private float _sfxVolume = 1.0f;
    private float _musicVolume = 0.8f;
    private bool _isMuted;

    private SoundEffect? _laserSynth;
    private SoundEffect? _explosionSynth;
    private SoundEffect? _coinSynth;

    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0f, 1f);
            SoundEffect.MasterVolume = _isMuted ? 0f : _masterVolume * _sfxVolume;
            MediaPlayer.Volume = _isMuted ? 0f : _masterVolume * _musicVolume;
        }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = Math.Clamp(value, 0f, 1f);
            SoundEffect.MasterVolume = _isMuted ? 0f : _masterVolume * _sfxVolume;
        }
    }

    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Math.Clamp(value, 0f, 1f);
            MediaPlayer.Volume = _isMuted ? 0f : _masterVolume * _musicVolume;
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            MasterVolume = _masterVolume;
        }
    }

    public AudioManager()
    {
        try
        {
            SoundEffect.MasterVolume = _sfxVolume;
            MediaPlayer.Volume = _musicVolume;
            GenerateProceduralSounds();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LightEngine] Ses kartı ilklendirme uyarısı: {ex.Message}");
        }
    }

    public SoundEffectInstance? PlaySound(string soundPath, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        if (_isMuted) return null;

        try
        {
            var asset = Engine.Services.Get<IAssetService>();
            var sfx = asset.LoadSound(soundPath);
            if (sfx != null)
            {
                var instance = sfx.CreateInstance();
                instance.Volume = Math.Clamp(volume * _masterVolume * _sfxVolume, 0f, 1f);
                instance.Pitch = Math.Clamp(pitch, -1f, 1f);
                instance.Pan = Math.Clamp(pan, -1f, 1f);
                instance.Play();
                return instance;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LightEngine] Ses çalma hatası ({soundPath}): {ex.Message}");
        }
        return null;
    }

    private readonly AudioListener _audioListener = new();
    private readonly AudioEmitter _audioEmitter = new();

    public SoundEffectInstance? PlaySound3D(string soundPath, Vector3 emitterPosition, Vector3 listenerPosition, Vector3 listenerForward, Vector3 listenerUp, float volume = 1.0f, float maxDistance = 30.0f)
    {
        if (_isMuted) return null;

        try
        {
            var asset = Engine.Services.Get<IAssetService>();
            var sfx = asset.LoadSound(soundPath);
            if (sfx != null)
            {
                var instance = sfx.CreateInstance();
                _audioListener.Position = listenerPosition;
                _audioListener.Forward = listenerForward;
                _audioListener.Up = listenerUp;

                _audioEmitter.Position = emitterPosition;
                _audioEmitter.Forward = Vector3.Forward;
                _audioEmitter.Up = Vector3.Up;

                float dist = Vector3.Distance(emitterPosition, listenerPosition);
                float attenuation = Math.Clamp(1.0f - (dist / Math.Max(0.1f, maxDistance)), 0f, 1f);

                instance.Volume = Math.Clamp(volume * _masterVolume * _sfxVolume * attenuation, 0f, 1f);
                instance.Apply3D(_audioListener, _audioEmitter);
                instance.Play();
                return instance;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LightEngine] 3D ses çalma hatası ({soundPath}): {ex.Message}");
        }

        return null;
    }

    public void PlayLaserSound()

    {
        if (_isMuted) return;
        try { _laserSynth?.Play(_masterVolume * _sfxVolume, 0f, 0f); } catch { }
    }

    public void PlayExplosionSound()
    {
        if (_isMuted) return;
        try { _explosionSynth?.Play(_masterVolume * _sfxVolume, 0f, 0f); } catch { }
    }

    public void PlayCoinSound()
    {
        if (_isMuted) return;
        try { _coinSynth?.Play(_masterVolume * _sfxVolume, 0f, 0f); } catch { }
    }

    public void PlayMusic(string musicPath, bool isRepeating = true)
    {
        try
        {
            var asset = Engine.Services.Get<IAssetService>();
            var song = asset.Load<Song>(musicPath);
            if (song != null)
            {
                MediaPlayer.IsRepeating = isRepeating;
                MediaPlayer.Play(song);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LightEngine] Müzik çalma uyarısı ({musicPath}): {ex.Message}");
        }
    }

    public void StopMusic()
    {
        try { MediaPlayer.Stop(); } catch { }
    }

    public void PauseMusic()
    {
        try { MediaPlayer.Pause(); } catch { }
    }

    public void ResumeMusic()
    {
        try { MediaPlayer.Resume(); } catch { }
    }

    private void GenerateProceduralSounds()
    {
        const int sampleRate = 22050;

        // 1. Lazer sesi (Frekans süpürme: 880Hz -> 220Hz)
        {
            float duration = 0.14f;
            int totalSamples = (int)(sampleRate * duration);
            byte[] buffer = new byte[totalSamples * 2]; // 16-bit mono PCM

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = MathHelper.Lerp(880f, 220f, t);
                float angle = 2f * MathF.PI * freq * (i / (float)sampleRate);
                short sample = (short)(MathF.Sin(angle) * (1f - t) * short.MaxValue * 0.6f);

                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            _laserSynth = new SoundEffect(buffer, sampleRate, AudioChannels.Mono);
        }

        // 2. Patlama sesi (Beyaz gürültü + üstel sönümleme)
        {
            float duration = 0.35f;
            int totalSamples = (int)(sampleRate * duration);
            byte[] buffer = new byte[totalSamples * 2];
            var rand = new Random(42);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float envelope = MathF.Pow(1f - t, 2.5f);
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                short sample = (short)(noise * envelope * short.MaxValue * 0.7f);

                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            _explosionSynth = new SoundEffect(buffer, sampleRate, AudioChannels.Mono);
        }

        // 3. Eşya/Altın Toplama sesi (İki tonlu arpej: 987Hz -> 1318Hz)
        {
            float duration = 0.20f;
            int totalSamples = (int)(sampleRate * duration);
            byte[] buffer = new byte[totalSamples * 2];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = t < 0.5f ? 987f : 1318f;
                float angle = 2f * MathF.PI * freq * (i / (float)sampleRate);
                short sample = (short)(MathF.Sin(angle) * (1f - t * 0.5f) * short.MaxValue * 0.5f);

                buffer[i * 2] = (byte)(sample & 0xFF);
                buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            _coinSynth = new SoundEffect(buffer, sampleRate, AudioChannels.Mono);
        }
    }
}
