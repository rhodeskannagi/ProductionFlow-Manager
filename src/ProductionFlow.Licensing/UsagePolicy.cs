namespace ProductionFlow.Licensing;
public sealed record UsageState(string Day="",int Used=0,long LastSeen=0,string Token="");
public interface IUsageStore { UsageState Load(); void Save(UsageState state); }
/// <summary>Offline per-Windows-user policy. Not a tamper-proof server-side entitlement system.</summary>
public sealed class UsagePolicy(IUsageStore store,string device,Func<DateTimeOffset>? clock=null,string? publicKey=null)
{
 readonly SemaphoreSlim gate=new(1,1);
 DateTimeOffset Now()=> (clock??(()=>DateTimeOffset.UtcNow))();
 public string Device=>device;
 (UsageState State,DateTimeOffset Time) Current()
 {
  var s=store.Load();var now=Now();
  if(s.Used is <0 or >5||s.LastSeen<0)throw new LicenseException("Lisans/kota kaydı bozuk.");
  if(now.AddMinutes(5).ToUnixTimeSeconds()<s.LastSeen)throw new LicenseException("Bilgisayar saati geri alınmış. Doğru tarih ve saati ayarlayın.");
  now=DateTimeOffset.FromUnixTimeSeconds(Math.Max(now.ToUnixTimeSeconds(),s.LastSeen));
  string day=now.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);
  s=s with {Day=day,Used=s.Day==day?s.Used:0,LastSeen=now.ToUnixTimeSeconds()};
  return(s,now);
 }
 bool Full(UsageState s,DateTimeOffset now)
 {
  if(string.IsNullOrEmpty(s.Token))return false;
  try{LicenseToken.Verify(s.Token,device,now,publicKey);return true;}catch(LicenseException){return false;}
 }
 public (bool Full,int Remaining) Status()
 {
  var (s,now)=Current();store.Save(s);return(Full(s,now),5-s.Used);
 }
 public void RequireFull(){if(!Status().Full)throw new LicenseException("Gerçek veritabanı ve yönetim işlemleri için tam sürüm lisansı gerekli. Lisans ekranından cihaz kodunuzu paylaşın.");}
 public void Activate(string token)
 {
  var(s,now)=Current();LicenseToken.Verify(token,device,now,publicKey);store.Save(s with {Token=token.Trim()});
 }
 public async Task DemoWrite(Func<Task> action)
 {
  await gate.WaitAsync();
  try
  {
   var(s,now)=Current();
   if(Full(s,now)){store.Save(s);await action();return;}
   if(s.Used>=5)throw new LicenseException("Bugünkü 5 demo işlem hakkınız doldu. Haklar Türkiye saatiyle 00:00'da yenilenir veya lisansla tam sürüme geçebilirsiniz.");
   // Reserve durably before the action. A crash may consume one slot, but cannot grant an extra write.
   var reserved=s with {Used=s.Used+1};store.Save(reserved);
   try{await action();}catch{store.Save(s);throw;}
  }
  finally{gate.Release();}
 }
}
