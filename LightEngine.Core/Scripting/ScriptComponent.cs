using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Input;
using LightEngine.Core.Rendering3D;

namespace LightEngine.Core.Scripting;

/// <summary>
/// Kullanıcıların C# ile özel oyun mantığı (Component) yazabilmesi için temel betik sınıfı.
/// Unity MonoBehaviour / Godot Node betik yapısına benzer temiz yaşam döngüsü kancaları sunar.
/// </summary>
public abstract class ScriptComponent : Component
{
    /// <summary>
    /// Hızlı girdi (Input) servisi erişimi.
    /// </summary>
    protected IInputService Input => Engine.Services.Get<IInputService>();

    /// <summary>
    /// Hızlı 3D Transform erişimi (varsa).
    /// </summary>
    protected Transform3D? Transform3D => Entity?.GetComponent<Transform3D>();

    /// <summary>
    /// Betik sahnede ilk kez aktifleştiğinde çağrılır.
    /// </summary>
    public virtual void OnStart() { }

    /// <summary>
    /// Her oyun karesinde (değişken zaman adımı ile) çağrılır.
    /// </summary>
    public virtual void OnUpdate(float deltaTime) { }

    /// <summary>
    /// Sabit zaman adımıyla (fizik ve mantık için) çağrılır.
    /// </summary>
    public virtual void OnFixedUpdate(float fixedDeltaTime) { }

    /// <summary>
    /// Betik veya bağlı olduğu Entity yok edildiğinde çağrılır.
    /// </summary>
    public virtual void OnDestroy() { }

    public override void Initialize()
    {
        base.Initialize();
        OnStart();
    }

    public override void Update(float deltaTime)
    {
        OnUpdate(deltaTime);
    }

    public override void FixedUpdate(float fixedDeltaTime)
    {
        OnFixedUpdate(fixedDeltaTime);
    }

    public override void Destroy()
    {
        OnDestroy();
        base.Destroy();
    }
}
