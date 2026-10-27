using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProductionFlow.Api.Services;
namespace ProductionFlow.Desktop;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class ProfileStore
{
 readonly string path;
 static readonly byte[] Purpose=Encoding.UTF8.GetBytes("ProductionFlow.ConnectionProfile.v1");
 public ProfileStore(string? path=null)=>this.path=path??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ProductionFlow","connection.dpapi");
 public bool Exists=>File.Exists(path);
 public ConnectionProfile? Load()
 {
  if(!Exists)return null;
  byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(path),Purpose,DataProtectionScope.CurrentUser);
  try{var profile=JsonSerializer.Deserialize<ConnectionProfile>(clear)??throw new InvalidDataException("Bağlantı kaydı boş.");profile.Validate();return profile;}
  finally{CryptographicOperations.ZeroMemory(clear);}
 }
 public void Save(ConnectionProfile profile)
 {
  profile.Validate();byte[] clear=JsonSerializer.SerializeToUtf8Bytes(profile);byte[] encrypted;
  try{encrypted=ProtectedData.Protect(clear,Purpose,DataProtectionScope.CurrentUser);}
  finally{CryptographicOperations.ZeroMemory(clear);}
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  var temp=path+".tmp";
  try{using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(encrypted);stream.Flush(true);}File.Move(temp,path,true);}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
