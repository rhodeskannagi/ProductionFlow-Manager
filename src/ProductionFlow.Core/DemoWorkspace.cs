using System.Text.Json;
namespace ProductionFlow.Api.Services;

public record Recipe(int Product, int Material, decimal PerUnit);
public record Movement(Guid Run, int Product, decimal Delta, bool Reversal);
public sealed record DemoState(Overview Data,List<Recipe> Recipes,List<Movement> Movements);

// A caller must hold the desktop's single-instance mutex for this per-user file.
// Changes are committed by atomic replacement before publishing the new in-memory state.
public sealed class DemoWorkspace : IWorkspace
{
 readonly string path;
 DemoState state;
 public DemoWorkspace(string path)
 {
  this.path=path;
  state=File.Exists(path)?JsonSerializer.Deserialize<DemoState>(File.ReadAllText(path))
      ??throw new InvalidDataException("Demo dosyası okunamadı."):Seed();
 }
 static DemoState Seed()
 {
  var now=DateTime.UtcNow;
  var products=new List<ProductView>{new(1,"YM-ALFA-120","Alfa Yarı Mamul Panel","adet","finished",0),new(2,"YM-BETA-090","Beta Montaj Profili","adet","finished",0),new(3,"YM-GAMA-200","Gama Gövde Modülü","adet","finished",0),new(4,"HM-ALU-6063","Alüminyum Profil","kg","material",800),new(5,"HM-BAG-001","Bağlantı Elemanı","adet","material",1500),new(6,"HM-CONTA-02","Sızdırmazlık Contası","m","material",220),new(7,"HM-LEV-03","Alüminyum Levha","m²","material",210)};
  var orders=new List<OrderView>();
  for(int i=0;i<5;i++) {var p=products[i%3];orders.Add(new(101+i,$"ÜE-26010{i+1}",p.Id,p.Name,p.Sku,new[]{120,80,50,32,65}[i],0,"Başladı",now));}
  return new(new(orders,products,[],[]),[new(1,4,2.5m),new(1,5,4),new(2,4,1.2m),new(2,6,.7m),new(3,7,1.8m),new(3,5,8)],[]);
 }
 static T Copy<T>(T obj)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj))!;
 void Save(DemoState next)
 {
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  string temp=path+".tmp";
  try {using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){JsonSerializer.Serialize(stream,next);stream.Flush(true);} File.Move(temp,path,true);state=next;}
  finally {if(File.Exists(temp)) File.Delete(temp);}
 }
 static void Quantity(decimal q){if(q<=0||q>100000||decimal.Truncate(q)!=q)throw new DomainException("Miktar 1-100000 arasında tam sayı olmalı.");}
 static string Actor(string? a){a=a?.Trim();if(string.IsNullOrEmpty(a)||a.Length>80)throw new DomainException("Operatör adı 1-80 karakter olmalı.");return a;}
 static string Status(decimal q,decimal p)=>q<=0?"Başladı":q>=p?"Kapandı":"Üretiliyor";
 public Task<Overview> Read(CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(Copy(state.Data));}
 public Task<ProductionPreview> Preview(int order,decimal quantity,CancellationToken ct)
 {
  ct.ThrowIfCancellationRequested();Quantity(quantity);
  var o=state.Data.Orders.SingleOrDefault(x=>x.Id==order)??throw new DomainException("Emir bulunamadı.");
  var mats=state.Recipes.Where(x=>x.Product==o.ProductId).Select(r=>{var p=state.Data.Inventory.Single(x=>x.Id==r.Material);return new MaterialView(p.Id,p.Sku,p.Name,p.Unit,r.PerUnit*quantity,p.OnHand,p.OnHand>=r.PerUnit*quantity);}).ToList();
  List<string> warnings=[];if(quantity>o.Planned-o.Produced)warnings.Add("Kalan miktar aşılıyor.");if(mats.Count==0)warnings.Add("Reçete bulunamadı.");if(mats.Any(m=>!m.Enough))warnings.Add("Hammadde yetersiz.");
  return Task.FromResult(new ProductionPreview(o.Id,o.Code,quantity,o.Planned-o.Produced,mats,warnings.Count==0,warnings));
 }
 public async Task Produce(int order,ProduceInput input,CancellationToken ct)
 {
  var actor=Actor(input.Operator);if(input.RequestId==Guid.Empty)throw new DomainException("İstek kimliği gerekli.");
  if(state.Data.Runs.Any(x=>x.RequestId==input.RequestId))throw new DomainException("Bu üretim isteği daha önce işlendi. Geçmişi kontrol edin.");
  var preview=await Preview(order,input.Quantity,ct);if(!preview.CanProduce)throw new DomainException(string.Join(" ",preview.Warnings));
  var next=Copy(state);var o=next.Data.Orders.Single(x=>x.Id==order);var id=Guid.NewGuid();var now=DateTime.UtcNow;
  var run=new RunView(id,input.RequestId,order,o.Code,o.ProductName,input.Quantity,now,null,null,actor);
  foreach(var m in preview.Materials)Move(next,id,m.ProductId,-m.Needed,false);
  Move(next,id,o.ProductId,input.Quantity,false);
  next.Data.Orders[next.Data.Orders.IndexOf(o)]=o with{Produced=o.Produced+input.Quantity,Status=Status(o.Produced+input.Quantity,o.Planned)};
  next.Data.Runs.Insert(0,run);next.Data.Audit.Insert(0,new(next.Data.Audit.Count+1,now,"PRODUCE",$"{o.Code} · {input.Quantity} adet · {actor}"));ct.ThrowIfCancellationRequested();Save(next);
 }
 static void Move(DemoState s,Guid run,int product,decimal delta,bool reversal)
 {
  var p=s.Data.Inventory.Single(x=>x.Id==product);if(p.OnHand+delta<0)throw new DomainException("Stok eksiye düşer.");
  s.Data.Inventory[s.Data.Inventory.IndexOf(p)]=p with{OnHand=p.OnHand+delta};s.Movements.Add(new(run,product,delta,reversal));
 }
 public Task<UndoPreview> UndoPreview(Guid id,CancellationToken ct)
 {
  ct.ThrowIfCancellationRequested();var r=state.Data.Runs.SingleOrDefault(x=>x.Id==id)??throw new DomainException("Kayıt bulunamadı.");
  var o=state.Data.Orders.Single(x=>x.Id==r.OrderId);var stock=state.Data.Inventory.Single(x=>x.Id==o.ProductId).OnHand;
  List<string> warnings=[];if(r.ReversedAt!=null)warnings.Add("İşlem zaten geri alınmış.");if(stock<r.Quantity)warnings.Add("Mamul stoğu yetersiz.");
  return Task.FromResult(new UndoPreview(id,o.Id,o.Code,r.Quantity,stock,warnings.Count==0,warnings));
 }
 public async Task Undo(Guid id,UndoInput input,CancellationToken ct)
 {
  var actor=Actor(input.Operator);var reason=input.Reason?.Trim()??"";if(reason.Length is <8 or >300)throw new DomainException("Gerekçe 8-300 karakter olmalı.");
  var preview=await UndoPreview(id,ct);if(!preview.CanUndo)throw new DomainException(string.Join(" ",preview.Warnings));
  var next=Copy(state);var r=next.Data.Runs.Single(x=>x.Id==id);var o=next.Data.Orders.Single(x=>x.Id==r.OrderId);
  foreach(var m in next.Movements.Where(x=>x.Run==id&&!x.Reversal).ToList())Move(next,id,m.Product,-m.Delta,true);
  var now=DateTime.UtcNow;next.Data.Runs[next.Data.Runs.IndexOf(r)]=r with{ReversedAt=now,Reason=reason};
  next.Data.Orders[next.Data.Orders.IndexOf(o)]=o with{Produced=o.Produced-r.Quantity,Status=Status(o.Produced-r.Quantity,o.Planned)};
  next.Data.Audit.Insert(0,new(next.Data.Audit.Count+1,now,"UNDO",$"{o.Code} · {r.Quantity} adet · {actor} · {reason}"));ct.ThrowIfCancellationRequested();Save(next);
 }
 public Task<IntegrityReport> Check(CancellationToken ct)
 {
  ct.ThrowIfCancellationRequested();List<IntegrityIssue> issues=[];
  void Add(string code,string reference,string detail)=>issues.Add(new("error",code,reference,detail));
  foreach(var o in state.Data.Orders){var q=state.Data.Runs.Where(r=>r.OrderId==o.Id&&r.ReversedAt==null).Sum(r=>r.Quantity);if(q!=o.Produced)Add("ORDER_QUANTITY",o.Code,"Emir ve geçmiş miktarı farklı.");if(o.Status!=Status(o.Produced,o.Planned))Add("ORDER_STATUS",o.Code,"Emir durumu tutarsız.");}
  foreach(var p in state.Data.Inventory.Where(p=>p.OnHand<0))Add("INVALID_STOCK",p.Sku,"Negatif stok.");
  foreach(var r in state.Data.Runs)
  {
   var o=state.Data.Orders.Single(x=>x.Id==r.OrderId);var moves=state.Movements.Where(m=>m.Run==r.Id).ToList();
   if(moves.Count(m=>!m.Reversal&&m.Product==o.ProductId)!=1||moves.Where(m=>!m.Reversal&&m.Product==o.ProductId).Sum(m=>m.Delta)!=r.Quantity)Add("RUN_OUTPUT",r.Id.ToString(),"Mamul hareketi tutarsız.");
   foreach(var rec in state.Recipes.Where(x=>x.Product==o.ProductId))if(moves.Where(m=>!m.Reversal&&m.Product==rec.Material).Sum(m=>m.Delta)!=-rec.PerUnit*r.Quantity)Add("BOM_CONSUMPTION",r.Id.ToString(),"Reçete tüketimi tutarsız.");
   foreach(var g in moves.GroupBy(m=>m.Product))if(r.ReversedAt!=null?g.Sum(m=>m.Delta)!=0:g.Any(m=>m.Reversal))Add("REVERSAL_LEDGER",r.Id.ToString(),"Ters hareket tutarsız.");
   if(r.ReversedAt==null&&state.Data.Inventory.Single(p=>p.Id==o.ProductId).OnHand<r.Quantity)issues.Add(new("warning","UNDO_BLOCKED",r.Id.ToString(),"Geri alma için mamul yetersiz."));
  }
  return Task.FromResult(new IntegrityReport(DateTime.UtcNow,state.Data.Orders.Count,state.Data.Runs.Count,issues,issues.Count(x=>x.Severity=="error"),issues.Count(x=>x.Severity=="warning")));
 }
}
