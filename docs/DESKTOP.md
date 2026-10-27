# ProductionFlow Desktop 0.3.0

C# / .NET 10 WinForms uygulaması. Web arayüzüne ek olarak yerel Windows ekranları sunar. Web API ve masaüstü aynı `ProductionFlow.Core` SQL servislerini kullanır; API sunucusu masaüstü için gerekli değildir.

## Windows paketini çalıştırma

1. `ProductionFlow-Desktop-v0.3.0-win-x64.zip` dosyasını **tamamen bir klasöre çıkarın**.
2. `ProductionFlow/ProductionFlow.Desktop.exe` dosyasını açın. DLL dosyalarını ayırmayın.
3. Uygulama DEMO modunda açılır; SQL ve ayrıca .NET kurulumu gerekmez. Windows 10/11 x64 hedeflidir.
4. Bir emir seçin, miktar girin, **Önizle ve üret** → malzeme kontrolü → **Üretimi kaydet**.
5. **Üretim geçmişi** sekmesinden bir kayıt seçip gerekçeyle geri alın.
6. **Veri kalitesi** sekmesinde **Kontrolleri çalıştır** ile sonucu görün.

Paket bağımsız çalışacak .NET çalışma zamanını içerir. Kod imzalama sertifikası ve MSI yükleyici içermez. Bu ortamda Windows ekranları çalıştırılmadı; aşağıdaki kabul adımlarını test bilgisayarında tamamlayın.

## Masaüstü özellikleri

- Emir / stok kodu / ürün araması, tüm-açık-kapalı emir filtresi.
- Açık emir, kalan miktar, üretim ve geri alma göstergeleri.
- Reçete/malzeme önizlemesi, tam sayı miktar ve kalan miktar kontrolü.
- Kalıcı üretim geçmişi, gerekçeli ters stok hareketleri, işlem günlüğü.
- Stok listesi, veri kalitesi kontrolü, görünen tablonun CSV aktarımı.
- SQL sunucusundaki erişilebilir kullanıcı veritabanlarının listelenmesi.
- Windows veya SQL kimlik doğrulaması. Parolalar dosyaya kaydedilmez.
- Aynı kullanıcı için tek uygulama örneği; işlem sırasında çift tıklama ve kapanma engeli.

## Kalıcı demo

Sentetik beş emir, yedi stok, üç reçete; başlangıçta tüm emirlerin üretilen miktarı sıfırdır. Web demosundaki hazır geçmişten bağımsızdır.

`%LOCALAPPDATA%\ProductionFlow\desktop-demo-v1.json` içinde saklanır. Yeni durum önce geçici dosyaya yazılır, diske aktarılır ve hedef dosya atomik olarak değiştirilir. Yazma başarısızsa bellek durumu değiştirilmez. Bozuk dosya otomatik sıfırlanmaz; uygulama hata verir ve dosyayı korur. Veriyi taşımak/yedeklemek için uygulama kapalıyken bu dosyayı kopyalayın. JSON bir SQL yedeği veya kurumsal denetim kaydı değildir.

Demo ve SQL birbirinden ayrı çalışma alanlarıdır; aralarında otomatik veri aktarımı yapılmaz. SQL bağlantısındaki bir hata sessizce demo moduna geçirmez.

## SQL bağlantısı

1. SSMS ile `db/001_create_and_seed.sql` dosyasını ayrı test sunucusunda çalıştırın. Scriptin sabit hedefi **ProductionFlowDemo** veritabanıdır; masaüstü kurulum scriptini otomatik çalıştırmaz.
2. Masaüstünde **SQL bağlantısı** → sunucu/örnek adını girin.
3. Windows kimlik doğrulaması veya SQL kullanıcı/parolasını seçin.
4. **Veritabanlarını listele** → `ProductionFlowDemo` → **Bağlan**.
5. Yedi temel ProductionFlow tablosu kontrol edilir; sütun uyumluluğu ilk veri okumasında sınanır.

