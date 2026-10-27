# Bağımsız ProductionFlow veritabanı — v0.4

## İlk açılış

Uygulama kayıtlı bağlantı varsa otomatik bağlanır. Kayıt yoksa tek seferlik kurulum ekranı açılır. Mevcut SQL Server örneğinizi, SQL kullanıcı/parolanızı veya Windows kimlik doğrulamasını seçin. Varsayılan veritabanı **ProductionFlowManager**. İlk kurulumda hesabın veritabanı oluşturma ve şema kurma yetkisi gerekir; uygulama kendisine yetki vermez ve SQL login oluşturmaz.

**Bağlan / kur ve şifreli kaydet**:

1. Mevcut hedef veritabanının uygulama kimliği ve şema sürümünü kontrol eder.
2. Hedef yoksa yalnızca o veritabanını oluşturur.
3. Boş veritabanına tabloları, kuralları ve indeksleri tek transaction içinde kurar.
4. Örnek veri eklemez. Yarım kurulumda şema transaction'ı geri alınır; oluşturulan boş veritabanı sonraki denemede tekrar kullanılabilir.
5. Başarılı bağlantıdan sonra giriş profilini Windows DPAPI ile şifreli kaydeder.

Normal sonraki açılışta şema oluşturma veya master üzerinde DDL gerekmez. Profildeki veritabanı silinmişse, hesap yetkili olduğu sürece yeniden boş kurulur. **Bu bir yedekten kurtarma değildir.** Erişim sorunu, sürüm uyuşmazlığı veya dolu yabancı veritabanı varsa kurulum durur; veri silinmez.

Adlar `ProductionFlowManager` veya `ProductionFlow_` öneki ve harf/rakam/alt çizgi ile sınırlıdır. Bu, sistem ve ERP veritabanlarının yanlış seçilmesini engeller. `dbo.AppSchema` içindeki uygulama kimliği ve sürüm ayrıca doğrulanır. Var olan v0.3 `ProductionFlowDemo` otomatik taşınmaz. Mega'dan tablo, prosedür, trigger veya veri alınmaz. Aynı SQL örneğinde barınmak CPU/RAM/disk kaynaklarının paylaşıldığı anlamına gelir; mantıksal veritabanı ayrılığı fiziksel sunucu ayrılığı değildir.

## Şifreli profil

Dosya: `%LOCALAPPDATA%\ProductionFlow\connection.dpapi`

Sunucu, veritabanı, kullanıcı adı ve parola dahil profilin tamamı `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)` ile korunur. Anahtar EXE içine gömülmez. Başka Windows hesabında dosyayı kopyalamak giriş sağlamaz; hesabın Windows profilini değiştirdiğinizde yeniden kurulum gerekebilir. Parola uygulama çalışırken SQL bağlantısı için bellekte açılır; aynı kullanıcı bağlamında çalışan zararlı yazılımlara karşı mutlak koruma değildir.

Parola sohbetten istenmez, kaynak koda eklenmez ve GitHub'a gönderilmez. Bozuk/değiştirilmiş profil otomatik silinmez; kullanıcı yeni ayarlarla başarıyla bağlanınca atomik olarak değiştirilir. SQL bağlantı şifrelemesi açıktır; sunucu sertifikası doğrulaması varsayılan olarak açıktır. Kendi yerel test sunucunuz kendinden imzalı sertifika kullanıyorsa kurulum ekranındaki ilgili seçeneği açıkça seçebilirsiniz.

## Boş sistemde çalışma sırası

**Yönetim** sekmesinde:

1. Hammaddeleri ve mamulleri **Ürün / ilk stok** ile tanımlayın.
2. **Reçete tanımla** ile her mamule hammadde ve birim tüketimini ekleyin. Aynı eşleşme güncellenir; 0 miktar ilgili satırı kaldırır.
3. **Yeni üretim emri** ile mamul, emir kodu ve planlanan tam sayı miktarını girin.
4. Üretim sekmesinde önizleyip kaydedin. Geçmişten gerekçeyle geri alın.
5. Sonraki stok giriş/çıkışlarını **Stok düzelt** ile gerekçeli kaydedin.

Üretim geçmişi olan mamulün reçetesi değiştirilemez; yeni ürün kodu kullanılır. Bu kural geçmiş tüketim kayıtlarının yeni reçeteye göre yanlış değerlendirilmesini önler. Fiziksel kayıt silme, geçmişi yeniden yazma, kullanıcı/rol yönetimi ve canlı ERP bağlantısı bu sürümün kapsamı değildir.

## Veri modeli ve optimizasyon

- Otomatik artan ürün/emir kimlikleri; tekil stok ve emir kodları.
- Miktar kontrolleri, foreign key'ler, tekrarlanan üretim isteği için tekil RequestId.
- `ProductionRuns` GUID anahtarı nonclustered; fiziksel sıralama `CreatedAt, RunId`.
- Emir/geri alma sorguları için `(OrderId, ReversedAt) INCLUDE (Quantity)`.
- Stok hareketleri için `(RunId, MovementType, ProductId) INCLUDE (Delta)`.
- Reçete hammadde ve emir ürün foreign key indeksleri.
- `InventoryAdjustments`: ilk stok ve gerekçeli düzeltme defteri.
- `STOCK_LEDGER`: stok bakiyesini üretim + ters hareket + ilk stok/düzeltme toplamıyla karşılaştırır.
- Tutarlılık raporu aynı SERIALIZABLE okuma transaction'ında çalışır; eşzamanlı yazma sırasında geçici tutarsız görünümü azaltır, fakat yazmaları bekletebilir.
- Kurulumda oturum kilidi, şemada transaction; aynı anda iki uygulama açılışı tekrarlı tablo kurmaz.

Bu değişiklikler sorgu şekillerine göre seçildi; kullanıcı sunucusunda süre/IO ölçümü yapılmadı. Sayısal hızlanma garantisi verilmez. SQL Server servis ayarları, instance belleği, diğer veritabanlarının indeksleri ve recovery ayarları değiştirilmez. Yedekleme politikası sunucu yöneticisinde kalır.

## Testler

C# kontrolleri; izin verilen hedef adları, bağlantı dizisi kaçışları, SQL Server 2017 sözdizimi ve mevcut demo kurallarını kapsar. Windows üzerinde DPAPI geri açma, şifreli dosyada açık parola olmaması ve değiştirilmiş ciphertext reddi test edilir. Windows CI içindeki SQL LocalDB testleri iki eşzamanlı ilk kurulum, boş şema, katalog/reçete/emir/üretim/geri alma, stok mutabakatı, ikinci açılışta geçmişin korunması ve yabancı veritabanının değiştirilmemesini sınar. Testler yalnızca rastgele adlandırılan kendi LocalDB veritabanlarını oluşturur ve temizler; gerçek şirket sunucusuna bağlanmaz.

Kendi sunucunuzdaki ilk bağlantı/kurulum ve Windows ekran kabul testi ayrıca yapılmalıdır.

## Teknik kaynaklar

- [Windows DPAPI](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)
- [CREATE DATABASE ve autocommit](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-database-transact-sql)
- [SQL uygulama kilidi](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql)
