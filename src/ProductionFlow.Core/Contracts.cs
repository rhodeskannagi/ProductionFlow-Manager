namespace ProductionFlow.Api.Services;
public sealed class DomainException(string message) : Exception(message);
public record QuantityInput(decimal Quantity);
public record ProduceInput(decimal Quantity, Guid RequestId, string? Operator);
public record UndoInput(string? Reason, string? Operator);
public record Overview(List<OrderView> Orders, List<ProductView> Inventory, List<RunView> Runs, List<AuditView> Audit)
{
 public object Stats => new { orders=Orders.Count, active=Orders.Count(o=>o.Status!="Kapandı"), completed=Orders.Count(o=>o.Status=="Kapandı"), produced=Orders.Sum(o=>o.Produced), reversedRuns=Runs.Count(r=>r.ReversedAt!=null) };
}
public interface IWorkspace
{
 Task<Overview> Read(CancellationToken ct);
 Task<ProductionPreview> Preview(int order, decimal quantity, CancellationToken ct);
 Task Produce(int order, ProduceInput input, CancellationToken ct);
 Task<UndoPreview> UndoPreview(Guid id, CancellationToken ct);
 Task Undo(Guid id, UndoInput input, CancellationToken ct);
 Task<IntegrityReport> Check(CancellationToken ct);
}
public sealed class SqlWorkspace(ProductionDb db) : IWorkspace
{
 readonly ProductionService service=new(db);
 public Task<Overview> Read(CancellationToken ct)=>service.GetOverviewAsync(ct);
 public Task<ProductionPreview> Preview(int order,decimal quantity,CancellationToken ct)=>service.PreviewProductionAsync(order,quantity,ct);
 public async Task Produce(int order,ProduceInput input,CancellationToken ct)=>await service.ProduceAsync(order,input,ct);
 public Task<UndoPreview> UndoPreview(Guid id,CancellationToken ct)=>service.PreviewUndoAsync(id,ct);
 public async Task Undo(Guid id,UndoInput input,CancellationToken ct)=>await service.UndoAsync(id,input,ct);
 public Task<IntegrityReport> Check(CancellationToken ct)=>new IntegrityService(db).GetReportAsync(ct);
}
public static class Csv
{
 public static string Cell(object? value)
 {
  var s=Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??"";
  if(s.TrimStart().StartsWith('=')||s.TrimStart().StartsWith('+')||s.TrimStart().StartsWith('-')||s.TrimStart().StartsWith('@')||s.StartsWith('\t')||s.StartsWith('\r')) s="'"+s;
  return "\""+s.Replace("\"","\"\"")+"\"";
 }
 public static string Line(IEnumerable<object?> values)=>string.Join(";",values.Select(Cell));
}
