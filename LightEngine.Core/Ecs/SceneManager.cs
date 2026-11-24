using LightEngine.Core.Rendering;

namespace LightEngine.Core.Ecs;

/// <summary>
/// Sahneler arası geçiş ve sahne döngülerini yöneten somut servis sınıfı.
/// Engine.Services üzerinden erişilir.
/// </summary>
public class SceneManager : ISceneService
{
    private readonly Dictionary<string, Func<IScene>> _sceneFactories = new(StringComparer.OrdinalIgnoreCase);
    private IScene? _nextScene;
    private bool _hasPendingSceneChange;

    public IScene? ActiveScene { get; private set; }

    public void RegisterScene(string name, Func<IScene> sceneFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(sceneFactory);

        _sceneFactories[name] = sceneFactory;
    }

    public void LoadScene(string name)
    {
        if (_sceneFactories.TryGetValue(name, out var factory))
        {
            LoadScene(factory());
        }
        else
        {
            throw new ArgumentException($"[LightEngine] '{name}' isimli kayıtlı sahne bulunamadı.");
        }
    }

    public void LoadScene(IScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        // Bir sonraki kare başında güvenle geçiş yapmak için sıraya al
        _nextScene = scene;
        _hasPendingSceneChange = true;
    }

    public void Update(float deltaTime)
    {
        ApplyPendingSceneChange();

        ActiveScene?.Update(deltaTime);
    }

    public void FixedUpdate(float fixedDeltaTime)
    {
        ActiveScene?.FixedUpdate(fixedDeltaTime);
    }

    public void Draw(SpriteBatchRenderer renderer)
    {
        ActiveScene?.Draw(renderer);
    }

    private void ApplyPendingSceneChange()
    {
        if (!_hasPendingSceneChange) return;

        _hasPendingSceneChange = false;

        if (ActiveScene != null)
        {
            ActiveScene.UnloadContent();
        }

        ActiveScene = _nextScene;
        _nextScene = null;

        if (ActiveScene != null)
        {
            ActiveScene.Initialize();
            ActiveScene.LoadContent();
        }
    }
}
