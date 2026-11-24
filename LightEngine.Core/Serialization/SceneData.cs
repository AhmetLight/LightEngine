namespace LightEngine.Core.Serialization;

/// <summary>
/// İnsan tarafından kolayca okunabilen ve düzenlenebilen JSON sahne veri modeli.
/// </summary>
public class SceneData
{
    public string Name { get; set; } = "Scene";
    public List<EntityData> Entities { get; set; } = new();
}

public class EntityData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Entity";
    public string Tag { get; set; } = "Untagged";
    public bool IsActive { get; set; } = true;

    public TransformData Transform { get; set; } = new();
    public List<ComponentData> Components { get; set; } = new();
}

public class TransformData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
    public float ScaleX { get; set; } = 1.0f;
    public float ScaleY { get; set; } = 1.0f;
}

public class ComponentData
{
    public string Type { get; set; } = "";
    public Dictionary<string, string> Properties { get; set; } = new();
}
