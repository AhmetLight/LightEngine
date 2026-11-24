using LightEngine.Core.Ecs;

namespace LightEngine.Core.Animation3D;

/// <summary>
/// 3D iskelet animasyon durumları arasındaki koşullu geçiş (transition) tanımı.
/// </summary>
public class AnimationTransition3D
{
    public string FromState { get; }
    public string ToState { get; }
    public float BlendDuration { get; }
    public Func<AnimationStateMachine3D, bool> Condition { get; }

    public AnimationTransition3D(string fromState, string toState, float blendDuration, Func<AnimationStateMachine3D, bool> condition)
    {
        FromState = fromState;
        ToState = toState;
        BlendDuration = blendDuration;
        Condition = condition;
    }
}

/// <summary>
/// Parametrelere (float, bool, trigger) göre 3D iskelet animasyon durumları arasında
/// otomatik Cross-Fade geçişi yöneten 3D Animasyon Durum Makinesi (State Machine).
/// </summary>
public class AnimationStateMachine3D : Component
{
    private readonly Dictionary<string, string> _stateToClip = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AnimationTransition3D> _transitions = new();
    private readonly Dictionary<string, float> _floatParams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _boolParams = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _triggers = new(StringComparer.OrdinalIgnoreCase);

    private SkeletalAnimator3D? _animator;

    public string CurrentState { get; private set; } = "";

    public override void Initialize()
    {
        base.Initialize();
        _animator = Entity?.GetComponent<SkeletalAnimator3D>();
        if (_animator != null && !string.IsNullOrEmpty(CurrentState) && _stateToClip.TryGetValue(CurrentState, out string? clip))
        {
            _animator.Play(clip);
        }
    }

    public void AddState(string stateName, string clipName, bool isDefault = false)
    {
        _stateToClip[stateName] = clipName;
        if (isDefault || string.IsNullOrEmpty(CurrentState))
        {
            CurrentState = stateName;
        }
    }

    public void AddTransition(string fromState, string toState, Func<AnimationStateMachine3D, bool> condition, float blendDuration = 0.25f)
    {
        _transitions.Add(new AnimationTransition3D(fromState, toState, blendDuration, condition));
    }

    public void SetFloat(string name, float value) => _floatParams[name] = value;
    public float GetFloat(string name) => _floatParams.TryGetValue(name, out float v) ? v : 0f;

    public void SetBool(string name, bool value) => _boolParams[name] = value;
    public bool GetBool(string name) => _boolParams.TryGetValue(name, out bool v) && v;

    public void SetTrigger(string name) => _triggers.Add(name);
    public bool ConsumeTrigger(string name) => _triggers.Remove(name);

    public override void Update(float deltaTime)
    {
        if (!IsEnabled) return;
        _animator ??= Entity?.GetComponent<SkeletalAnimator3D>();
        if (_animator == null) return;

        for (int i = 0; i < _transitions.Count; i++)
        {
            var t = _transitions[i];
            bool matchesFrom = t.FromState == "*" || t.FromState.Equals(CurrentState, StringComparison.OrdinalIgnoreCase);
            if (!matchesFrom) continue;

            if (t.Condition(this))
            {
                if (_stateToClip.TryGetValue(t.ToState, out string? targetClip))
                {
                    CurrentState = t.ToState;
                    _animator.CrossFade(targetClip, t.BlendDuration);
                    break;
                }
            }
        }
    }
}
