using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Rendering3D;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace LightEngine.Core.Audio;

/// <summary>
/// 3D uzayda uzamsal ses (Spatial Audio) yayan bileşen.
/// Entity'nin Transform3D dünya koordinatını ve aktif sahnenin Camera3D dinleyicisini
/// kullanarak ses şiddetini ve stereo yönünü (panning) dinamik hesaplar.
/// </summary>
public class AudioEmitter3D : Component
{
    private SoundEffectInstance? _activeInstance;

    public string SoundPath { get; set; } = "";
    public float Volume { get; set; } = 1.0f;
    public float MaxDistance { get; set; } = 35.0f;
    public bool PlayOnStart { get; set; } = false;
    public bool IsLooping { get; set; } = false;
    public bool IsPlaying => _activeInstance != null && _activeInstance.State == SoundState.Playing;

    public AudioEmitter3D()
    {
    }

    public AudioEmitter3D(string soundPath, float volume = 1.0f, float maxDistance = 35.0f, bool isLooping = false)
    {
        SoundPath = soundPath;
        Volume = volume;
        MaxDistance = maxDistance;
        IsLooping = isLooping;
    }

    public override void Initialize()
    {
        base.Initialize();
        if (PlayOnStart && !string.IsNullOrWhiteSpace(SoundPath))
        {
            Play();
        }
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        if (_activeInstance != null && _activeInstance.State == SoundState.Playing)
        {
            UpdateSpatialAudio();
        }
    }

    /// <summary>
    /// 3D sesi başlatır.
    /// </summary>
    public void Play()
    {
        if (string.IsNullOrWhiteSpace(SoundPath)) return;
        Stop();

        if (Engine.Services.TryGet<IAudioService>(out var audio) && audio != null)
        {
            GetEmitterAndListenerPositions(out var emitterPos, out var listenerPos, out var forward, out var up);
            _activeInstance = audio.PlaySound3D(SoundPath, emitterPos, listenerPos, forward, up, Volume, MaxDistance);
            if (_activeInstance != null)
            {
                _activeInstance.IsLooped = IsLooping;
            }
        }
    }

    /// <summary>
    /// Çalmakta olan 3D sesi durdurur.
    /// </summary>
    public void Stop()
    {
        if (_activeInstance != null)
        {
            try
            {
                _activeInstance.Stop();
                _activeInstance.Dispose();
            }
            catch { }
            _activeInstance = null;
        }
    }

    private void UpdateSpatialAudio()
    {
        if (_activeInstance == null) return;

        GetEmitterAndListenerPositions(out var emitterPos, out var listenerPos, out var forward, out var up);

        float dist = Vector3.Distance(emitterPos, listenerPos);
        float attenuation = Math.Clamp(1.0f - (dist / Math.Max(0.1f, MaxDistance)), 0f, 1f);

        if (Engine.Services.TryGet<IAudioService>(out var audio) && audio != null)
        {
            _activeInstance.Volume = Math.Clamp(Volume * audio.MasterVolume * audio.SfxVolume * attenuation, 0f, 1f);
        }

        var listener = new AudioListener
        {
            Position = listenerPos,
            Forward = forward,
            Up = up
        };

        var emitter = new Microsoft.Xna.Framework.Audio.AudioEmitter
        {
            Position = emitterPos,
            Forward = Vector3.Forward,
            Up = Vector3.Up
        };

        try
        {
            _activeInstance.Apply3D(listener, emitter);
        }
        catch { }
    }

    private void GetEmitterAndListenerPositions(out Vector3 emitterPos, out Vector3 listenerPos, out Vector3 forward, out Vector3 up)
    {
        emitterPos = Vector3.Zero;
        if (Entity != null)
        {
            var t3d = Entity.GetComponent<Transform3D>();
            if (t3d != null)
            {
                emitterPos = t3d.WorldPosition;
            }
            else
            {
                emitterPos = new Vector3(Entity.Transform.WorldPosition.X, Entity.Transform.WorldPosition.Y, 0f);
            }
        }

        var scene = Entity?.Scene;
        if (scene != null && scene.Camera3D != null)
        {
            listenerPos = scene.Camera3D.Position;
            forward = scene.Camera3D.Forward;
            up = scene.Camera3D.Up;
        }
        else
        {
            listenerPos = new Vector3(0, 0, 10f);
            forward = Vector3.Forward;
            up = Vector3.Up;
        }
    }

    public override void Destroy()
    {
        Stop();
        base.Destroy();
    }
}
