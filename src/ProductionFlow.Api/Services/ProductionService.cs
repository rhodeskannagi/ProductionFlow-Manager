using Microsoft.Data.SqlClient;
using System.Data;

namespace ProductionFlow.Api.Services;

public sealed record OrderView(int Id,string Code,int ProductId,string ProductName,string Sku,decimal Planned,decimal Produced,string Status,DateTime CreatedAt);
public sealed record ProductView(int Id,string Sku,string Name,string Unit,string Kind,decimal OnHand);
public sealed record RunView(Guid Id,Guid RequestId,int OrderId,string OrderCode,string ProductName,decimal Quantity,DateTime CreatedAt,DateTime? ReversedAt,string? Reason,string Operator);
public sealed record AuditView(long Id,DateTime At,string Action,string Details);
public sealed record MaterialView(int ProductId,string Sku,string Name,string Unit,decimal Needed,decimal Available,bool Enough);
public sealed record ProductionPreview(int OrderId,string OrderCode,decimal Quantity,decimal Remaining,List<MaterialView> Materials,bool CanProduce,List<string> Warnings);
public sealed record UndoPreview(Guid RunId,int OrderId,string OrderCode,decimal Quantity,decimal FinishedAvailable,bool CanUndo,List<string> Warnings);
internal sealed record OrderRow(int Id,string Code,int ProductId,decimal Planned,decimal Produced);
internal sealed record OriginalRun(Guid Id,int OrderId,decimal Quantity,DateTime? ReversedAt,string OrderCode,int ProductId,decimal Produced,decimal Planned);
internal sealed record LedgerRow(int ProductId,decimal Delta);

