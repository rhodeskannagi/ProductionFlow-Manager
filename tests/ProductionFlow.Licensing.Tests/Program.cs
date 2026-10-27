using System.Security.Cryptography;
using ProductionFlow.Licensing;
int passed=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
void Reject(Action f,string label){try{f();}catch(LicenseException){Check(true,label);return;}throw new Exception("Expected rejection: "+label);}
async Task RejectAsync(Func<Task> f,string label){try{await f();}catch(LicenseException){Check(true,label);return;}throw new Exception("Expected rejection: "+label);}
using var rsa=RSA.Create(3072);var publicKey=rsa.ExportSubjectPublicKeyInfoPem();var device=new string('A',64);var other=new string('B',64);
var now=new DateTimeOffset(2026,10,27,20,59,0,TimeSpan.Zero);
var token=LicenseToken.Issue(rsa,device,"Test customer",now,null);
Check(LicenseToken.Verify(token,device,now,publicKey).Customer=="Test customer","Signed full license accepted");
Reject(()=>LicenseToken.Verify(token,other,now,publicKey),"Different device rejected");
using(var stranger=RSA.Create(2048))Reject(()=>LicenseToken.Verify(token,device,now,stranger.ExportSubjectPublicKeyInfoPem()),"Different signing key rejected");
var parts=token.Split('.');var bytes=Convert.FromBase64String(parts[1]);bytes[5]^=1;parts[1]=Convert.ToBase64String(bytes);
Reject(()=>LicenseToken.Verify(string.Join('.',parts),device,now,publicKey),"Payload tampering rejected");
parts=token.Split('.');bytes=Convert.FromBase64String(parts[2]);bytes[0]^=1;parts[2]=Convert.ToBase64String(bytes);
Reject(()=>LicenseToken.Verify(string.Join('.',parts),device,now,publicKey),"Signature tampering rejected");
Reject(()=>LicenseToken.Verify("PF1.bad.bad",device,now,publicKey),"Malformed token rejected");
Reject(()=>LicenseToken.Issue(rsa,"bad","Test",now,null),"Malformed device rejected");
var expired=LicenseToken.Issue(rsa,device,"Test",now.AddDays(-2),now.AddDays(-1));
Reject(()=>LicenseToken.Verify(expired,device,now,publicKey),"Expired license rejected");
var future=LicenseToken.Issue(rsa,device,"Test",now.AddDays(1),null);
Reject(()=>LicenseToken.Verify(future,device,now,publicKey),"Future-issued license rejected");
var finite=LicenseToken.Issue(rsa,device,"Test",now,now.AddMinutes(1));
Check(LicenseToken.Verify(finite,device,now,publicKey).ExpiresAt!=null,"Finite license accepted before expiry");
Reject(()=>LicenseToken.Verify(finite,device,now.AddMinutes(1),publicKey),"Exact expiry boundary rejected");
var store=new MemoryStore();var policy=new UsagePolicy(store,device,()=>now,publicKey);int writes=0;
Check(!policy.Status().Full&&policy.Status().Remaining==5,"Unlicensed starts with five demo operations");
Reject(policy.RequireFull,"Unlicensed real database blocked");
for(int i=0;i<5;i++)await policy.DemoWrite(()=>{writes++;return Task.CompletedTask;});
Check(writes==5&&policy.Status().Remaining==0,"Five successful writes consume quota");
await RejectAsync(()=>policy.DemoWrite(()=>{writes++;return Task.CompletedTask;}),"Sixth demo write rejected");
Check(writes==5,"Blocked write never reaches business action");
policy=new UsagePolicy(store,device,()=>now,publicKey);Check(policy.Status().Remaining==0,"Restart preserves quota");
now=now.AddMinutes(1);Check(policy.Status().Remaining==5,"Quota resets at Turkey midnight");
await RejectAsync(()=>policy.DemoWrite(()=>throw new LicenseException("Business validation")),"Failed action is propagated");
Check(policy.Status().Remaining==5,"Failed operation refunds quota");
var saved=now;now=now.AddDays(-1);Reject(()=>policy.Status(),"Clock rollback rejected");now=saved;
store.FailWrites=true;try{await policy.DemoWrite(()=>{writes++;return Task.CompletedTask;});throw new Exception("Save should fail");}catch(IOException){Check(writes==5,"Storage failure prevents business mutation");}store.FailWrites=false;
policy.Activate(token);Check(policy.Status().Full,"Activation persists full entitlement");
for(int i=0;i<8;i++)await policy.DemoWrite(()=>Task.CompletedTask);
Check(policy.Status().Full&&policy.Status().Remaining==5,"Full license has unlimited operations");
new UsagePolicy(store,device,()=>now,publicKey).RequireFull();Check(true,"Activation survives restart");
Reject(()=>policy.Activate("wrong"),"Invalid replacement license rejected");Check(policy.Status().Full,"Invalid activation preserves existing license");
policy.Activate(LicenseToken.Issue(rsa,device,"Test",now,now.AddMinutes(1)));now=now.AddMinutes(1);
Check(!policy.Status().Full,"Expired activation returns to demo entitlement");Reject(policy.RequireFull,"Expired real database access blocked");
var concurrent=new UsagePolicy(new MemoryStore(),device,()=>now,publicKey);int accepted=0;
await Task.WhenAll(Enumerable.Range(0,20).Select(async _=>{try{await concurrent.DemoWrite(async()=>{await Task.Yield();Interlocked.Increment(ref accepted);});}catch(LicenseException){}}));
Check(accepted==5,"Concurrent demo calls cannot exceed five");
store.State=store.State with{Used=-1};Reject(()=>policy.Status(),"Corrupt negative usage rejected");
if(OperatingSystem.IsWindows())
{
 var root=Path.Combine(Path.GetTempPath(),"pf-license-test-"+Guid.NewGuid());Directory.CreateDirectory(root);
 try
 {
  var path=Path.Combine(root,"usage.dpapi");var vault=new ProductionFlow.Desktop.LicenseStore(path);var state=new UsageState("2026-10-27",5,now.ToUnixTimeSeconds(),token);vault.Save(state);
  Check(vault.Load()==state,"Windows DPAPI usage and license roundtrip");
  Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(token),"License token absent from plaintext storage");
  var damaged=File.ReadAllBytes(path);damaged[^1]^=127;File.WriteAllBytes(path,damaged);
  Reject(()=>vault.Load(),"Tampered quota file rejected");Check(File.ReadAllBytes(path).SequenceEqual(damaged),"Tampered quota is not reset");
  var code=ProductionFlow.Desktop.LicenseStore.DeviceCode();LicenseToken.ValidateDevice(code);Check(code==ProductionFlow.Desktop.LicenseStore.DeviceCode(),"Windows device code stable");
 }
 finally{Directory.Delete(root,true);}
}
else Console.WriteLine("SKIP Windows-specific storage tests");
Console.WriteLine($"{passed} license checks passed.");
sealed class MemoryStore:IUsageStore
{
 public UsageState State=new();public bool FailWrites;
 public UsageState Load()=>State;
 public void Save(UsageState s){if(FailWrites)throw new IOException("test");State=s;}
}
