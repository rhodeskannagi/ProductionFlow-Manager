using Microsoft.Win32;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ProductionFlow.Licensing;
namespace ProductionFlow.Desktop;
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class LicenseStore(string? customPath=null):IUsageStore
{
 readonly string path=customPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ProductionFlow","license-usage.dpapi");
 static readonly byte[] purpose=Encoding.UTF8.GetBytes("ProductionFlow.OfflineLicense.Usage.v1");
 public static string DeviceCode()
 {
  using var registry=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
  using var machine=registry.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
  var guid=machine?.GetValue("MachineGuid") as string;var sid=WindowsIdentity.GetCurrent().User?.Value;
  if(string.IsNullOrWhiteSpace(guid)||string.IsNullOrWhiteSpace(sid))throw new LicenseException("Bu bilgisayarın cihaz kodu okunamadı.");
  return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("ProductionFlow.v1|"+guid+"|"+sid)));
 }
 public UsageState Load()
 {
  if(!File.Exists(path))return new();
  try
  {
   var bytes=ProtectedData.Unprotect(File.ReadAllBytes(path),purpose,DataProtectionScope.CurrentUser);
   try{return JsonSerializer.Deserialize<UsageState>(bytes)??throw new LicenseException("Kota kaydı boş.");}
   finally{CryptographicOperations.ZeroMemory(bytes);}
  }
  catch(Exception e) when(e is CryptographicException or JsonException){throw new LicenseException("Lisans/kota dosyası değiştirilmiş veya okunamıyor. Dosya otomatik sıfırlanmadı.");}
 }
 public void Save(UsageState state)
 {
  var bytes=JsonSerializer.SerializeToUtf8Bytes(state);byte[] encrypted;
  try{encrypted=ProtectedData.Protect(bytes,purpose,DataProtectionScope.CurrentUser);}finally{CryptographicOperations.ZeroMemory(bytes);}
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);var temp=path+".tmp";
  try{using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(encrypted);f.Flush(true);}File.Move(temp,path,true);}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
