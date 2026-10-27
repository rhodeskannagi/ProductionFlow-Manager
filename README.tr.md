# ProductionFlow Manager

[English](README.md) | **Türkçe**

**Üretim emri, reçete, stok, güvenli geri alma ve veri tutarlılığı kontrolü**
.NET 10, SQL Server ve sade JavaScript ile geliştirilen üretim operasyonları projesi.

[![CI](https://github.com/rhodeskannagi/ProductionFlow-Manager/actions/workflows/ci.yml/badge.svg)](https://github.com/rhodeskannagi/ProductionFlow-Manager/actions/workflows/ci.yml) ![Sürüm](https://img.shields.io/badge/version-0.6.0-40577c) ![Veritabanı](https://img.shields.io/badge/database-SQL%20Server%202022-8f67c8) ![Lisans](https://img.shields.io/badge/license-MIT-4b7396)

> **Veritabanı kurmadan dene:** `ProductionFlow-Demo.html` dosyasını indirip Chrome veya Edge ile aç. Bu bağımsız tarayıcı demosu kurgu verilerle çevrimdışı çalışır. Şirket veritabanı, ERP erişim bilgisi veya gizli kayıt içermez.

![Üretim panosu](docs/dashboard-preview.png)

### Veri tutarlılığı tanılama

![Tespit edilen malzeme tüketim uyuşmazlığı](docs/integrity-preview.png)

## Windows masaüstü (v0.6.0)

C# WinForms uygulaması; otomatik SQL bağlantısı, boş ve bağımsız veritabanı kurulumu, Windows DPAPI ile şifrelenen bağlantı profili, ürün/stok/reçete/emir yönetimi, kalıcı çevrimdışı demo, üretim önizlemesi, gerekçeli geri alma, arama/filtreleme, stok/geçmiş görünümü ve CSV aktarımı sunar. API ile masaüstünün SQL iş mantığı `ProductionFlow.Core` içinde ortaktır.

**Kurulum ve kabul kontrolleri:** [Masaüstü kılavuzu](docs/DESKTOP.md). Windows iş akışı indirilebilir uygulama paketini üretir. Masaüstü demosu için SQL Server veya ayrı .NET kurulumu gerekmez.

## Proje neleri gösteriyor?

| Modül | Özellikler | Gösterilen beceriler |
|---|---|---|
| Üretim emirleri | Planlanan/üretilen miktar, emir durumu, filtreleme | ERP / MRP iş mantığı |
| Reçete | Kayıt öncesi malzeme ihtiyacını önizleme | SQL birleştirmeleri, doğrulamalar |
| Üretim kaydı | Tek işlem bütünlüğünde malzeme tüketimi ve mamul girişi | Transaction, izolasyon, kilitleme |
| Güvenli geri alma | Ters stok hareketleri, özgün geçmişin korunması | Denetlenebilirlik, kurtarma tasarımı |
| Veri kalitesi | Altı salt okunur tanılama kategorisi ve CSV aktarımı | SQL raporlama, sorun giderme |
| Demo arayüzü | Ekrana uyumlu, bağımsız, yerel durum saklama | HTML, CSS, JavaScript |
| Yazılım geliştirme | Tekrarlı isteği engelleyen anahtarlar, testler, CI | Hata önleyici kodlama, GitHub Actions |

Tarayıcı demosunun **Veri kalitesi** ekranında örnek reçete tüketim hatası oluşturabilir, hatayı tespit edip CSV raporuna aktarabilirsin. Bu işlem yalnızca **kurgu demo verilerini** etkiler. **Demoyu sıfırla** başlangıç durumuna döndürür.

Bağımsız SQL kurulumu ve güvenlik ayrıntıları: [Veritabanı kılavuzu](docs/DATABASE.md).

## Hızlı başlangıç: kurulum gerektirmez

1. **`ProductionFlow-Demo.html`** dosyasını Chrome/Edge ile aç.
2. Üretim kaydı, kalan miktar kontrolü ve geri alma geçmişini dene.
3. **Veri kalitesi → Örnek hata oluştur** ile tüketim uyuşmazlığı kontrolünü çalıştır.
4. Raporu CSV olarak dışa aktar veya kurgu verileri sıfırla.

Çevrimdışı tarayıcı demosu yalnızca yerel kurgu verilerini saklar. Ayrı kaynak dosyalarını sunmak için:

```powershell
cd src/ProductionFlow.Api/wwwroot
python -m http.server 5500
```

<http://localhost:5500> adresini aç.

## SQL Server ve .NET ile geliştirme

Gereksinimler: .NET 10 SDK, SQL Server 2022 (veya Docker Desktop), SSMS.

1. `.env.example` dosyasını `.env` adıyla kopyala; **yeni ve güçlü bir demo parolası** belirle.
2. SQL Server'ı başlat: `docker compose up -d sqlserver`
3. SSMS üzerinden `localhost,14333` adresine bağlan (`sa` ve belirlediğin parola). **`db/001_create_and_seed.sql`** dosyasını tamamen çalıştır. **Ayrı** bir `UretimAkisDemo` veritabanı oluşturur.
4. API'yi başlat: `docker compose up --build -d api`
5. <http://localhost:5080> adresini aç; **SQL SERVER BAĞLI** göstergesini kontrol et.

Bağımsız SQL tanılaması için SSMS'te `db/002_integrity_checks.sql` dosyasını çalıştır.

Docker olmadan geliştirme:

```powershell
cd src/ProductionFlow.Api
$env:ConnectionStrings__ProductionFlow="Server=localhost;Database=UretimAkisDemo;Integrated Security=True;TrustServerCertificate=True;"
dotnet run --urls http://localhost:5080
```

**Önemli:** Yalnızca izole demo veritabanı kullan. Web/API uygulamasında kimlik doğrulama ve rol tabanlı yetkilendirme bulunmaz; **canlı işletme kullanımı için hazır değildir**.

## HTTP API

| Yöntem | Adres | Amaç |
|---|---|---|
| GET | `/api/health` | Sağlık kontrolü |
| GET | `/api/overview` | Emirler, stok, geçmiş ve göstergeler |
| GET | `/api/integrity` | Salt okunur tutarlılık raporu |
| POST | `/api/orders/{id}/preview` | İhtiyaç kontrolü, `{ "quantity":5 }` |
| POST | `/api/orders/{id}/produce` | Atomik üretim, `{ "quantity":5, "requestId":"<uuid>", "operator":"Demo" }` |
| GET | `/api/runs/{id}/undo-preview` | Geri alma ön koşulları |
| POST | `/api/runs/{id}/undo` | Atomik ters kayıtlar, `{ "reason":"Yanlış miktar girildi", "operator":"Demo" }` |

## Veri tutarlılığı kuralları

- `ORDER_QUANTITY`: Emirdeki miktar, geri alınmamış üretim kayıtları toplamından farklı.
- `ORDER_STATUS`: Emir durumu, planlanan ve üretilen miktarla uyumsuz.
- `RUN_OUTPUT`: Mamul giriş hareketinin sayısı veya miktarı hatalı.
- `BOM_CONSUMPTION`: Hammadde tüketimi reçeteden farklı.
- `REVERSAL_LEDGER`: Geri alma hareketleri özgün hareketleri dengelemiyor.
- `INVALID_STOCK`: Negatif veya geçersiz stok bakiyesi.
- `UNDO_BLOCKED` *(uyarı)*: Geri alma için mamul stoku yetersiz; tek başına veri bozulması anlamına gelmez.

**Hesaplama kapsamı:** Tarayıcı/API demosunda açılış stoku, satın alma girişleri ve sevkiyatlar kapsam dışıdır. Yalnızca üretim hareketlerine bakarak kapanış stokunun tamamen mutabık olduğu iddia edilmez.

## Testler ve sürekli entegrasyon

```sh
npm test
npm run check
node scripts/build-standalone.mjs
dotnet build src/ProductionFlow.Api/ProductionFlow.Api.csproj
```

Geliştirme ortamında **16 Node testi** geçti: üretim, geri alma, tekrar engelleme, geçersiz girdiler, emir mutabakatı, reçete hataları, eksik girişler, ters hareketler ve CSV güvenliği. Tarayıcı demosunda üretim → geri alma → tutarlılık → CSV indirme akışı da denendi.

v0.6 için Windows CI üzerinde **40 temel kontrol, 36 lisans/kota/DPAPI kontrolü ve 12 SQL LocalDB senaryosu** geçti; iki Windows uygulaması da derlendi. Bu otomatik kontroller, Windows ekranlarında yapılacak manuel kabul testinin yerine geçmez. Docker kurulumu geliştirme ortamında test edilmedi.

## Tasarım ve sınırlamalar

- SQL yazma işlemlerinde `SERIALIZABLE`, satır kilitleri ve benzersiz `RequestId` kullanılır.
- Geri alma, özgün kayıtları korur ve eşit büyüklükte ters stok hareketleri oluşturur.
- Rapor uç noktası salt okunur toplulaştırılmış SQL çalıştırır; tarayıcı demosu benzer kontrolleri kurgu verilerde uygular.
- Bu portföy projesinde kullanıcı yetkilendirmesi, onay akışları, üretim ortamı sırları, gerçek ERP entegrasyonu, çok depolu stok ve harici muhasebe bağlantıları bulunmaz.

[İngilizce mimari açıklaması](docs/ARCHITECTURE.md) ve [İngilizce mühendislik incelemesi](docs/CASE_STUDY.md).

## Depo

[Kaynak kod ve derleme durumu](https://github.com/rhodeskannagi/ProductionFlow-Manager)

```sh
git clone https://github.com/rhodeskannagi/ProductionFlow-Manager.git
cd ProductionFlow-Manager
```

`.env` dosyasını yerelde tut; yapılandırma örneği için `.env.example` kullan.

## Gizlilik ve kaynak kod lisansı

Verilerin tamamı kurgudur. İşverene ait veritabanı, kaynak prosedür, hesap, sunucu adı, müşteri bilgisi veya gizli erişim bilgisi içermez. Kaynak kod **MIT lisanslıdır**.

Türkçe ASCII şema v2, mevcut kayıtların taşınması ve önerilen genişletmeler: [Üretim veri modeli](docs/URETIM_VERI_MODELI.md). Önceden kaydedilmiş veritabanı adları otomatik değiştirilmez.

## Çevrimdışı masaüstü lisansı (v0.6)

Anahtarsız masaüstü uygulaması demo açılır. **Windows kullanıcısı başına günlük toplam 5 üretim/geri alma işlemi** yapılabilir; haklar Türkiye saatiyle gece yarısında yenilenir. Gerçek SQL erişimi ve sınırsız işlemler için sahibinin imzaladığı cihaza bağlı çevrimdışı lisans gerekir. [Lisanslama, sınırlar ve yönetici aracı](docs/LISANSLAMA.md). Gizli imzalama anahtarı depoda veya müşteri paketinde bulunmaz.

Çevrimdışı denetim; yerel kayıtlar, saat veya uygulama değiştirilerek aşılabilir. Merkezi lisans iptali yoktur. Eski uygulamalar, ayrı web/API demosu ve herkese açık MIT kaynak kodu bu özellik tarafından kısıtlanmaz.
