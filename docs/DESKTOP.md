# ProductionFlow Desktop 0.5.0

İlk açılışta bağlantı kurulumu, sonraki açılışlarda otomatik SQL bağlantısı ve bağımsız veritabanı kurulumu için [Güncel veritabanı kılavuzu](DATABASE.md).

ZIP’i tamamen çıkarın ve `ProductionFlow.Desktop.exe` dosyasını açın. .NET çalışma zamanı pakete dahildir. Sunucu ve giriş bilgilerini yalnızca uygulamanın ilk kurulum ekranına girin. Varsayılan bağımsız veritabanı `UretimAkis` olarak oluşturulur. İlk veritabanı boştur; Yönetim sekmesinden ürün/stok, reçete ve emir tanımlayın.

SQL olmadan denemek için bağlantı penceresini kapatıp **Demo moduna geç** seçin. Demo verileri ayrı yerel JSON dosyasında saklanır ve SQL veritabanına otomatik aktarılmaz.

## Kaynaktan derleme

```powershell
dotnet build src/ProductionFlow.Desktop/ProductionFlow.Desktop.csproj -c Release
dotnet run --project tests/ProductionFlow.Core.Tests -c Release
dotnet publish src/ProductionFlow.Desktop/ProductionFlow.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts/ProductionFlow
```

## Windows kabul kontrolü

- [ ] İlk açılışta sunucu ve kimlik bilgileri girilir; UretimAkis oluşur.
- [ ] Uygulama kapatılıp açıldığında şifre sormadan aynı veritabanına bağlanır.
- [ ] Yönetimden hammadde, mamul, reçete ve emir oluşturulur.
- [ ] Üretim ve geri alma stok/defter miktarlarını doğru değiştirir.
- [ ] Veri kalitesi 0 hata gösterir; CSV aktarımı çalışır.
- [ ] SQL bağlantısı kesilince hata gösterilir, demo moduna kendiliğinden geçmez.
- [ ] %100/%150 Windows ölçeklendirmesi kontrol edilir.

Windows GUI etkileşimleri ve kullanıcının SQL sunucusu burada çalıştırılmadı. Paket kod imzalı değildir. Bağlantı sorunu sırasında commit sonucu belirsiz olabilir; üretimi tekrar göndermeden önce geçmişi yenileyip kontrol edin. Operatör adı bir kullanıcı/rol yetkilendirme mekanizması değildir.

Turkce sema v2, eski verilerle gecis ve onerilen genislemeler: [Uretim veri modeli](URETIM_VERI_MODELI.md). Eski kayitli veritabani adi otomatik degistirilmez.
