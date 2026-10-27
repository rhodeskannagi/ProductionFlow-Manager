using ProductionFlow.Api.Services;
using System.Text.Json;
var root=Path.Combine(Path.GetTempPath(),"pf-tests-"+Guid.NewGuid());Directory.CreateDirectory(root);int passed=0;
void Assert(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
async Task Reject(Func<Task> call,string name){try{await call();}catch(DomainException){Assert(true,name);return;}throw new Exception("Expected rejection: "+name);}
try
{
 var path=Path.Combine(root,"demo.json");var service=new DemoWorkspace(path);var ct=CancellationToken.None;var before=await service.Read(ct);
 Assert(before.Orders.Count==5,"Five synthetic orders");
 var p=await service.Preview(101,10,ct);Assert(p.CanProduce&&p.Materials.Single(x=>x.ProductId==4).Needed==25,"BOM calculation");
 foreach(var qty in new[]{0m,-1m,.5m,100001m})await Reject(async()=>{await service.Preview(101,qty,ct);},"Reject invalid quantity "+qty);
 await Reject(()=>service.Produce(101,new(121,Guid.NewGuid(),"Test"),ct),"Reject overproduction");
 Assert(JsonSerializer.Serialize(before)==JsonSerializer.Serialize(await service.Read(ct)),"Rejected booking does not mutate state");
 await Reject(()=>service.Produce(101,new(1,Guid.Empty,"Test"),ct),"Reject empty request ID");
 await Reject(()=>service.Produce(101,new(1,Guid.NewGuid(),""),ct),"Reject empty operator");
 var request=Guid.NewGuid();await service.Produce(101,new(10,request,"Test"),ct);
 var after=await service.Read(ct);Assert(after.Orders.Single(x=>x.Id==101).Produced==10&&after.Inventory.Single(x=>x.Id==1).OnHand==10&&after.Inventory.Single(x=>x.Id==4).OnHand==775,"Atomic inventory and order booking");
 await Reject(()=>service.Produce(101,new(10,request,"Test"),ct),"Reject duplicate request");
 var reopened=new DemoWorkspace(path);var saved=await reopened.Read(ct);Assert(saved.Runs.Count==1&&saved.Runs[0].RequestId==request,"Persistence across restart");
 await Reject(()=>reopened.Undo(saved.Runs[0].Id,new("short","Test"),ct),"Reject short reversal reason");
 await reopened.Undo(saved.Runs[0].Id,new("Yanlış miktar girildi","Test"),ct);
 var undone=await new DemoWorkspace(path).Read(ct);Assert(undone.Runs[0].ReversedAt!=null&&undone.Runs[0].Reason=="Yanlış miktar girildi","Reversal history persisted");
 Assert(undone.Inventory.SequenceEqual(before.Inventory)&&undone.Orders.SequenceEqual(before.Orders),"Reversal restores stock and order");
 await Reject(()=>reopened.Undo(saved.Runs[0].Id,new("Tekrar geri alma","Test"),ct),"Reject second reversal");
 Assert((await reopened.Check(ct)).Errors==0,"Integrity after production and reversal");
 await reopened.Produce(101,new(120,Guid.NewGuid(),"Test"),ct);Assert((await reopened.Read(ct)).Orders.Single(x=>x.Id==101).Status=="Kapandı","Order closes at planned quantity");
 var json=JsonSerializer.Serialize(await reopened.Read(ct),new JsonSerializerOptions(JsonSerializerDefaults.Web));Assert(json.Contains("\"stats\"")&&json.Contains("\"reversedRuns\""),"Web overview contract preserved");
 Assert(Csv.Cell("=SUM(A1)")=="\"'=SUM(A1)\""&&Csv.Cell("x\"y")=="\"x\"\"y\""&&Csv.Cell("  @cmd").StartsWith("\"'"),"CSV formula neutralization and quoting");
 var detached=await reopened.Read(ct);detached.Inventory.Clear();Assert((await reopened.Read(ct)).Inventory.Count==7,"Read returns isolated snapshot");
 var corrupt=Path.Combine(root,"broken.json");File.WriteAllText(corrupt,"broken");try{_=new DemoWorkspace(corrupt);throw new Exception("Corruption silently overwritten");}catch(JsonException){Assert(File.ReadAllText(corrupt)=="broken","Corrupt persistence is not overwritten");}
 var state=JsonSerializer.Deserialize<DemoState>(File.ReadAllText(path))!;var r=state.Data.Runs.First(x=>x.ReversedAt==null);var original=state.Movements.First(x=>x.Run==r.Id&&x.Delta<0);state.Movements[state.Movements.IndexOf(original)]=original with{Delta=original.Delta-1};File.WriteAllText(path,JsonSerializer.Serialize(state));Assert((await new DemoWorkspace(path).Check(ct)).Issues.Any(i=>i.Code=="BOM_CONSUMPTION"),"Real consumption discrepancy detected");
 var owned=new ConnectionProfile{Server="localhost",Database="ProductionFlowManager",WindowsAuthentication=false,UserName="test",Password="abc;Database=master;xyz"};
 var cs=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(owned.Build());Assert(cs.InitialCatalog=="ProductionFlowManager"&&cs.Password==owned.Password,"Connection builder escapes password delimiters");
 foreach(var invalid in new[]{"master","MegaData_Example","ProductionFlowDemo","ProductionFlow_x];DROP DATABASE master;--","ProductionFlow_"})
 {owned.Database=invalid;await Reject(()=>{owned.Validate();return Task.CompletedTask;},"Reject unowned database name: "+invalid);}
 owned.Database="ProductionFlowManager";
 var parser=new Microsoft.SqlServer.TransactSql.ScriptDom.TSql140Parser(true);
 parser.Parse(new StringReader(DatabaseBootstrap.SchemaSql),out var errors);Assert(errors.Count==0,"Owned schema parses for SQL Server 2017");
 Assert(!DatabaseBootstrap.SchemaSql.Contains("INSERT dbo.Products")&&!DatabaseBootstrap.SchemaSql.Contains("USE "),"Bootstrap schema contains no seed or database switching");
 await Reject(()=>{CatalogService.Amount(.00001m);return Task.CompletedTask;},"Reject unrepresentable stock precision");
 await Reject(()=>{CatalogService.Amount(-1,true);return Task.CompletedTask;},"Reject negative planned amount");
 await Reject(()=>{CatalogService.Text(" ",40,"Code");return Task.CompletedTask;},"Reject blank master data");
 if(OperatingSystem.IsWindows())
 {
  var protectedPath=Path.Combine(root,"profile.dpapi");var vault=new ProductionFlow.Desktop.ProfileStore(protectedPath);vault.Save(owned);
  Assert(vault.Load()!.Password==owned.Password&&vault.Load()!.UserName==owned.UserName,"Windows DPAPI profile roundtrip");
  Assert(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(protectedPath)).Contains(owned.Password),"No plaintext password in saved profile");
  var bytes=File.ReadAllBytes(protectedPath);bytes[^1]^=0x7F;File.WriteAllBytes(protectedPath,bytes);
  try{_=vault.Load();throw new Exception("Tampered ciphertext accepted");}catch(System.Security.Cryptography.CryptographicException){Assert(File.ReadAllBytes(protectedPath).SequenceEqual(bytes),"Tampered profile rejected and preserved");}
 }
 else Console.WriteLine("SKIP Windows DPAPI checks (requires Windows)");
 if(args.Contains("--sql-smoke"))await SqlSmoke.Run();
 Console.WriteLine($"{passed} checks passed.");
}
finally{Directory.Delete(root,true);}
