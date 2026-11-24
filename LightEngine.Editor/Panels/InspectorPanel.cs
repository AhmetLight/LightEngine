using System.Reflection;
using ImGuiNET;
using LightEngine.Core.Animation;
using LightEngine.Core.Assets;
using LightEngine.Core.Common;
using LightEngine.Core.Ecs;
using LightEngine.Core.Particles;
using LightEngine.Core.Physics2D;
using LightEngine.Core.Physics3D;
using LightEngine.Core.Animation3D;
using LightEngine.Core.Rendering;
using LightEngine.Core.Rendering3D;
using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;
using NumVec2 = System.Numerics.Vector2;
using NumVec3 = System.Numerics.Vector3;
using NumVec4 = System.Numerics.Vector4;

namespace LightEngine.Editor.Panels;

/// <summary>
/// Seçili Entity'nin adını, etiketini, Transform2D ve tüm bileşenlerini
/// (SpriteRenderer, BoxCollider2D, CircleCollider2D, ParticleEmitter, Animator2D)
/// canlı olarak düzenlemeyi sağlayan Detaylar (Inspector) Paneli.
/// </summary>
public class InspectorPanel
{
    private readonly EditorContext _context;

    public InspectorPanel(EditorContext context)
    {
        _context = context;
    }

    public void Draw()
    {
        if (ImGui.Begin("Inspector##InspectorPanel"))
        {
            var entity = _context.SelectedEntity;

            if (entity == null || entity.IsDestroyed)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("Select an entity from Hierarchy or Viewport to inspect.");
                ImGui.End();
                return;
            }

            // 1. GENEL BİLGİLER
            DrawGeneralInfo(entity);

            ImGui.Separator();

            // 2. TRANSFORM2D BİLEŞENİ
            DrawTransform(entity, entity.Transform);

            ImGui.Separator();

            // 3. EKLİ BİLEŞENLER
            DrawComponents(entity);

            ImGui.Separator();

            // 4. BİLEŞEN EKLE (Add Component)
            DrawAddComponentButton(entity);
        }
        ImGui.End();
    }

    private void DrawGeneralInfo(IEntity entity)
    {
        // Varlık Adı
        string name = entity.Name;
        if (ImGui.InputText("Name", ref name, 64))
        {
            entity.Name = name;
        }

        // Aktiflik
        bool isActive = entity.IsActive;
        if (ImGui.Checkbox("Active", ref isActive))
        {
            entity.IsActive = isActive;
        }

        ImGui.SameLine();

        // Etiket (Tag)
        string tag = entity.Tag;
        if (ImGui.InputText("Tag", ref tag, 32))
        {
            entity.Tag = tag;
        }
    }

    private void DrawTransform(IEntity entity, Transform2D transform)
    {
        if (ImGui.CollapsingHeader("Transform 2D", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var oldPos = transform.LocalPosition;
            var oldRot = transform.LocalRotation;
            var oldScale = transform.LocalScale;

            // Pozisyon (Position)
            var pos = transform.LocalPosition;
            NumVec2 posVec = new(pos.X, pos.Y);
            if (ImGui.DragFloat2("Position (X, Y)", ref posVec, 1f))
            {
                transform.LocalPosition = new Vector2(posVec.X, posVec.Y);
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                _context.RecordCommand(new Commands.TransformCommand(entity, oldPos, oldRot, oldScale, transform.LocalPosition, transform.LocalRotation, transform.LocalScale));
            }

            // Döndürme (Rotation - Derece cinsinden)
            float rotDeg = MathHelper.ToDegrees(transform.LocalRotation);
            if (ImGui.DragFloat("Rotation (Deg)", ref rotDeg, 1f, -360f, 360f))
            {
                transform.LocalRotation = MathHelper.ToRadians(rotDeg);
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                _context.RecordCommand(new Commands.TransformCommand(entity, oldPos, oldRot, oldScale, transform.LocalPosition, transform.LocalRotation, transform.LocalScale));
            }

            // Ölçek (Scale)
            var scale = transform.LocalScale;
            NumVec2 scaleVec = new(scale.X, scale.Y);
            if (ImGui.DragFloat2("Scale (X, Y)", ref scaleVec, 0.05f, 0.01f, 50f))
            {
                transform.LocalScale = new Vector2(scaleVec.X, scaleVec.Y);
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                _context.RecordCommand(new Commands.TransformCommand(entity, oldPos, oldRot, oldScale, transform.LocalPosition, transform.LocalRotation, transform.LocalScale));
            }

            if (ImGui.Button("Reset Transform"))
            {
                _context.RecordCommand(new Commands.TransformCommand(entity, oldPos, oldRot, oldScale, Vector2.Zero, 0f, Vector2.One));
                transform.LocalPosition = Vector2.Zero;
                transform.LocalRotation = 0f;
                transform.LocalScale = Vector2.One;
            }
        }
    }

    private void DrawComponents(IEntity entity)
    {
        for (int i = 0; i < entity.Components.Count; i++)
        {
            var comp = entity.Components[i];
            string typeName = comp.GetType().Name;

            bool isHeaderOpen = ImGui.CollapsingHeader($"{typeName}##Comp_{i}", ImGuiTreeNodeFlags.DefaultOpen);

            if (isHeaderOpen)
            {
                bool isEnabled = comp.IsEnabled;
                if (ImGui.Checkbox($"Enabled##CompEn_{i}", ref isEnabled))
                {
                    comp.IsEnabled = isEnabled;
                }

                // Bileşene özel kontroller
                if (comp is SpriteRenderer sr)
                {
                    DrawSpriteRendererDetails(sr);
                }
                else if (comp is BoxCollider2D box)
                {
                    DrawBoxColliderDetails(box);
                }
                else if (comp is CircleCollider2D circle)
                {
                    DrawCircleColliderDetails(circle);
                }
                else if (comp is ParticleEmitter pe)
                {
                    DrawParticleEmitterDetails(pe);
                }
                else if (comp is Animator2D anim)
                {
                    DrawAnimatorDetails(anim);
                }
                else if (comp is Transform3D t3d)
                {
                    DrawTransform3DDetails(t3d, i);
                }
                else if (comp is MeshRenderer3D mr3d)
                {
                    DrawMeshRenderer3DDetails(mr3d, i);
                }
                else if (comp is Light3D light3d)
                {
                    DrawLight3DDetails(light3d, i);
                }
                else if (comp is RigidBody3D rb3d)
                {
                    DrawRigidBody3DDetails(rb3d, i);
                }
                else if (comp is CharacterController3D cc3d)
                {
                    DrawCharacterController3DDetails(cc3d, i);
                }
                else if (comp is SkeletalAnimator3D skelAnim)
                {
                    DrawSkeletalAnimator3DDetails(skelAnim, i);
                }
                else if (comp is ScriptComponent scriptComp)
                {
                    DrawScriptComponentDetails(scriptComp, i);
                }
                else if (comp is LightEngine.Core.Audio.AudioEmitter3D ae3d)
                {
                    DrawAudioEmitter3DDetails(ae3d, i);
                }
                else if (comp is Terrain3D terrain)
                {
                    DrawTerrain3DDetails(terrain, i);
                }
                else if (comp is LightEngine.Core.Particles.ParticleEmitter3D pe3d)
                {
                    DrawParticleEmitter3DDetails(pe3d, i);
                }

                ImGui.Spacing();
                if (ImGui.SmallButton($"Remove Component##Remove_{i}"))
                {
                    entity.RemoveComponent(comp);
                    _context.Log($"Component removed: {typeName}");
                    break;
                }
            }
        }
    }

    private static void DrawSpriteRendererDetails(SpriteRenderer sr)
    {
        // Renk Seçici (Color Picker)
        var c = sr.Color;
        NumVec4 colorVec = new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        if (ImGui.ColorEdit4("Color Tint", ref colorVec))
        {
            sr.Color = new Color(colorVec.X, colorVec.Y, colorVec.Z, colorVec.W);
        }

        // Katman Derinliği (Layer Depth)
        float depth = sr.LayerDepth;
        if (ImGui.SliderFloat("Layer Depth", ref depth, 0f, 1f, "%.2f"))
        {
            sr.LayerDepth = depth;
        }

        // Merkez Origin
        bool centerOrigin = sr.CenterOrigin;
        if (ImGui.Checkbox("Center Pivot", ref centerOrigin))
        {
            sr.CenterOrigin = centerOrigin;
        }

        if (sr.Texture != null)
        {
            ImGui.TextDisabled($"Texture: {sr.Texture.Width}x{sr.Texture.Height} px");
        }
    }

    private static void DrawBoxColliderDetails(BoxCollider2D box)
    {
        float w = box.Width;
        float h = box.Height;
        if (ImGui.DragFloat("Width##BoxW", ref w, 1f, 1f, 1000f)) box.Width = w;
        if (ImGui.DragFloat("Height##BoxH", ref h, 1f, 1f, 1000f)) box.Height = h;

        bool isTrigger = box.IsTrigger;
        if (ImGui.Checkbox("Is Trigger##BoxTrig", ref isTrigger)) box.IsTrigger = isTrigger;

        bool isStatic = box.IsStatic;
        if (ImGui.Checkbox("Is Static##BoxStat", ref isStatic)) box.IsStatic = isStatic;
    }

    private static void DrawCircleColliderDetails(CircleCollider2D circle)
    {
        float r = circle.Radius;
        if (ImGui.DragFloat("Radius##CircR", ref r, 1f, 1f, 500f)) circle.Radius = r;

        bool isTrigger = circle.IsTrigger;
        if (ImGui.Checkbox("Is Trigger##CircTrig", ref isTrigger)) circle.IsTrigger = isTrigger;

        bool isStatic = circle.IsStatic;
        if (ImGui.Checkbox("Is Static##CircStat", ref isStatic)) circle.IsStatic = isStatic;
    }

    private static void DrawParticleEmitterDetails(ParticleEmitter pe)
    {
        bool isEmitting = pe.IsEmitting;
        if (ImGui.Checkbox("Is Emitting##PeEmit", ref isEmitting)) pe.IsEmitting = isEmitting;

        float rate = pe.EmissionRate;
        if (ImGui.DragFloat("Rate (per sec)", ref rate, 5f, 1f, 500f)) pe.EmissionRate = rate;

        var startC = pe.StartColor;
        NumVec4 startVec = new(startC.R / 255f, startC.G / 255f, startC.B / 255f, startC.A / 255f);
        if (ImGui.ColorEdit4("Start Color##PeStart", ref startVec))
        {
            pe.StartColor = new Color(startVec.X, startVec.Y, startVec.Z, startVec.W);
        }

        if (ImGui.Button("Emit Burst (30)"))
        {
            pe.EmitBurst(30);
        }
    }

    private static void DrawAnimatorDetails(Animator2D anim)
    {
        float speed = anim.PlaybackSpeed;
        if (ImGui.DragFloat("Speed##AnimSpd", ref speed, 0.05f, 0f, 5f)) anim.PlaybackSpeed = speed;

        bool flipX = anim.FlipX;
        if (ImGui.Checkbox("Flip X##AnimFlipX", ref flipX)) anim.FlipX = flipX;

        bool flipY = anim.FlipY;
        if (ImGui.Checkbox("Flip Y##AnimFlipY", ref flipY)) anim.FlipY = flipY;

        if (anim.CurrentClip != null)
        {
            ImGui.TextColored(new NumVec4(0.4f, 0.9f, 0.5f, 1f), $"Clip: {anim.CurrentClip.Name} ({anim.CurrentFrameIndex + 1}/{anim.CurrentClip.Frames.Count})");
        }
    }

    private void DrawTransform3DDetails(Transform3D t3d, int idx)
    {
        var oldPos = t3d.LocalPosition;
        var oldRot = t3d.LocalRotationDegrees;
        var oldScale = t3d.LocalScale;

        NumVec3 pos = new(t3d.LocalPosition.X, t3d.LocalPosition.Y, t3d.LocalPosition.Z);
        if (ImGui.DragFloat3($"Position##T3DPos_{idx}", ref pos, 0.1f))
        {
            t3d.LocalPosition = new Vector3(pos.X, pos.Y, pos.Z);
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _context.RecordCommand(new Commands.Transform3DCommand(t3d, oldPos, oldRot, oldScale, t3d.LocalPosition, t3d.LocalRotationDegrees, t3d.LocalScale));
        }

        NumVec3 rot = new(t3d.LocalRotationDegrees.X, t3d.LocalRotationDegrees.Y, t3d.LocalRotationDegrees.Z);
        if (ImGui.DragFloat3($"Rotation##T3DRot_{idx}", ref rot, 1f))
        {
            t3d.LocalRotationDegrees = new Vector3(rot.X, rot.Y, rot.Z);
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _context.RecordCommand(new Commands.Transform3DCommand(t3d, oldPos, oldRot, oldScale, t3d.LocalPosition, t3d.LocalRotationDegrees, t3d.LocalScale));
        }

        NumVec3 scl = new(t3d.LocalScale.X, t3d.LocalScale.Y, t3d.LocalScale.Z);
        if (ImGui.DragFloat3($"Scale##T3DScl_{idx}", ref scl, 0.05f, 0.01f, 100f))
        {
            t3d.LocalScale = new Vector3(scl.X, scl.Y, scl.Z);
        }
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _context.RecordCommand(new Commands.Transform3DCommand(t3d, oldPos, oldRot, oldScale, t3d.LocalPosition, t3d.LocalRotationDegrees, t3d.LocalScale));
        }

        if (ImGui.SmallButton($"Reset 3D Transform##T3DRst_{idx}"))
        {
            _context.RecordCommand(new Commands.Transform3DCommand(t3d, oldPos, oldRot, oldScale, Vector3.Zero, Vector3.Zero, Vector3.One));
            t3d.LocalPosition = Vector3.Zero;
            t3d.LocalRotationDegrees = Vector3.Zero;
            t3d.LocalScale = Vector3.One;
        }
    }

    private static void DrawMeshRenderer3DDetails(MeshRenderer3D mr, int idx)
    {
        if (mr.Mesh != null)
        {
            ImGui.TextDisabled($"Mesh: {mr.Mesh.Name} ({mr.Mesh.PrimitiveCount} tris)");
        }

        // Hızlı şekil (Primitive) değiştirici
        int primIdx = (int)(mr.Mesh?.PrimitiveType ?? PrimitiveType3D.Cube);
        if (primIdx < 6 && ImGui.Combo($"Primitive##MRPrim_{idx}", ref primIdx, "Cube\0Sphere\0Plane\0Cylinder\0Cone\0Pyramid\0"))
        {
            if (Engine.Services.TryGet<IRender3DService>(out var r3d) && r3d != null)
            {
                mr.Mesh = r3d.GetPrimitiveMesh((PrimitiveType3D)primIdx);
            }
        }

        var c = mr.Material.DiffuseColor;
        NumVec4 col = new(c.R / 255f, c.G / 255f, c.B / 255f, mr.Material.Alpha);
        if (ImGui.ColorEdit4($"Albedo Color##MRCol_{idx}", ref col))
        {
            mr.Material.DiffuseColor = new Color(col.X, col.Y, col.Z, 1f);
            mr.Material.Alpha = col.W;
        }

        float specPower = mr.Material.SpecularPower;
        if (ImGui.SliderFloat($"Specular Power##MRSpec_{idx}", ref specPower, 1f, 128f))
        {
            mr.Material.SpecularPower = specPower;
        }

        float roughness = mr.Material.Roughness;
        if (ImGui.SliderFloat($"Roughness (PBR)##MRRough_{idx}", ref roughness, 0.05f, 1f))
        {
            mr.Material.Roughness = roughness;
        }

        float metallic = mr.Material.Metallic;
        if (ImGui.SliderFloat($"Metallic (PBR)##MRMet_{idx}", ref metallic, 0f, 1f))
        {
            mr.Material.Metallic = metallic;
        }

        bool lit = mr.Material.EnableLighting;
        if (ImGui.Checkbox($"Enable Lighting##MRLit_{idx}", ref lit)) mr.Material.EnableLighting = lit;

        ImGui.SameLine();
        bool wire = mr.Material.Wireframe;
        if (ImGui.Checkbox($"Wireframe##MRWire_{idx}", ref wire)) mr.Material.Wireframe = wire;

        if (mr.Material.LightmapTexture != null)
        {
            ImGui.TextColored(new NumVec4(0.4f, 0.9f, 0.6f, 1f), $"Lightmap Baked ({mr.Material.LightmapTexture.Width}x{mr.Material.LightmapTexture.Height})");
        }
    }

    private static void DrawRigidBody3DDetails(RigidBody3D rb, int idx)
    {
        int btIdx = (int)rb.BodyType;
        if (ImGui.Combo($"Body Type##RBBT_{idx}", ref btIdx, "Dynamic\0Kinematic\0Static\0"))
            rb.BodyType = (BodyType3D)btIdx;

        int shIdx = (int)rb.Shape;
        if (ImGui.Combo($"Collider Shape##RBSH_{idx}", ref shIdx, "Box\0Sphere\0Capsule\0"))
            rb.Shape = (ColliderShape3D)shIdx;

        float mass = rb.Mass;
        if (ImGui.DragFloat($"Mass (kg)##RBMass_{idx}", ref mass, 0.25f, 0.05f, 10000f))
            rb.Mass = mass;

        bool lockRot = rb.LockRotation;
        if (ImGui.Checkbox($"Lock Rotation##RBLock_{idx}", ref lockRot))
            rb.LockRotation = lockRot;

        ImGui.TextDisabled($"Velocity: ({rb.LinearVelocity.X:0.0}, {rb.LinearVelocity.Y:0.0}, {rb.LinearVelocity.Z:0.0})");
    }

    private static void DrawCharacterController3DDetails(CharacterController3D cc, int idx)
    {
        float spd = cc.MoveSpeed;
        if (ImGui.DragFloat($"Move Speed##CCSpd_{idx}", ref spd, 0.2f, 0.5f, 50f)) cc.MoveSpeed = spd;

        float sprint = cc.SprintMultiplier;
        if (ImGui.DragFloat($"Sprint Mult##CCSpr_{idx}", ref sprint, 0.05f, 1f, 5f)) cc.SprintMultiplier = sprint;

        float jump = cc.JumpVelocity;
        if (ImGui.DragFloat($"Jump Velocity##CCJmp_{idx}", ref jump, 0.2f, 1f, 30f)) cc.JumpVelocity = jump;

        ImGui.TextColored(cc.IsGrounded ? new NumVec4(0.3f, 0.9f, 0.4f, 1f) : new NumVec4(1f, 0.65f, 0.2f, 1f),
            cc.IsGrounded ? "Status: Grounded" : "Status: Airborne");
    }

    private static void DrawSkeletalAnimator3DDetails(SkeletalAnimator3D anim, int idx)
    {
        float spd = anim.PlaybackSpeed;
        if (ImGui.DragFloat($"Playback Speed##SKSpd_{idx}", ref spd, 0.05f, 0f, 5f)) anim.PlaybackSpeed = spd;

        bool playing = anim.IsPlaying;
        if (ImGui.Checkbox($"Is Playing##SKPlay_{idx}", ref playing)) anim.IsPlaying = playing;

        ImGui.TextDisabled($"Bones: {anim.Skeleton?.BoneCount ?? 0} | Clip: {anim.CurrentClip?.Name ?? "None"}");
        if (anim.IsBlending)
        {
            ImGui.TextColored(new NumVec4(0.4f, 0.85f, 1f, 1f), $"Blending: {anim.BlendFactor * 100f:0}%");
        }
    }

    private static void DrawLight3DDetails(Light3D light, int idx)
    {
        int ltIdx = (int)light.LightType;
        if (ImGui.Combo($"Light Type##LT_{idx}", ref ltIdx, "Directional\0Point\0Spot\0"))
        {
            light.LightType = (LightType3D)ltIdx;
        }

        var c = light.Color;
        NumVec3 col = new(c.R / 255f, c.G / 255f, c.B / 255f);
        if (ImGui.ColorEdit3($"Color##LCol_{idx}", ref col))
        {
            light.Color = new Color(col.X, col.Y, col.Z);
        }

        float intensity = light.Intensity;
        if (ImGui.DragFloat($"Intensity##LInt_{idx}", ref intensity, 0.05f, 0f, 5f)) light.Intensity = intensity;

        if (light.LightType != LightType3D.Directional)
        {
            float range = light.Range;
            if (ImGui.DragFloat($"Range##LRng_{idx}", ref range, 0.5f, 1f, 200f)) light.Range = range;
        }
    }

    private static void DrawAudioEmitter3DDetails(LightEngine.Core.Audio.AudioEmitter3D ae, int idx)
    {
        string path = ae.SoundPath;
        if (ImGui.InputText($"Sound Path##AESnd_{idx}", ref path, 128)) ae.SoundPath = path;

        float vol = ae.Volume;
        if (ImGui.SliderFloat($"Volume##AEVol_{idx}", ref vol, 0f, 1f, "%.2f")) ae.Volume = vol;

        float dist = ae.MaxDistance;
        if (ImGui.DragFloat($"Max Distance##AEDist_{idx}", ref dist, 1f, 1f, 200f)) ae.MaxDistance = dist;

        bool loop = ae.IsLooping;
        if (ImGui.Checkbox($"Is Looping##AELoop_{idx}", ref loop)) ae.IsLooping = loop;

        ImGui.SameLine();
        bool playStart = ae.PlayOnStart;
        if (ImGui.Checkbox($"Play on Start##AEStart_{idx}", ref playStart)) ae.PlayOnStart = playStart;

        if (ImGui.Button($"Play##AEPlay_{idx}")) ae.Play();
        ImGui.SameLine();
        if (ImGui.Button($"Stop##AEStop_{idx}")) ae.Stop();

        ImGui.SameLine();
        ImGui.TextDisabled(ae.IsPlaying ? "Playing (Spatial)" : "Stopped");
    }

    private static void DrawTerrain3DDetails(Terrain3D terrain, int idx)
    {
        float sx = terrain.SizeX;
        float sz = terrain.SizeZ;
        if (ImGui.DragFloat($"Width (X)##TerrX_{idx}", ref sx, 1f, 10f, 200f)) terrain.SizeX = sx;
        if (ImGui.DragFloat($"Length (Z)##TerrZ_{idx}", ref sz, 1f, 10f, 200f)) terrain.SizeZ = sz;

        float mh = terrain.MaxHeight;
        if (ImGui.DragFloat($"Max Height##TerrH_{idx}", ref mh, 0.2f, 0.5f, 50f)) terrain.MaxHeight = mh;

        float freq = terrain.Frequency;
        if (ImGui.DragFloat($"Frequency##TerrF_{idx}", ref freq, 0.005f, 0.01f, 0.5f)) terrain.Frequency = freq;

        int seed = terrain.Seed;
        if (ImGui.DragInt($"Random Seed##TerrS_{idx}", ref seed, 1, 0, 99999)) terrain.Seed = seed;

        int rx = terrain.ResolutionX;
        if (ImGui.SliderInt($"Resolution##TerrR_{idx}", ref rx, 8, 64))
        {
            terrain.ResolutionX = rx;
            terrain.ResolutionZ = rx;
        }

        ImGui.Spacing();
        if (ImGui.Button($"Regenerate Terrain Mesh##RegenTerr_{idx}"))
        {
            terrain.Regenerate();
        }
    }

    private static void DrawParticleEmitter3DDetails(LightEngine.Core.Particles.ParticleEmitter3D pe, int idx)
    {
        bool emit = pe.IsEmitting;
        if (ImGui.Checkbox($"Is Emitting##PEEmit_{idx}", ref emit)) pe.IsEmitting = emit;

        float rate = pe.EmissionRate;
        if (ImGui.DragFloat($"Emission Rate##PERate_{idx}", ref rate, 2f, 1f, 500f)) pe.EmissionRate = rate;

        float life = pe.ParticleLifetime;
        if (ImGui.DragFloat($"Lifetime (s)##PELife_{idx}", ref life, 0.1f, 0.1f, 10f)) pe.ParticleLifetime = life;

        float spd = pe.StartSpeed;
        if (ImGui.DragFloat($"Start Speed##PESpd_{idx}", ref spd, 0.2f, 0.1f, 50f)) pe.StartSpeed = spd;

        float sSize = pe.StartSize;
        float eSize = pe.EndSize;
        if (ImGui.DragFloat($"Start Size##PESSize_{idx}", ref sSize, 0.02f, 0.01f, 5f)) pe.StartSize = sSize;
        if (ImGui.DragFloat($"End Size##PEESize_{idx}", ref eSize, 0.02f, 0.01f, 5f)) pe.EndSize = eSize;

        ImGui.TextDisabled($"Active Particles: {pe.ActiveCount} / {pe.MaxParticles}");
    }


    /// <summary>
    /// Kullanıcı C# betiklerinin public alanlarını Reflection ile otomatik olarak Inspector'da düzenlenebilir yapar.
    /// </summary>
    private static void DrawScriptComponentDetails(ScriptComponent script, int idx)
    {
        ImGui.TextColored(new NumVec4(0.45f, 0.85f, 1f, 1f), $"C# Script: {script.GetType().Name}");
        var fields = script.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        if (fields.Length == 0)
        {
            ImGui.TextDisabled("No public fields exposed.");
            return;
        }

        foreach (var field in fields)
        {
            string label = $"{field.Name}##Scr_{idx}_{field.Name}";
            object? val = field.GetValue(script);

            if (field.FieldType == typeof(float) && val is float fv)
            {
                if (ImGui.DragFloat(label, ref fv, 0.25f)) field.SetValue(script, fv);
            }
            else if (field.FieldType == typeof(int) && val is int iv)
            {
                if (ImGui.DragInt(label, ref iv, 1f)) field.SetValue(script, iv);
            }
            else if (field.FieldType == typeof(bool) && val is bool bv)
            {
                if (ImGui.Checkbox(label, ref bv)) field.SetValue(script, bv);
            }
            else if (field.FieldType == typeof(string))
            {
                string sv = val as string ?? "";
                if (ImGui.InputText(label, ref sv, 128)) field.SetValue(script, sv);
            }
            else if (field.FieldType == typeof(Vector3) && val is Vector3 v3)
            {
                NumVec3 nv = new(v3.X, v3.Y, v3.Z);
                if (ImGui.DragFloat3(label, ref nv, 0.1f)) field.SetValue(script, new Vector3(nv.X, nv.Y, nv.Z));
            }
            else if (field.FieldType == typeof(Vector2) && val is Vector2 v2)
            {
                NumVec2 nv = new(v2.X, v2.Y);
                if (ImGui.DragFloat2(label, ref nv, 0.1f)) field.SetValue(script, new Vector2(nv.X, nv.Y));
            }
            else if (field.FieldType == typeof(Color) && val is Color c)
            {
                NumVec4 nv = new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
                if (ImGui.ColorEdit4(label, ref nv)) field.SetValue(script, new Color(nv.X, nv.Y, nv.Z, nv.W));
            }
        }
    }

    private void DrawAddComponentButton(IEntity entity)
    {
        if (ImGui.Button("+ Add Component", new NumVec2(-1, 26)))
        {
            ImGui.OpenPopup("AddComponentPopup");
        }

        if (ImGui.BeginPopup("AddComponentPopup"))
        {
            ImGui.TextDisabled("--- 2D Components ---");
            if (ImGui.MenuItem("SpriteRenderer (2D)"))
            {
                if (!entity.HasComponent<SpriteRenderer>())
                {
                    var assetService = Engine.Services.Get<IAssetService>();
                    var tex = assetService.CreateSolidTexture(32, 32, Color.White, "new_sprite");
                    entity.AddComponent(new SpriteRenderer(tex));
                    _context.Log($"Added SpriteRenderer to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("BoxCollider2D (2D Physics)"))
            {
                if (!entity.HasComponent<BoxCollider2D>())
                {
                    entity.AddComponent(new BoxCollider2D(32, 32));
                    _context.Log($"Added BoxCollider2D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("CircleCollider2D (2D Trigger)"))
            {
                if (!entity.HasComponent<CircleCollider2D>())
                {
                    entity.AddComponent(new CircleCollider2D(16));
                    _context.Log($"Added CircleCollider2D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("ParticleEmitter (Zero GC)"))
            {
                if (!entity.HasComponent<ParticleEmitter>())
                {
                    entity.AddComponent(new ParticleEmitter(300));
                    _context.Log($"Added ParticleEmitter to {entity.Name}");
                }
            }

            ImGui.Separator();
            ImGui.TextDisabled("--- 3D Components ---");

            if (ImGui.MenuItem("Transform3D"))
            {
                if (!entity.HasComponent<Transform3D>())
                {
                    entity.AddComponent(new Transform3D());
                    _context.Log($"Added Transform3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("MeshRenderer3D (Cube)"))
            {
                if (!entity.HasComponent<Transform3D>()) entity.AddComponent(new Transform3D());
                if (!entity.HasComponent<MeshRenderer3D>() && Engine.Services.TryGet<IRender3DService>(out var r3d) && r3d != null)
                {
                    entity.AddComponent(new MeshRenderer3D(r3d.GetPrimitiveMesh(PrimitiveType3D.Cube)));
                    _context.Is3DMode = true;
                    _context.Log($"Added MeshRenderer3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("Light3D (Directional/Point)"))
            {
                if (!entity.HasComponent<Transform3D>()) entity.AddComponent(new Transform3D());
                if (!entity.HasComponent<Light3D>())
                {
                    entity.AddComponent(new Light3D());
                    _context.Is3DMode = true;
                    _context.Log($"Added Light3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("RigidBody3D (BepuPhysics v2)"))
            {
                if (!entity.HasComponent<Transform3D>()) entity.AddComponent(new Transform3D());
                if (!entity.HasComponent<RigidBody3D>())
                {
                    entity.AddComponent(new RigidBody3D(BodyType3D.Dynamic, ColliderShape3D.Box, 1f));
                    _context.Log($"Added RigidBody3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("CharacterController3D (WASD + Jump)"))
            {
                if (!entity.HasComponent<Transform3D>()) entity.AddComponent(new Transform3D());
                if (!entity.HasComponent<CharacterController3D>())
                {
                    entity.AddComponent(new CharacterController3D());
                    _context.Log($"Added CharacterController3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("SkeletalAnimator3D (Humanoid Rig)"))
            {
                if (!entity.HasComponent<SkeletalAnimator3D>())
                {
                    entity.AddComponent(new SkeletalAnimator3D(Skeleton3D.CreateSimpleHumanoid()));
                    _context.Log($"Added SkeletalAnimator3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("AudioEmitter3D (Spatial Sound)"))
            {
                if (!entity.HasComponent<LightEngine.Core.Audio.AudioEmitter3D>())
                {
                    entity.AddComponent(new LightEngine.Core.Audio.AudioEmitter3D());
                    _context.Log($"Added AudioEmitter3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("Terrain3D (Heightmap)"))
            {
                if (!entity.HasComponent<Terrain3D>())
                {
                    entity.AddComponent(new Terrain3D());
                    _context.Log($"Added Terrain3D to {entity.Name}");
                }
            }

            if (ImGui.MenuItem("ParticleEmitter3D (Fire/Smoke FX)"))
            {
                if (!entity.HasComponent<LightEngine.Core.Particles.ParticleEmitter3D>())
                {
                    entity.AddComponent(new LightEngine.Core.Particles.ParticleEmitter3D());
                    _context.Log($"Added ParticleEmitter3D to {entity.Name}");
                }
            }

            ImGui.Separator();
            if (ImGui.BeginMenu("C# Scripts (Hot Reload)"))
            {
                if (Engine.Services.TryGet<IScriptService>(out var scriptSvc) && scriptSvc != null)
                {
                    if (scriptSvc.AvailableScriptNames.Count == 0)
                    {
                        ImGui.TextDisabled("No compiled scripts found.");
                    }
                    else
                    {
                        foreach (var scriptName in scriptSvc.AvailableScriptNames)
                        {
                            if (ImGui.MenuItem(scriptName))
                            {
                                var instance = scriptSvc.CreateScriptInstance(scriptName);
                                if (instance != null)
                                {
                                    entity.AddComponent(instance);
                                    _context.Log($"Attached C# Script '{scriptName}' to {entity.Name}");
                                }
                            }
                        }
                    }

                    ImGui.Separator();
                    if (ImGui.MenuItem("+ Create New Script (CustomBehavior.cs)"))
                    {
                        string path = scriptSvc.CreateScriptTemplate($"Script_{DateTime.Now:HHmmss}");
                        scriptSvc.ReloadScripts(_context.ActiveScene);
                        _context.Log($"Created & compiled script: {Path.GetFileName(path)}");
                    }
                }
                ImGui.EndMenu();
            }

            ImGui.EndPopup();
        }
    }
}
