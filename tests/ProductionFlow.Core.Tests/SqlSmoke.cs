using Microsoft.Data.SqlClient;
using ProductionFlow.Api.Services;

internal static class SqlSmoke
{
 public static async Task Run()
 {
  // This test owns only these freshly-created, randomly-named LocalDB databases.
  var tag=Guid.NewGuid().ToString("N");
  var profile=new ConnectionProfile{Server=@"(localdb)\MSSQLLocalDB",Database="ProductionFlow_Test_"+tag,TrustServerCertificate=true};
  var foreign=new ConnectionProfile{Server=profile.Server,Database="ProductionFlow_TestForeign_"+tag,TrustServerCertificate=true};
  var ct=CancellationToken.None;
  void Require(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("SQL PASS "+name);}
  async Task Reject(Func<Task> action,string name){try{await action();}catch(DomainException){Console.WriteLine("SQL PASS "+name);return;}throw new Exception("Expected rejection: "+name);}
  try
  {
   await using(var master=new SqlConnection(profile.Build("master")))
   {await master.OpenAsync(ct);await using var cmd=new SqlCommand($"CREATE DATABASE [{profile.Database}]",master);await cmd.ExecuteNonQueryAsync(ct);}
   await using(var legacy=new SqlConnection(profile.Build()))
   {
    await legacy.OpenAsync(ct);
    foreach(var batch in System.Text.RegularExpressions.Regex.Split(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"LegacySchema.sql")),@"^\s*GO\s*$",System.Text.RegularExpressions.RegexOptions.Multiline))
    {if(string.IsNullOrWhiteSpace(batch))continue;await using var cmd=new SqlCommand(batch,legacy);await cmd.ExecuteNonQueryAsync(ct);}
    await using var seed=new SqlCommand("INSERT dbo.Products(Sku,Name,Unit,Kind) VALUES('LEGACY','Preserved','kg','material'); INSERT dbo.StockBalances(ProductId,OnHand) VALUES(SCOPE_IDENTITY(),0);",legacy);await seed.ExecuteNonQueryAsync(ct);
   }
   await Task.WhenAll(DatabaseBootstrap.EnsureAsync(profile,ct),DatabaseBootstrap.EnsureAsync(profile,ct));
   Require((await new CatalogService(new ProductionDb(profile.Build())).Read(ct)).Inventory.Any(x=>x.Sku=="LEGACY"),"Version 1 migration preserves existing rows");
   var db=new ProductionDb(profile.Build());var catalog=new CatalogService(db);var service=new ProductionService(db);
   await using(var migrated=await db.OpenAsync(ct))
   {
    bool blocked=false;
    try{await ProductionDb.ExecAsync(migrated,null,"UPDATE dbo.StokBakiyeleri SET MevcutMiktar=-1",ct);}catch(SqlException ex) when(ex.Number==547){blocked=true;}
    Require(blocked,"Migrated check constraints still reject negative stock");
   }

   Require((await service.GetOverviewAsync(ct)).Orders.Count==0,"Concurrent bootstrap leaves an empty owned schema");
   await catalog.AddProduct("MAT","Raw material","kg","material",100,"Test",ct);
   await catalog.AddProduct("FIN","Finished product","adet","finished",0,"Test",ct);
   var initial=await catalog.Read(ct);var material=initial.Inventory.Single(x=>x.Sku=="MAT").Id;var product=initial.Inventory.Single(x=>x.Sku=="FIN").Id;
   await catalog.SetRecipe(product,material,2.5m,"Test",ct);await catalog.AddOrder("ORDER-1",product,10,"Test",ct);
   var order=(await catalog.Read(ct)).Orders.Single().Id;
   await service.ProduceAsync(order,new(4,Guid.NewGuid(),"Test"),ct);var booked=await catalog.Read(ct);
   Require(booked.Inventory.Single(x=>x.Id==material).OnHand==90,"SQL material consumption");
   await Reject(()=>catalog.SetRecipe(product,material,3,"Test",ct),"Historic BOM cannot change");
   await service.UndoAsync(booked.Runs[0].Id,new("Smoke test reversal","Test"),ct);
   await catalog.AdjustStock(material,15,"Warehouse receipt","Test",ct);
   Require((await catalog.Read(ct)).Inventory.Single(x=>x.Id==material).OnHand==115,"Reversal and stock adjustment");
   Require((await new IntegrityService(db).GetReportAsync(ct)).Errors==0,"Full inventory ledger reconciles");
   await DatabaseBootstrap.EnsureAsync(profile,ct);
   Require((await catalog.Read(ct)).Runs.Count==1,"Second startup preserves history");
   await using(var conn=await db.OpenAsync(ct))
   {
    await ProductionDb.ExecAsync(conn,null,"UPDATE dbo.StokBakiyeleri SET MevcutMiktar=MevcutMiktar+1 WHERE UrunId=@id",ct,("@id",material));
   }
   Require((await new IntegrityService(db).GetReportAsync(ct)).Issues.Any(i=>i.Code=="STOCK_LEDGER"),"Manual stock corruption is detected");
   await Task.WhenAll(DatabaseBootstrap.EnsureAsync(foreign,ct),DatabaseBootstrap.EnsureAsync(foreign,ct));
   Require((await new CatalogService(new ProductionDb(foreign.Build())).Read(ct)).Inventory.Count==0,"Concurrent fresh Turkish schema is empty");
   await using(var master=new SqlConnection(profile.Build("master")))
   {await master.OpenAsync(ct);SqlConnection.ClearAllPools();await using var cmd=new SqlCommand($"ALTER DATABASE [{foreign.Database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{foreign.Database}]; CREATE DATABASE [{foreign.Database}];",master);await cmd.ExecuteNonQueryAsync(ct);}

   await using(var conn=new SqlConnection(foreign.Build()))
   {
    await conn.OpenAsync(ct);await using var cmd=new SqlCommand("CREATE TABLE dbo.KeepMe(Id INT NOT NULL); INSERT dbo.KeepMe VALUES(42);",conn);await cmd.ExecuteNonQueryAsync(ct);
   }
   await Reject(()=>DatabaseBootstrap.EnsureAsync(foreign,ct),"Non-owned nonempty database is rejected");
   await using(var conn=new SqlConnection(foreign.Build()))
   {
    await conn.OpenAsync(ct);await using var cmd=new SqlCommand("SELECT Id FROM dbo.KeepMe",conn);Require(Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))==42,"Unrelated table remains unchanged");
   }
  }
  finally
  {
   SqlConnection.ClearAllPools();
   await using var master=new SqlConnection(profile.Build("master"));await master.OpenAsync();
   foreach(var target in new[]{profile.Database,foreign.Database})
   {
    if(!target.EndsWith(tag,StringComparison.Ordinal)||!target.StartsWith("ProductionFlow_Test",StringComparison.Ordinal))throw new Exception("Test cleanup guard");
    await using var cmd=new SqlCommand($"IF DB_ID(N'{target}') IS NOT NULL BEGIN ALTER DATABASE [{target}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{target}]; END",master);await cmd.ExecuteNonQueryAsync();
   }
  }
 }
}
