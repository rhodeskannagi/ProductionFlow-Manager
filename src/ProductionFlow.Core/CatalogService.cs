using Microsoft.Data.SqlClient;
using System.Data;
namespace ProductionFlow.Api.Services;

public sealed class CatalogService(ProductionDb database)
{
 public Task<Overview> Read(CancellationToken ct)=>new ProductionService(database).GetOverviewAsync(ct);
 public static string Text(string? value,int max,string label)
 {
  var text=value?.Trim()??"";if(text.Length==0||text.Length>max)throw new DomainException($"{label}: 1-{max} karakter girin.");return text;
 }
 public static void Amount(decimal value,bool positive=false)
 {
  if(Math.Abs(value)>100000000||decimal.Round(value,4)!=value||(positive&&value<=0))throw new DomainException("Miktar en fazla 100 milyon ve 4 ondalık basamak olmalı.");
 }
 public async Task AddProduct(string sku,string name,string unit,string kind,decimal opening,string actor,CancellationToken ct)
 {
  sku=Text(sku,40,"Stok kodu");name=Text(name,120,"Ürün adı");unit=Text(unit,12,"Birim");actor=Text(actor,80,"Operatör");Amount(opening);if(opening<0||kind is not ("finished" or "material"))throw new DomainException("Ürün türü veya başlangıç stoğu geçersiz.");
  await Write(async(conn,tx)=>
  {
   var id=await ProductionDb.ScalarAsync<int>(conn,tx,"INSERT dbo.Urunler(StokKodu,Ad,Birim,Tur) OUTPUT INSERTED.UrunId VALUES(@sku,@name,@unit,@kind)",ct,("@sku",sku),("@name",name),("@unit",unit),("@kind",kind));
   await ProductionDb.ExecAsync(conn,tx,"INSERT dbo.StokBakiyeleri(UrunId,MevcutMiktar) VALUES(@id,@qty)",ct,("@id",id),("@qty",opening));
   if(opening!=0)await AdjustLog(conn,tx,id,opening,"İlk stok",actor,ct);
   await Audit(conn,tx,actor,"PRODUCT_CREATE",$"{sku} · {name} · ilk stok {opening}",ct);
  },ct);
 }
 public async Task AdjustStock(int product,decimal delta,string reason,string actor,CancellationToken ct)
 {
  Amount(delta);if(delta==0)throw new DomainException("Stok değişimi sıfır olamaz.");reason=Text(reason,300,"Gerekçe");if(reason.Length<8)throw new DomainException("Gerekçe en az 8 karakter olmalı.");actor=Text(actor,80,"Operatör");
  await Write(async(conn,tx)=>
  {
   if(await ProductionDb.ExecAsync(conn,tx,"UPDATE dbo.StokBakiyeleri SET MevcutMiktar=MevcutMiktar+@delta WHERE UrunId=@id AND MevcutMiktar+@delta>=0",ct,("@id",product),("@delta",delta))!=1)throw new DomainException("Ürün bulunamadı veya stok eksiye düşüyor.");
   await AdjustLog(conn,tx,product,delta,reason,actor,ct);await Audit(conn,tx,actor,"STOCK_ADJUST",$"Stok {product} · {delta} · {reason}",ct);
  },ct);
 }
 public async Task SetRecipe(int product,int material,decimal perUnit,string actor,CancellationToken ct)
 {
  Amount(perUnit);if(perUnit<0||product==material)throw new DomainException("Reçete seçimi geçersiz.");actor=Text(actor,80,"Operatör");
  await Write(async(conn,tx)=>
  {
   var kinds=await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT COUNT(*) FROM dbo.Urunler WHERE (UrunId=@p AND Tur='finished') OR (UrunId=@m AND Tur='material')",ct,("@p",product),("@m",material));
   if(kinds!=2)throw new DomainException("Bir mamul ve bir hammadde seçin.");
   if(await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT COUNT(*) FROM dbo.UretimKayitlari r JOIN dbo.UretimEmirleri o ON o.EmirId=r.EmirId WHERE o.UrunId=@p",ct,("@p",product))!=0)throw new DomainException("Bu mamulde üretim geçmişi var. Geçmiş reçeteyi değiştirmek yerine yeni mamul kodu oluşturun.");
   await ProductionDb.ExecAsync(conn,tx,"DELETE dbo.ReceteKalemleri WHERE UrunId=@p AND MalzemeId=@m",ct,("@p",product),("@m",material));
   if(perUnit>0)await ProductionDb.ExecAsync(conn,tx,"INSERT dbo.ReceteKalemleri(UrunId,MalzemeId,BirimTuketim) VALUES(@p,@m,@q)",ct,("@p",product),("@m",material),("@q",perUnit));
   await Audit(conn,tx,actor,"RECIPE_SET",$"Mamul {product} · hammadde {material} · birim tüketim {perUnit}",ct);
  },ct);
 }
 public async Task AddOrder(string code,int product,decimal planned,string actor,CancellationToken ct)
 {
  code=Text(code,24,"Emir kodu");actor=Text(actor,80,"Operatör");Amount(planned,true);if(decimal.Truncate(planned)!=planned)throw new DomainException("Planlanan miktar tam sayı olmalı.");
  await Write(async(conn,tx)=>
  {
   if(await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT COUNT(*) FROM dbo.Urunler WHERE UrunId=@id AND Tur='finished'",ct,("@id",product))!=1)throw new DomainException("Mamul seçin.");
   if(await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT COUNT(*) FROM dbo.ReceteKalemleri WHERE UrunId=@id",ct,("@id",product))==0)throw new DomainException("Önce mamul reçetesini tanımlayın.");
   await ProductionDb.ExecAsync(conn,tx,"INSERT dbo.UretimEmirleri(Kod,UrunId,PlanlananMiktar) VALUES(@code,@p,@q)",ct,("@code",code),("@p",product),("@q",planned));
   await Audit(conn,tx,actor,"ORDER_CREATE",$"{code} · mamul {product} · plan {planned}",ct);
  },ct);
 }
 async Task Write(Func<SqlConnection,SqlTransaction,Task> action,CancellationToken ct)
 {
  await using var conn=await database.OpenAsync(ct);await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  try
  {
   if(await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT COUNT(*) FROM dbo.UygulamaSemasi WHERE Id=1 AND UygulamaKimligi=@app AND Surum=@version",ct,("@app",DatabaseBootstrap.ApplicationId),("@version",DatabaseBootstrap.CurrentVersion))!=1)throw new DomainException("Bağımsız ProductionFlow veritabanı gerekli.");
   await ProductionDb.ExecAsync(conn,tx,"DECLARE @r INT; EXEC @r=sys.sp_getapplock @Resource='ProductionFlow.Catalog',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Catalog is busy',1;",ct);
   await action(conn,tx);await tx.CommitAsync(ct);
  }
  catch{await tx.RollbackAsync(CancellationToken.None);throw;}
 }
 static Task<int> AdjustLog(SqlConnection c,SqlTransaction t,int id,decimal delta,string reason,string actor,CancellationToken ct)=>ProductionDb.ExecAsync(c,t,"INSERT dbo.StokDuzeltmeleri(UrunId,MiktarDegisimi,Gerekce,Kullanici) VALUES(@id,@delta,@reason,@actor)",ct,("@id",id),("@delta",delta),("@reason",reason),("@actor",actor));
 static Task<int> Audit(SqlConnection c,SqlTransaction t,string actor,string action,string details,CancellationToken ct)=>ProductionDb.ExecAsync(c,t,"INSERT dbo.IslemGunlugu(Kullanici,Islem,Aciklama) VALUES(@actor,@action,@details)",ct,("@actor",actor),("@action",action),("@details",details));
}
