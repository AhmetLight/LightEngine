using Microsoft.Xna.Framework;

namespace LightEngine.Core.Animation3D;

/// <summary>
/// Tek bir kemik için belirli bir zamandaki konum, rotasyon (Quaternion) ve ölçek anahtar karesi.
/// </summary>
public readonly struct BoneKeyframe3D
{
    public float Time { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public Vector3 Scale { get; }

    public BoneKeyframe3D(float time, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        Time = time;
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }
}

/// <summary>
/// Bir kemiğe ait zaman eksenli anahtar kare dizisi (Lerp / Slerp ara değerleme destekli).
/// </summary>
public class BoneChannel3D
{
    public int BoneIndex { get; }
    public List<BoneKeyframe3D> Keyframes { get; } = new();

    public BoneChannel3D(int boneIndex)
    {
        BoneIndex = boneIndex;
    }

    public void AddKeyframe(float time, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        Keyframes.Add(new BoneKeyframe3D(time, pos, Quaternion.Normalize(rot), scale));
        Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    public void Sample(float time, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        if (Keyframes.Count == 0)
        {
            position = Vector3.Zero;
            rotation = Quaternion.Identity;
            scale = Vector3.One;
            return;
        }

        if (Keyframes.Count == 1 || time <= Keyframes[0].Time)
        {
            var k = Keyframes[0];
            position = k.Position;
            rotation = k.Rotation;
            scale = k.Scale;
            return;
        }

        int last = Keyframes.Count - 1;
        if (time >= Keyframes[last].Time)
        {
            var k = Keyframes[last];
            position = k.Position;
            rotation = k.Rotation;
            scale = k.Scale;
            return;
        }

        for (int i = 0; i < last; i++)
        {
            var k0 = Keyframes[i];
            var k1 = Keyframes[i + 1];
            if (time >= k0.Time && time <= k1.Time)
            {
                float span = Math.Max(0.00001f, k1.Time - k0.Time);
                float t = (time - k0.Time) / span;
                position = Vector3.Lerp(k0.Position, k1.Position, t);
                rotation = Quaternion.Slerp(k0.Rotation, k1.Rotation, t);
                scale = Vector3.Lerp(k0.Scale, k1.Scale, t);
                return;
            }
        }

        var fallback = Keyframes[last];
        position = fallback.Position;
        rotation = fallback.Rotation;
        scale = fallback.Scale;
    }
}

/// <summary>
/// Birden fazla kemik kanalından oluşan 3D iskelet animasyon klibi.
/// </summary>
public class AnimationClip3D
{
    public string Name { get; }
    public float Duration { get; set; }
    public bool IsLooping { get; set; } = true;
    public List<BoneChannel3D> Channels { get; } = new();

    public AnimationClip3D(string name, float duration, bool isLooping = true)
    {
        Name = name;
        Duration = Math.Max(0.001f, duration);
        IsLooping = isLooping;
    }

    public BoneChannel3D AddChannel(int boneIndex)
    {
        var ch = new BoneChannel3D(boneIndex);
        Channels.Add(ch);
        return ch;
    }

    /// <summary>
    /// Belirtilen zaman anındaki tüm kemik pozlarını önceden ayrılmış dizilere (allocation yapmadan) örnekler.
    /// </summary>
    public void SamplePose(float time, Vector3[] positions, Quaternion[] rotations, Vector3[] scales, bool[] hasOverride)
    {
        Array.Clear(hasOverride, 0, hasOverride.Length);

        float sampleTime = IsLooping ? (time % Duration) : Math.Clamp(time, 0f, Duration);
        if (sampleTime < 0f) sampleTime += Duration;

        for (int i = 0; i < Channels.Count; i++)
        {
            var ch = Channels[i];
            if (ch.BoneIndex < 0 || ch.BoneIndex >= positions.Length) continue;

            ch.Sample(sampleTime, out positions[ch.BoneIndex], out rotations[ch.BoneIndex], out scales[ch.BoneIndex]);
            hasOverride[ch.BoneIndex] = true;
        }
    }
}