SQL bağlantısında şifreleme açıktır. **Yerel test sertifikasına güven** varsayılan olarak kapalıdır; yalnızca kendi izole test ortamınızın kendinden imzalı sertifikası için kullanın. Gerçek sunucuda geçerli sertifika kullanın.

**ERP/Mega veritabanları bu şemaya sahip değildir ve entegrasyon yapılmamıştır.** Uygulama `SipHarDet` / `StkHarDet` / `spYariMamul` çalıştırmaz. SQL yazımları mevcut projenin transaction ve ters hareket kurallarını kullanır. Operatör alanı bir kimlik doğrulama mekanizması değildir. Kullanıcı/rol yetkilendirmesi, reçete sürümleme ve kurumsal dağıtım eklenmeden canlı işletme kullanımı hedeflenmez.

Bir SQL üretimi sırasında bağlantı koparsa commit sonucu belirsiz olabilir. **Yenile** ile geçmişi kontrol edin. Aynı seçim/miktar/operatör tekrarında request ID korunur; mevcut sunucu kuralı tekrarlanan isteği reddeder. Uygulama kapatılıp açıldığında bu bekleyen istek kimliği korunmaz. Sonuç doğrulanmadan yeni üretim göndermeyin.

## Kaynaktan derleme

```powershell
dotnet build src/ProductionFlow.Desktop/ProductionFlow.Desktop.csproj -c Release
dotnet run --project tests/ProductionFlow.Core.Tests -c Release
dotnet publish src/ProductionFlow.Desktop/ProductionFlow.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/ProductionFlow
```

`.github/workflows/desktop.yml` Windows üzerinde derler, çekirdek kontrollerini çalıştırır ve çalıştırılabilir paketi workflow artifact olarak üretir. SQL sunucusu veya masaüstü ekran testi çalıştırmaz.

## Doğrulama — 27.10.2026

| Kontrol | Sonuç |
|---|---|
| C# demo ve sözleşme kontrolleri | 24 kontrol geçti |
| Web demo alan/veri kalitesi testleri | 16 test geçti |
| Web JavaScript sözdizimi | Geçti |
| Masaüstü Release derleme | 0 hata, 0 uyarı |
| API Release derleme | 0 hata, 0 uyarı |
| Gerçek SQL üretim/geri alma entegrasyonu | Bu sürümde çalıştırılmadı |
| Windows ekranları / DPI / etkileşim | Burada çalıştırılmadı |
| Docker image build | Burada çalıştırılmadı |

C# testleri: reçete hesabı, geçersiz miktarlar, fazla üretim reddi ve değişmezlik, boş operatör/istek kimliği, stok ve emir güncellemesi, tekrar isteği reddi, yeniden açma sonrası geçmiş, geri alma nedeni, ters işlem ve tekrar engeli, kapanış durumu, API JSON sözleşmesi, CSV formül güvenliği, bozuk dosyanın korunması ve gerçek tüketim sapmasının yakalanması.

### Windows kabul kontrolü

- [ ] ZIP'i çıkar, EXE'yi aç; DEMO görünür.
- [ ] Emir 101 için 10 adet önizle; alüminyum 25 kg, bağlantı elemanı 40 adet.
- [ ] Üret; emir 10, mamul stoğu 10 olur. Kapat/aç; geçmiş korunur.
- [ ] Geçmişten geri al; emir ve stoklar başlangıca döner. Tekrar geri alma reddedilir.
- [ ] Arama, açık/kapalı filtreleri ve her dolu sekmede CSV aktarımını dene.
- [ ] Veri kalitesi kontrolünde 0 hata gör.
- [ ] Ayrı SQL test veritabanına bağlan; üretim ve geri almayı SSMS'ten doğrula.
- [ ] SQL erişimi kesilince hata gösterilir, demo moduna kendiliğinden geçmez.
- [ ] %100 ve %150 Windows ölçeklendirmesinde ekranları kontrol et.
