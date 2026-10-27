# v0.6 — internetsiz lisans ve gunluk demo kotasi

## Musteriye verilecek paket

Yalniz `ProductionFlow-Desktop-v0.6.0-win-x64.zip` dagitilir. Ilk acilista SQL baglantisi kurulmaz, mevcut kayitli SQL profili de anahtarsiz kullanilmaz. Uygulama ornek verilerle demo acilir.

- Her Windows kullanicisi icin Turkiye saatiyle gunluk **5 basarili uretim veya geri alma** toplam hakki.
- 1 kaydetme = 1 islem; miktar/satir sayisi hak sayisini degistirmez. Geri alma da 1 haktir.
- Arama, listeleme, onizleme, veri kalitesi kontrolu ve demo CSV aktarimi ucretsizdir.
- Reddedilen/iptal edilen islemler hak tuketmez. Beklenmedik kapanma sirasinda ayrilan bir hak tuketilmis kalabilir.
- Haklar 00:00 UTC+03:00'te yenilenir. Uygulamayi kapatip acmak, demo gorunumune gecmek veya sadece demo veri dosyasini degistirmek kotayi sifirlamaz.
- Gercek SQL baglantisi, veritabani olusturma/guncelleme, urun/stok/recete/emir yonetimi icin gecerli tam surum lisansi zorunludur.

## Lisans verme

1. Musteri **Lisans / cihaz kodu** ekranindan cihaz kodunu kopyalayip sana iletir.
2. Sadece sana verilen `ProductionFlow-Lisans-Yonetici-v0.6.0.zip` paketini ayri klasore cikar. Bu paketi veya `owner-private.pem` dosyasini kimseyle paylasma, GitHub'a yukleme.
3. `ProductionFlow.LicenseIssuer.exe` dosyasini ac; musteri adini ve cihaz kodunu gir. Suresiz veya son kullanma tarihli lisans secip **Lisans anahtari uret** dugmesine bas.
4. Musteriye yalniz uretilen `PF1...` lisans metnini gonder. Musteri bunu uygulamadaki lisans ekranina yapistirir.
5. Aktivasyondan sonra tam surum acilir, varsa kayitli SQL profili kullanilir; yoksa ilk SQL kurulumu istenir. Tam surumun islem kotasi yoktur.

Bu bir web API anahtari degil, **RSA-3072 / SHA-256 / PSS imzali cevrimdisi lisans anahtaridir**. Cihaz kodu Windows kurulum kimligi ve Windows kullanici SID'sinin SHA-256 ozetidir; ham kimlikler gonderilmez. Windows yeniden kurulursa veya kullanici hesabi degisirse yeni kod icin yeni lisans gerekir. Son kullanma tarihi secilen gunun Turkiye saatiyle sonuna kadardir; suresi dolan anahtar SQL islemlerini acmaz.

## Anahtar ve kota saklama

Uygulamada yalniz acik dogrulama anahtari gomuludur. Gizli imzalama anahtari sahibin ayri yonetici paketindedir. Kaybolursa ayni yayinci adina yeni lisans uretilemez; guvenli yedegini sakla. Ele gecerse anahtari degistirip yeni uygulama ve lisanslar dagitmak gerekir.

Aktivasyon ve kota `%LOCALAPPDATA%/ProductionFlow/license-usage.dpapi` dosyasinda Windows DPAPI CurrentUser ile saklanir. Dosya bozulursa haklari otomatik sifirlamak yerine islem engellenir. Saat belirgin bicimde geri alinmissa islem engellenir. Yeni lisans dogrulanmadan mevcut lisansin uzerine yazilmaz. Imza ve cihaz kontrolu her lisansli islem oncesinde tekrarlanir. SQL parolasi ile lisans anahtari birbirinden ayridir.

## Cevrimdisi korumanin sinirlari

Bu model kasitli olarak internetsizdir: merkezi iptal, kesin guvenilir saat ve kullanici kimlik dogrulamasi yoktur. Yerel yonetici dosya yedegi geri yukleyerek, kota dosyasini silerek, saat ileri alarak veya baska Windows hesabi kullanarak deneme limitini asabilir. Ayni kullanici hesabinda calisan yazilim DPAPI verisine erisebilir. EXE/kaynak kod degisikligiyle istemci kontrolleri kaldirilabilir. Projenin acik kaynak/MIT durumu degistirilmedi; kaynak kodu derleyen bir kisinin bunu yapmasi teknik olarak engellenmez.

Limit **yalniz yeni v0.6 masaustu paketine** uygulanir. Onceki EXE'ler, ayri web demolar ve mevcut API bu degisiklikle uzaktan kisitlanmaz. Kaynak kodunu yayinlama izni ile tam surum lisans anahtarinin dagitimi ayri konulardir. Daha guclu ticari denetim istenirse sunucu tarafinda yetkilendirme gerekir.

## Testler ve manuel kabul

`dotnet run --project tests/ProductionFlow.Licensing.Tests -c Release`: imza/cihaz/sure, besinci-altinci islem, yeniden acilis, gece yarisi, basarisiz islem iadesi, saat geri alma, eszamanli islem limiti, depolama hatasi ve Windows DPAPI dosya degisikligi senaryolari. Windows CI ayrica mevcut SQL regresyonlarini calistirir ve iki Windows projesini derler. Gercek Windows ekran kabul testi ayridir.

Manuel kontrol: anahtarsiz ilk acilis demo; SQL/Yonetim engelli; 5 islemden sonra 6. engelli; yeniden acilista kota korunuyor; baska cihazin anahtari reddediliyor; dogru lisansla SQL ekrani aciliyor. Kisiye ozel yonetici paketinin musteri ZIP'inde olmadigini kontrol edin.
