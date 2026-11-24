using LightEngine.Core.Ecs;

namespace LightEngine.Core.Animation;

/// <summary>
/// Animasyon durumları arasındaki geçiş şartını temsil eden sınıf.
/// </summary>
public class AnimationTransition
{
    public string TargetState { get; }
    public Func<AnimationStateMachine, bool> Condition { get; }

    public AnimationTransition(string targetState, Func<AnimationStateMachine, bool> condition)
    {
        TargetState = targetState;
        Condition = condition;
    }
}

/// <summary>
/// Oyun durumlarına (örn: Yürüme, Koşma, Saldırı) bağlı olarak animasyonları
/// parametrelerle (bool, float, trigger) yöneten Animasyon Durum Makinesi (State Machine).
/// </summary>
public class AnimationStateMachine : Component
{
    private Animator2D? _animator;
    private readonly Dictionary<string, AnimationClip> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<AnimationTransition>> _transitions = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, bool> _boolParams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _floatParams = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _triggers = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentState { get; private set; } = "";

    public override void Initialize()
    {
        base.Initialize();
        _animator = Entity?.GetComponent<Animator2D>();
        if (_animator == null && Entity != null)
        {
            _animator = Entity.AddComponent<Animator2D>();
        }

        foreach (var clip in _states.Values)
        {
            _animator?.AddClip(clip);
        }

        if (!string.IsNullOrEmpty(CurrentState))
        {
            _animator?.Play(CurrentState);
        }
    }

    public AnimationStateMachine AddState(string stateName, AnimationClip clip)
    {
        _states[stateName] = clip;
        _transitions[stateName] = new List<AnimationTransition>();

        if (string.IsNullOrEmpty(CurrentState))
        {
            CurrentState = stateName;
        }

        _animator?.AddClip(clip);
        return this;
    }

    public AnimationStateMachine AddTransition(string fromState, string toState, Func<AnimationStateMachine, bool> condition)
    {
        if (!_transitions.TryGetValue(fromState, out var list))
        {
            list = new List<AnimationTransition>();
            _transitions[fromState] = list;
        }

        list.Add(new AnimationTransition(toState, condition));
        return this;
    }

    #region Parametre Yönetimi

    public void SetBool(string name, bool value) => _boolParams[name] = value;
    public bool GetBool(string name) => _boolParams.TryGetValue(name, out bool val) && val;

    public void SetFloat(string name, float value) => _floatParams[name] = value;
    public float GetFloat(string name) => _floatParams.TryGetValue(name, out float val) ? val : 0f;

    public void SetTrigger(string name) => _triggers.Add(name);

    #endregion

    public override void Update(float deltaTime)
    {
        if (string.IsNullOrEmpty(CurrentState)) return;

        // Geçerli durumdan diğer durumlara geçiş şartlarını kontrol et
        if (_transitions.TryGetValue(CurrentState, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                var transition = list[i];
                if (transition.Condition(this))
                {
                    ChangeState(transition.TargetState);
                    break;
                }
            }
        }

        // Tetikleyicileri (Triggers) bir sonraki karede tüket
        _triggers.Clear();
    }

    public void ChangeState(string newState)
    {
        if (CurrentState == newState || !_states.ContainsKey(newState)) return;

        CurrentState = newState;
        _animator?.Play(newState);
    }
}
