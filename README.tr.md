# Light Engine (Türkçe Dokümantasyon)

**Light Engine**, modern **C# (.NET 9)** ve **MonoGame (DesktopGL)** üzerine inşa edilmiş; düşük sistem gereksinimli bilgisayarlarda (entegre GPU + 4 GB RAM) akıcı çalışırken **Half-Life 2 (2004)** seviyesinde atmosferik 3D görsel kalite sunmayı hedefleyen açık kaynaklı bir **2D + 3D hibrit oyun motoru ve görsel editörüdür**.

---

## Öne Çıkan Özellikler

1. **Hibrit 2D + 3D Çekirdek**:
   - `Engine.Services` üzerinden arayüz tabanlı (Service Locator) modüler mimari.
   - Sabit 60 Hz zaman adımlı (Fixed Timestep) simülasyon döngüsü.
   - Sıfır çöp toplayıcı yükü (Zero-GC) için `ObjectPool<T>` bellek tamponları.

2. **3D Atmosferik Render Hattı**:
   - Blinn-Phong ve hafif PBR (Metallic/Roughness) malzeme modeli.
   - Cascaded Shadow Maps (CSM) ile dinamik gölgeler.
   - Statik ışıklar için düşük maliyetli Radiosity Lightmap desteği.
   - Bloom, FXAA, Tone Mapping ve SSAO (Screen-Space Ambient Occlusion) son işleme efektleri.
   - Prosedürel Terrain3D (Heightmap) ve 3D Billboard parçacık sistemi.

3. **3D Fizik (BepuPhysics v2.4 Entegrasyonu)**:
   - SIMD ve doğrudan bellek havuzları (BufferPool) ile ultra hızlı 3D katı cisim simülasyonu.
   - Karakter Kontrolcüsü (CharacterController3D): zemin yakalama, rampa tırmanma, zıplama.
   - Ekrandan 3D nesne seçimi için hızlı 3D Raycast.

4. **Roslyn C# Betikleme ve Canlı Hot Reload**:
   - `Assets/Scripts/` altındaki C# dosyaları çalışırken bellekte yeniden derlenir.
   - Public değişkenlerin değerleri kaybolmadan yeni sürüme aktarılır.
   - Inspector panelinde Reflection ile otomatik görsel düzenleme alanları oluşturulur.

5. **Çift Görünümlü (Scene & Game) Docking Editörü**:
   - **Scene Viewport**: Düzenleme, taşıma/döndürme/ölçekleme gizmoları, 3D serbest uçuş kamerası.
   - **Game Viewport**: Düzenlemenin tamamen kapalı olduğu saf oyun çalıştırma ekranı.
   - Play tuşuna basıldığında sahnenin JSON kopyası alınır ve doğrudan Game ekranına geçilir; Stop'a basıldığında sahne ilk düzenleme anına geri döner.

---

## Hızlı Başlangıç

### Gereksinimler
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- OpenGL 3.0+ destekleyen herhangi bir ekran kartı

### Projeyi Derleme ve Çalıştırma
```bash
# Depoyu klonlayın
git clone https://github.com/KULLANICI_ADINIZ/LightEngine.git
cd LightEngine

# Çözümü derleyin
dotnet build LightEngine.slnx -c Release

# Görsel Editörü başlatın
dotnet run --project LightEngine.Editor/LightEngine.Editor.csproj

# 9 Aşamalı doğrulama test paketini çalıştırın
dotnet run --project LightEngine.Tests -c Release
```

### Tek Tıkla Standalone Paketleme (.exe / Linux ELF)
```bash
# Windows tek dosya .exe üretimi
build.bat publish

# Linux tek dosya ELF üretimi
chmod +x build.sh && ./build.sh publish
```

---

## Lisans
Bu proje **MIT Lisansı** ile lisanslanmıştır.  
Ahmet Işık & Light Engine Katkıda Bulunanları tarafından geliştirilmiştir.