public sealed class ProductionService(ProductionDb database)
{
    public async Task<object> GetOverviewAsync(CancellationToken ct)
    {
        await using var conn = await database.OpenAsync(ct);
        var orders=new List<OrderView>(); var inventory=new List<ProductView>();var runs=new List<RunView>();var audit=new List<AuditView>();
        await using(var cmd=ProductionDb.Command(conn,null,"""
          SELECT o.OrderId,o.Code,o.ProductId,p.Name,p.Sku,o.Planned,o.Produced,o.Status,o.CreatedAt
          FROM dbo.Orders o JOIN dbo.Products p ON p.ProductId=o.ProductId ORDER BY o.OrderId;
          """))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) orders.Add(new(reader.GetInt32(0),reader.GetString(1),reader.GetInt32(2),reader.GetString(3),reader.GetString(4),reader.GetDecimal(5),reader.GetDecimal(6),reader.GetString(7),Utc(reader.GetDateTime(8))));
        await using(var cmd=ProductionDb.Command(conn,null,"""
          SELECT p.ProductId,p.Sku,p.Name,p.Unit,p.Kind,b.OnHand
          FROM dbo.Products p JOIN dbo.StockBalances b ON p.ProductId=b.ProductId ORDER BY p.ProductId;
          """))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) inventory.Add(new(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetDecimal(5)));
        await using(var cmd=ProductionDb.Command(conn,null,"""
          SELECT r.RunId,r.RequestId,r.OrderId,o.Code,p.Name,r.Quantity,r.CreatedAt,r.ReversedAt,r.Reason,r.Operator
          FROM dbo.ProductionRuns r JOIN dbo.Orders o ON o.OrderId=r.OrderId JOIN dbo.Products p ON p.ProductId=o.ProductId
          ORDER BY r.CreatedAt DESC,r.RunId DESC;
          """))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) runs.Add(new(reader.GetGuid(0),reader.GetGuid(1),reader.GetInt32(2),reader.GetString(3),reader.GetString(4),reader.GetDecimal(5),Utc(reader.GetDateTime(6)),reader.IsDBNull(7)?null:Utc(reader.GetDateTime(7)),reader.IsDBNull(8)?null:reader.GetString(8),reader.GetString(9)));
        await using(var cmd=ProductionDb.Command(conn,null,"""
          SELECT TOP (150) AuditId,OccurredAt,Action,Details FROM dbo.AuditEvents ORDER BY AuditId DESC;
          """))
        await using(var reader=await cmd.ExecuteReaderAsync(ct))
            while(await reader.ReadAsync(ct)) audit.Add(new(reader.GetInt64(0),Utc(reader.GetDateTime(1)),reader.GetString(2),reader.GetString(3)));
        return new
        {
            orders,inventory,runs,audit,
            stats=new {orders=orders.Count,active=orders.Count(o=>o.Status!="Kapandı"),completed=orders.Count(o=>o.Status=="Kapandı"),produced=orders.Sum(o=>o.Produced),reversedRuns=runs.Count(r=>r.ReversedAt!=null)}
        };
    }

    public async Task<ProductionPreview> PreviewProductionAsync(int orderId,decimal quantity,CancellationToken ct)
    {
        CheckQuantity(quantity);
        await using var conn=await database.OpenAsync(ct);
        var order=await GetOrderAsync(conn,null,orderId,false,ct) ?? throw new DomainException("Üretim emri bulunamadı.");
        var mats=await GetMaterialsAsync(conn,null,order.ProductId,quantity,false,ct);
        return CreatePreview(order,quantity,mats);
    }
    public async Task<object> ProduceAsync(int orderId,ProduceInput input,CancellationToken ct)
    {
        CheckQuantity(input.Quantity);
        if(input.RequestId==Guid.Empty) throw new DomainException("Tekil requestId gerekli.");
        var actor=ValidateActor(input.Operator);
        await using var conn=await database.OpenAsync(ct);
        await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var existing=await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT COUNT(1) FROM dbo.ProductionRuns WITH (UPDLOCK,HOLDLOCK) WHERE RequestId=@id",ct,("@id",input.RequestId));
            if(existing!=0) throw new DomainException("Bu üretim isteği daha önce işlendi.");
            var order=await GetOrderAsync(conn,tx,orderId,true,ct) ?? throw new DomainException("Üretim emri bulunamadı.");
            var materials=await GetMaterialsAsync(conn,tx,order.ProductId,input.Quantity,true,ct);
            var preview=CreatePreview(order,input.Quantity,materials);
            if(!preview.CanProduce) throw new DomainException(string.Join(" ",preview.Warnings));
            var runId=Guid.NewGuid();
            await ProductionDb.ExecAsync(conn,tx,"""
                INSERT dbo.ProductionRuns(RunId,RequestId,OrderId,Quantity,Operator)
                VALUES(@run,@request,@order,@qty,@actor)
                """,ct,("@run",runId),("@request",input.RequestId),("@order",orderId),("@qty",input.Quantity),("@actor",actor));
            foreach(var m in materials.OrderBy(x=>x.ProductId))
            {
                await UpdateStockAsync(conn,tx,m.ProductId,-m.Needed,ct);
                await AddMovementAsync(conn,tx,runId,m.ProductId,-m.Needed,'C',ct);
            }
            await UpdateStockAsync(conn,tx,order.ProductId,input.Quantity,ct);
            await AddMovementAsync(conn,tx,runId,order.ProductId,input.Quantity,'P',ct);
            var next=order.Produced+input.Quantity;
            await ProductionDb.ExecAsync(conn,tx,"UPDATE dbo.Orders SET Produced=@qty,Status=@status WHERE OrderId=@id",ct,("@qty",next),("@status",GetStatus(next,order.Planned)),("@id",order.Id));
            await ProductionDb.ExecAsync(conn,tx,"INSERT dbo.AuditEvents(Actor,Action,RunId,Details) VALUES(@actor,N'PRODUCE',@run,@info)",ct,("@actor",actor),("@run",runId),("@info",$"{order.Code} · {input.Quantity} adet üretildi"));
            await tx.CommitAsync(ct);
            return new {id=runId,orderCode=order.Code,quantity=input.Quantity,produced=next,status=GetStatus(next,order.Planned)};
        }
        catch { await tx.RollbackAsync(CancellationToken.None);throw; }
    }
    public async Task<UndoPreview> PreviewUndoAsync(Guid runId,CancellationToken ct)
    {
        await using var conn=await database.OpenAsync(ct);
        var run=await GetRunAsync(conn,null,runId,false,ct)?? throw new DomainException("Üretim kaydı bulunamadı.");
        var stock=await GetStockAsync(conn,null,run.ProductId,false,ct);
        return CreateUndoPreview(run,stock);
    }
    public async Task<object> UndoAsync(Guid runId,UndoInput input,CancellationToken ct)
    {
        var reason=input.Reason?.Trim()??"";
        if(reason.Length is < 8 or > 300) throw new DomainException("Geri alma nedeni 8-300 karakter olmalı.");
        var actor=ValidateActor(input.Operator);
        await using var conn=await database.OpenAsync(ct);
        await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            // Lock order before run to preserve consistent lock acquisition against production.
            var orderId=await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT OrderId FROM dbo.ProductionRuns WHERE RunId=@id",ct,("@id",runId));
            if(orderId==0) throw new DomainException("Üretim kaydı bulunamadı.");
            _=await GetOrderAsync(conn,tx,orderId,true,ct)??throw new DomainException("Üretim emri bulunamadı.");
            var run=await GetRunAsync(conn,tx,runId,true,ct)??throw new DomainException("Üretim kaydı bulunamadı.");
            var movements=await GetOriginalMovementsAsync(conn,tx,runId,ct);
            if(movements.Count==0) throw new DomainException("Orijinal stok hareketleri bulunamadı.");
            if(movements.Count(m=>m.Delta>0)!=1||movements.Where(m=>m.Delta>0).Sum(m=>m.Delta)!=run.Quantity)
                throw new DomainException("Orijinal mamul giriş miktarı tutarsız.");
            // Lock all affected stock balances in the same ascending ProductId order.
            var stock =new Dictionary<int,decimal>();
            foreach(var m in movements.OrderBy(x=>x.ProductId)) stock[m.ProductId]=await GetStockAsync(conn,tx,m.ProductId,true,ct);
            var preview=CreateUndoPreview(run,stock[run.ProductId]);
            if(!preview.CanUndo) throw new DomainException(string.Join(" ",preview.Warnings));
            if(run.Produced<run.Quantity) throw new DomainException("Emir üretilen miktarı tutarsız.");
            foreach(var m in movements.OrderBy(x=>x.ProductId))
            {
                if(stock[m.ProductId]-m.Delta<0) throw new DomainException("Ters işlemde stok eksiye düşer.");
                await UpdateStockAsync(conn,tx,m.ProductId,-m.Delta,ct);
                await AddMovementAsync(conn,tx,runId,m.ProductId,-m.Delta,'U',ct);
            }
            var next=run.Produced-run.Quantity;
            await ProductionDb.ExecAsync(conn,tx,"UPDATE dbo.Orders SET Produced=@qty,Status=@status WHERE OrderId=@id",ct,("@qty",next),("@status",GetStatus(next,run.Planned)),("@id",run.OrderId));
            await ProductionDb.ExecAsync(conn,tx,"UPDATE dbo.ProductionRuns SET ReversedAt=SYSUTCDATETIME(),Reason=@reason,ReversedBy=@actor WHERE RunId=@id AND ReversedAt IS NULL",ct,("@reason",reason),("@actor",actor),("@id",run.Id));
            await ProductionDb.ExecAsync(conn,tx,"INSERT dbo.AuditEvents(Actor,Action,RunId,Details) VALUES(@actor,N'UNDO',@id,@details)",ct,("@actor",actor),("@id",run.Id),("@details",$"{run.OrderCode} · {run.Quantity} adet geri alındı · {reason}"));
            await tx.CommitAsync(ct);
            return new {runId,orderCode=run.OrderCode,reversedQuantity=run.Quantity,produced=next,status=GetStatus(next,run.Planned)};
        }
        catch { await tx.RollbackAsync(CancellationToken.None);throw; }
    }

    private static void CheckQuantity(decimal quantity)
    {
        if(quantity<=0||quantity>100000||decimal.Truncate(quantity)!=quantity) throw new DomainException("Üretim miktarı 1-100000 arasında tam sayı olmalı.");
    }
    private static string ValidateActor(string? actor)
    {
        actor=actor?.Trim();if(string.IsNullOrWhiteSpace(actor)) return "Demo Operatör";
        if(actor.Length>80) throw new DomainException("Operatör adı çok uzun.");return actor;
    }
    private static DateTime Utc(DateTime d)=>DateTime.SpecifyKind(d,DateTimeKind.Utc);
    private static string GetStatus(decimal qty,decimal planned)=>qty<=0?"Başladı":qty>=planned?"Kapandı":"Üretiliyor";
    private static ProductionPreview CreatePreview(OrderRow o,decimal qty,List<MaterialView> materials)
    {
        var remaining=o.Planned-o.Produced;var warnings=new List<string>();
        if(qty>remaining) warnings.Add($"Kalan üretim miktarı {remaining} adet.");
        if(materials.Count==0) warnings.Add("Reçete bulunamadı.");
        if(materials.Any(m=>!m.Enough)) warnings.Add("Hammadde stoku yetersiz.");
        return new(o.Id,o.Code,qty,remaining,materials,warnings.Count==0,warnings);
    }
    private static UndoPreview CreateUndoPreview(OriginalRun run,decimal stock)
    {
        var warnings=new List<string>();
        if(run.ReversedAt!=null) warnings.Add("Bu üretim zaten geri alınmış.");
        if(stock<run.Quantity) warnings.Add($"Mamul stoğu yetersiz: {stock} adet, gerekli {run.Quantity} adet.");
        return new(run.Id,run.OrderId,run.OrderCode,run.Quantity,stock,warnings.Count==0,warnings);
    }
    private static async Task<OrderRow?> GetOrderAsync(SqlConnection conn,SqlTransaction? tx,int id,bool locked,CancellationToken ct)
    {
        var sql=$"SELECT OrderId,Code,ProductId,Planned,Produced FROM dbo.Orders {(locked?"WITH (UPDLOCK,HOLDLOCK)":"")} WHERE OrderId=@id";
        await using var cmd=ProductionDb.Command(conn,tx,sql,("@id",id));
        await using var rd=await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct)?new(rd.GetInt32(0),rd.GetString(1),rd.GetInt32(2),rd.GetDecimal(3),rd.GetDecimal(4)):null;
    }
    private static async Task<List<MaterialView>> GetMaterialsAsync(SqlConnection conn,SqlTransaction? tx,int productId,decimal qty,bool locked,CancellationToken ct)
    {
        var sql=$"""
            SELECT r.MaterialId,p.Sku,p.Name,p.Unit,r.PerUnit,b.OnHand
            FROM dbo.Recipes r JOIN dbo.Products p ON p.ProductId=r.MaterialId
            JOIN dbo.StockBalances b {(locked?"WITH (UPDLOCK,HOLDLOCK)":"")} ON b.ProductId=r.MaterialId
            WHERE r.ProductId=@id ORDER BY r.MaterialId
            """;
        var items=new List<MaterialView>();
        await using var cmd=ProductionDb.Command(conn,tx,sql,("@id",productId));
        await using var rd=await cmd.ExecuteReaderAsync(ct);
        while(await rd.ReadAsync(ct))
        {
            var needed = qty * rd.GetDecimal(4);
            var available = rd.GetDecimal(5);
            items.Add(new(rd.GetInt32(0),rd.GetString(1),rd.GetString(2),rd.GetString(3),needed,available,available>=needed));
        }
        return items;
    }
    private static async Task<OriginalRun?> GetRunAsync(SqlConnection conn,SqlTransaction? tx,Guid id,bool locked,CancellationToken ct)
    {
        var sql=$"""
            SELECT r.RunId,r.OrderId,r.Quantity,r.ReversedAt,o.Code,o.ProductId,o.Produced,o.Planned
            FROM dbo.ProductionRuns r {(locked?"WITH (UPDLOCK,HOLDLOCK)":"")}
            JOIN dbo.Orders o ON o.OrderId=r.OrderId WHERE r.RunId=@id
            """;
        await using var cmd=ProductionDb.Command(conn,tx,sql,("@id",id));
        await using var rd=await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct)?new(rd.GetGuid(0),rd.GetInt32(1),rd.GetDecimal(2),rd.IsDBNull(3)?null:rd.GetDateTime(3),rd.GetString(4),rd.GetInt32(5),rd.GetDecimal(6),rd.GetDecimal(7)):null;
    }
    private static async Task<decimal> GetStockAsync(SqlConnection conn,SqlTransaction? tx,int id,bool locked,CancellationToken ct)
    {
        var sql=$"SELECT OnHand FROM dbo.StockBalances {(locked?"WITH (UPDLOCK,HOLDLOCK)":"")} WHERE ProductId=@id";
        await using var cmd=ProductionDb.Command(conn,tx,sql,("@id",id));
        var value=await cmd.ExecuteScalarAsync(ct);
        if(value is not decimal qty) throw new DomainException("Stok bakiyesi kaydı bulunamadı.");
        return qty;
    }
    private static async Task<List<LedgerRow>> GetOriginalMovementsAsync(SqlConnection conn,SqlTransaction tx,Guid id,CancellationToken ct)
    {
        var result=new List<LedgerRow>();
        await using var cmd=ProductionDb.Command(conn,tx,"""
            SELECT ProductId,SUM(Delta) FROM dbo.StockMovements WITH (HOLDLOCK)
            WHERE RunId=@id AND MovementType IN ('P','C') GROUP BY ProductId ORDER BY ProductId
            """,("@id",id));
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) result.Add(new(reader.GetInt32(0),reader.GetDecimal(1)));
        return result;
    }
    private static async Task UpdateStockAsync(SqlConnection conn,SqlTransaction tx,int id,decimal delta,CancellationToken ct)
    {
        if(await ProductionDb.ExecAsync(conn,tx,"UPDATE dbo.StockBalances SET OnHand=OnHand+@delta WHERE ProductId=@id AND OnHand+@delta>=0",ct,("@delta",delta),("@id",id))!=1)
            throw new DomainException("Stok bakiyesi yetersiz veya kayıt eksik.");
    }
    private static async Task AddMovementAsync(SqlConnection conn,SqlTransaction tx,Guid runId,int productId,decimal delta,char type,CancellationToken ct)
    {
        await ProductionDb.ExecAsync(conn,tx,"INSERT dbo.StockMovements(RunId,ProductId,Delta,MovementType) VALUES(@run,@product,@delta,@type)",ct,("@run",runId),("@product",productId),("@delta",delta),("@type",type.ToString()));
    }
}
