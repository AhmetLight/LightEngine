# Light Engine Katkı Rehberi (Contributing)

Light Engine'e katkıda bulunmak istediğiniz için teşekkürler! Motorun hafif, okunabilir ve modüler kalması için lütfen aşağıdaki kurallara uyun:

## Mimari İlkeler
1. **Proje Ayrımı:** Motor kodu yalnızca `LightEngine.Core` içinde, editör kodu `LightEngine.Editor` içinde, örnek oyun kodu ise `Game` içinde yer alır. `LightEngine.Core` hiçbir zaman editöre bağımlı olmamalıdır.
2. **Servis Odaklılık:** Yeni bir alt sistem (Render, Physics, Audio, AI, Networking vb.) eklerken önce arayüzünü (`I...Service`) tanımlayın ve `Engine.Services` üzerinden kaydedin.
3. **Allocation Bilinci:** `Update`, `FixedUpdate` ve `Draw` döngüleri içinde her karede gereksiz `new` tahsislerinden kaçının; yeniden kullanılabilir tamponlar veya `ObjectPool<T>` kullanın.
4. **Kod Stili:**
   - Sınıf, metot ve değişken isimleri **İngilizce** (`PascalCase` / `camelCase`).
   - XML dokümantasyon (`/// <summary>`) ve satır içi açıklamalar **Türkçe**.
   - Her yeni özellik için `LightEngine.Tests` altında birim/entegrasyon testi ekleyin.

## Pull Request Süreci
1. Depoyu fork edin ve yeni bir dal açın: `git checkout -b feature/yeni-ozellik`
2. Değişikliklerinizi yapın ve tüm testlerin geçtiğini doğrulayın:
   ```bash
   dotnet build
   dotnet test
   ```
3. Açıklayıcı bir commit mesajı ile Pull Request gönderin.
