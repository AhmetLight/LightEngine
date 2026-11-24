using LightEngine.Core.Ecs;
using Microsoft.Xna.Framework;

namespace LightEngine.Core.Animation3D;

/// <summary>
/// 3D iskelet (Skeleton3D) üzerinde klipleri oynatan, klipler arası yumuşak geçiş (Cross-Fade Blend)
/// yapan ve sıfır allocation ile GPU skinning matrislerini hesaplayan bileşen.
/// </summary>
public class SkeletalAnimator3D : Component
{
    private readonly Dictionary<string, AnimationClip3D> _clips = new(StringComparer.OrdinalIgnoreCase);

    private AnimationClip3D? _currentClip;
    private float _currentTime;

    private AnimationClip3D? _previousClip;
    private float _previousTime;
    private float _blendElapsed;
    private float _blendDuration;

    // Sıfır kare başı allocation için önceden ayrılmış kemik poz tamponları
    private Vector3[] _curPos = Array.Empty<Vector3>();
    private Quaternion[] _curRot = Array.Empty<Quaternion>();
    private Vector3[] _curScale = Array.Empty<Vector3>();
    private bool[] _curHas = Array.Empty<bool>();

    private Vector3[] _prevPos = Array.Empty<Vector3>();
    private Quaternion[] _prevRot = Array.Empty<Quaternion>();
    private Vector3[] _prevScale = Array.Empty<Vector3>();
    private bool[] _prevHas = Array.Empty<bool>();

    private Matrix[] _worldBoneMatrices = Array.Empty<Matrix>();
    private Matrix[] _skinningMatrices = Array.Empty<Matrix>();

    public Skeleton3D? Skeleton { get; private set; }
    public float PlaybackSpeed { get; set; } = 1.0f;
    public bool IsPlaying { get; set; } = true;
    public AnimationClip3D? CurrentClip => _currentClip;
    public float CurrentTime => _currentTime;
    public bool IsBlending => _previousClip != null && _blendDuration > 0f && _blendElapsed < _blendDuration;
    public float BlendFactor => IsBlending ? Math.Clamp(_blendElapsed / _blendDuration, 0f, 1f) : 1f;

    /// <summary>
    /// GPU vertex skinning shader'larına gönderilmeye hazır son kemik dönüşüm matrisleri.
    /// </summary>
    public IReadOnlyList<Matrix> SkinningMatrices => _skinningMatrices;

    /// <summary>
    /// Her kemiğin model uzayındaki dünya matrisleri (ekipman/silah bağlama soketleri için).
    /// </summary>
    public IReadOnlyList<Matrix> WorldBoneMatrices => _worldBoneMatrices;

    public SkeletalAnimator3D() { }

    public SkeletalAnimator3D(Skeleton3D skeleton)
    {
        SetSkeleton(skeleton);
    }

    public void SetSkeleton(Skeleton3D skeleton)
    {
        Skeleton = skeleton ?? throw new ArgumentNullException(nameof(skeleton));
        int count = skeleton.BoneCount;

        _curPos = new Vector3[count];
        _curRot = new Quaternion[count];
        _curScale = new Vector3[count];
        _curHas = new bool[count];

        _prevPos = new Vector3[count];
        _prevRot = new Quaternion[count];
        _prevScale = new Vector3[count];
        _prevHas = new bool[count];

        _worldBoneMatrices = new Matrix[count];
        _skinningMatrices = new Matrix[count];

        for (int i = 0; i < count; i++)
        {
            _worldBoneMatrices[i] = Matrix.Identity;
            _skinningMatrices[i] = Matrix.Identity;
        }
    }

    public void AddClip(AnimationClip3D clip)
    {
        _clips[clip.Name] = clip;
    }

    /// <summary>
    /// Belirtilen klibi anında başlatır.
    /// </summary>
    public bool Play(string clipName, bool restart = false)
    {
        if (!_clips.TryGetValue(clipName, out var clip)) return false;
        if (_currentClip == clip && !restart) return true;

        _previousClip = null;
        _blendDuration = 0f;
        _blendElapsed = 0f;
        _currentClip = clip;
        _currentTime = 0f;
        IsPlaying = true;
        EvaluatePose();
        return true;
    }

    /// <summary>
    /// Mevcut klipten yeni klibe belirtilen süre boyunca (saniye) yumuşak Cross-Fade geçişi başlatır.
    /// </summary>
    public bool CrossFade(string clipName, float blendDuration = 0.25f)
    {
        if (!_clips.TryGetValue(clipName, out var target)) return false;
        if (_currentClip == target) return true;

        if (_currentClip == null || blendDuration <= 0.001f)
        {
            return Play(clipName, restart: true);
        }

        _previousClip = _currentClip;
        _previousTime = _currentTime;
        _blendDuration = blendDuration;
        _blendElapsed = 0f;

        _currentClip = target;
        _currentTime = 0f;
        IsPlaying = true;
        EvaluatePose();
        return true;
    }

    public override void Update(float deltaTime)
    {
        if (!IsEnabled || !IsPlaying || Skeleton == null || _currentClip == null) return;

        float scaledDt = deltaTime * PlaybackSpeed;
        _currentTime += scaledDt;

        if (_previousClip != null)
        {
            _previousTime += scaledDt;
            _blendElapsed += deltaTime;
            if (_blendElapsed >= _blendDuration)
            {
                _previousClip = null;
                _blendDuration = 0f;
            }
        }

        EvaluatePose();
    }

    /// <summary>
    /// Tüm kemiklerin yerel, dünya ve skinning matrislerini harmanlayarak (blend) günceller.
    /// </summary>
    public void EvaluatePose()
    {
        if (Skeleton == null || _currentClip == null) return;

        int count = Skeleton.BoneCount;
        _currentClip.SamplePose(_currentTime, _curPos, _curRot, _curScale, _curHas);

        bool blending = _previousClip != null && _blendDuration > 0f;
        float weight = blending ? Math.Clamp(_blendElapsed / _blendDuration, 0f, 1f) : 1f;

        if (blending)
        {
            _previousClip!.SamplePose(_previousTime, _prevPos, _prevRot, _prevScale, _prevHas);
        }

        for (int i = 0; i < count; i++)
        {
            var bone = Skeleton.Bones[i];
            Matrix local;

            if (_curHas[i])
            {
                Vector3 pos = _curPos[i];
                Quaternion rot = _curRot[i];
                Vector3 scl = _curScale[i];

                if (blending && _prevHas[i])
                {
                    pos = Vector3.Lerp(_prevPos[i], pos, weight);
                    rot = Quaternion.Slerp(_prevRot[i], rot, weight);
                    scl = Vector3.Lerp(_prevScale[i], scl, weight);
                }

                local = Matrix.CreateScale(scl) * Matrix.CreateFromQuaternion(rot) * Matrix.CreateTranslation(pos);
            }
            else
            {
                local = bone.LocalBindPose;
            }

            _worldBoneMatrices[i] = bone.ParentIndex >= 0
                ? local * _worldBoneMatrices[bone.ParentIndex]
                : local;

            _skinningMatrices[i] = bone.InverseBindMatrix * _worldBoneMatrices[i];
        }
    }
}
