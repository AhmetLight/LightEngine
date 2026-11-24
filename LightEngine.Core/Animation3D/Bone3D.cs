using Microsoft.Xna.Framework;

namespace LightEngine.Core.Animation3D;

/// <summary>
/// 3D iskelet hiyerarşisindeki tek bir kemiği (Bone) temsil eder.
/// </summary>
public class Bone3D
{
    public string Name { get; }
    public int Index { get; }
    public int ParentIndex { get; }
    public Matrix LocalBindPose { get; set; }
    public Matrix InverseBindMatrix { get; set; }

    public Bone3D(string name, int index, int parentIndex, Matrix localBindPose, Matrix inverseBindMatrix)
    {
        Name = name;
        Index = index;
        ParentIndex = parentIndex;
        LocalBindPose = localBindPose;
        InverseBindMatrix = inverseBindMatrix;
    }
}

/// <summary>
/// Kemik hiyerarşisini (hiyerarşik ebeveyn-çocuk sırasına göre) ve önceden ayrılmış skinning matris tamponlarını tutar.
/// </summary>
public class Skeleton3D
{
    private readonly List<Bone3D> _bones = new();
    private readonly Dictionary<string, int> _nameToIndex = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Bone3D> Bones => _bones;
    public int BoneCount => _bones.Count;

    public int AddBone(string name, int parentIndex, Matrix localBindPose)
    {
        int index = _bones.Count;
        Matrix globalBind = parentIndex >= 0 ? localBindPose * GetGlobalBindPose(parentIndex) : localBindPose;
        Matrix invBind = Matrix.Invert(globalBind);

        var bone = new Bone3D(name, index, parentIndex, localBindPose, invBind);
        _bones.Add(bone);
        _nameToIndex[name] = index;
        return index;
    }

    public int FindBoneIndex(string name) => _nameToIndex.TryGetValue(name, out int idx) ? idx : -1;

    private Matrix GetGlobalBindPose(int index)
    {
        var b = _bones[index];
        return b.ParentIndex >= 0 ? b.LocalBindPose * GetGlobalBindPose(b.ParentIndex) : b.LocalBindPose;
    }

    /// <summary>
    /// Test ve prototipleme için 5 kemikli basit bir insansı (Root -> Spine -> Head, LeftArm, RightArm) iskelet üretir.
    /// </summary>
    public static Skeleton3D CreateSimpleHumanoid()
    {
        var skel = new Skeleton3D();
        int root = skel.AddBone("Root", -1, Matrix.Identity);
        int spine = skel.AddBone("Spine", root, Matrix.CreateTranslation(0f, 0.9f, 0f));
        skel.AddBone("Head", spine, Matrix.CreateTranslation(0f, 0.6f, 0f));
        skel.AddBone("LeftArm", spine, Matrix.CreateTranslation(-0.45f, 0.45f, 0f));
        skel.AddBone("RightArm", spine, Matrix.CreateTranslation(0.45f, 0.45f, 0f));
        return skel;
    }
}
