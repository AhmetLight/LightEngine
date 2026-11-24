# Light Engine C# Scripting & Hot Reload Rehberi

Light Engine, Roslyn (`Microsoft.CodeAnalysis.CSharp`) ve .NET Collectible `AssemblyLoadContext` altyapısı sayesinde oyun veya editör çalışırken C# betiklerinizi anında yeniden derleyip güncelleyebilir (**Hot Reload**).

## 1. Yeni Bir Betik Yazmak
`Assets/Scripts/` klasörü içine `.cs` uzantılı bir dosya ekleyin (veya Editörde **Scripts > Create New C# Script** seçeneğini kullanın):

```csharp
using LightEngine.Core.Scripting;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace GameScripts;

public class PlayerMover : ScriptComponent
{
    // Public alanlar Inspector panelinde otomatik olarak düzenlenebilir!
    public float MoveSpeed = 5.0f;
    public float RotationSpeed = 90.0f;

    public override void OnStart()
    {
        // Nesne ilk oluşturulduğunda çalışır
    }

    public override void OnUpdate(float deltaTime)
    {
        if (Transform3D == null) return;

        if (Input.IsKeyDown(Keys.Left))
            Transform3D.Rotate(new Vector3(0, RotationSpeed * deltaTime, 0));
        if (Input.IsKeyDown(Keys.Right))
            Transform3D.Rotate(new Vector3(0, -RotationSpeed * deltaTime, 0));
    }
}
```

## 2. Hot Reload Nasıl Çalışır?
- `ScriptManager`, `Assets/Scripts` klasörünü `FileSystemWatcher` ile izler.
- Bir `.cs` dosyasını kaydedip Editöre döndüğünüzde (veya araç çubuğundaki **Hot Reload C#** butonuna bastığınızda):
  1. Tüm betikler bellekte yeni bir `Assembly` olarak derlenir.
  2. Sahnedeki mevcut betik bileşenlerinin `public` alan değerleri okunur.
  3. Eski bileşenler yeni sürümüyle değiştirilir ve değerler geri yüklenir.
  4. Eski `AssemblyLoadContext` bellekten güvenle boşaltılır (`Unload()`).

## 3. 3D Fizik ve İskelet Animasyon Kullanımı
```csharp
// BepuPhysics v2 katı cisim ve zıplama
var rb = Entity.GetComponent<LightEngine.Core.Physics3D.RigidBody3D>();
rb?.ApplyImpulse(new Vector3(0, 12f, 0));

// 3D İskelet Animasyon Cross-Fade geçişi
var anim = Entity.GetComponent<LightEngine.Core.Animation3D.SkeletalAnimator3D>();
anim?.CrossFade("Run", blendDuration: 0.2f);
```
