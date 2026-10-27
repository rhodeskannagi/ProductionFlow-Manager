namespace ProductionFlow.Api.Services;
public sealed class DisconnectedWorkspace : IWorkspace
{
 static DomainException Missing()=>new("Önce bağlantı ayarlarını tamamlayın veya demo modunu açıkça seçin.");
 public Task<Overview> Read(CancellationToken ct)=>throw Missing();
 public Task<ProductionPreview> Preview(int order,decimal quantity,CancellationToken ct)=>throw Missing();
 public Task Produce(int order,ProduceInput input,CancellationToken ct)=>throw Missing();
 public Task<UndoPreview> UndoPreview(Guid id,CancellationToken ct)=>throw Missing();
 public Task Undo(Guid id,UndoInput input,CancellationToken ct)=>throw Missing();
 public Task<IntegrityReport> Check(CancellationToken ct)=>throw Missing();
}
