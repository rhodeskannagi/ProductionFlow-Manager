using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ProductionFlow.Licensing;
public sealed class LicenseException(string message):Exception(message);
public sealed record LicenseClaims(int Version,string Product,string Edition,string Device,string Customer,long IssuedAt,long? ExpiresAt);
public static class LicenseToken
{
 public static string PublicKey
 {
  get{using var s=typeof(LicenseToken).Assembly.GetManifestResourceStream("ProductionFlow.Licensing.license-public.pem")!;using var r=new StreamReader(s);return r.ReadToEnd();}
 }
 public static string NormalizeDevice(string device)=>device.Replace("-","").Trim().ToUpperInvariant();
 public static void ValidateDevice(string device){if(!Regex.IsMatch(device,@"\A[A-F0-9]{64}\z",RegexOptions.CultureInvariant))throw new LicenseException("Cihaz kodu 64 onaltılık karakter olmalı.");}
 public static string Issue(RSA privateKey,string device,string customer,DateTimeOffset now,DateTimeOffset? expires)
 {
  device=NormalizeDevice(device);ValidateDevice(device);customer=customer.Trim();
  if(customer.Length is <1 or >120)throw new LicenseException("Müşteri adı 1–120 karakter olmalı.");
  if(expires<=now)throw new LicenseException("Bitiş tarihi gelecekte olmalı.");
  var claims=new LicenseClaims(1,"ProductionFlow.Desktop","Full",device,customer,now.ToUnixTimeSeconds(),expires?.ToUnixTimeSeconds());
  var data=JsonSerializer.SerializeToUtf8Bytes(claims);
  return "PF1."+Convert.ToBase64String(data)+"."+Convert.ToBase64String(privateKey.SignData(data,HashAlgorithmName.SHA256,RSASignaturePadding.Pss));
 }
 public static LicenseClaims Verify(string token,string device,DateTimeOffset now,string? publicKey=null)
 {
  try
  {
   if(token.Length>8192)throw new LicenseException("Lisans anahtarı çok uzun.");
   var parts=token.Trim().Split('.');if(parts.Length!=3||parts[0]!="PF1")throw new LicenseException("Geçersiz lisans biçimi.");
   var data=Convert.FromBase64String(parts[1]);var signature=Convert.FromBase64String(parts[2]);
   using var rsa=RSA.Create();rsa.ImportFromPem(publicKey??PublicKey);
   if(!rsa.VerifyData(data,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new LicenseException("Lisans imzası doğrulanamadı.");
   var c=JsonSerializer.Deserialize<LicenseClaims>(data)??throw new LicenseException("Lisans boş.");
   if(c.Version!=1||c.Product!="ProductionFlow.Desktop"||c.Edition!="Full"||c.Device!=NormalizeDevice(device))throw new LicenseException("Lisans bu cihaz veya uygulama için değil.");
   if(string.IsNullOrWhiteSpace(c.Customer)||c.Customer.Length>120||c.IssuedAt>now.AddMinutes(5).ToUnixTimeSeconds())throw new LicenseException("Lisans bilgileri veya bilgisayar saati geçersiz.");
   if(c.ExpiresAt is long expiry&&(expiry<=now.ToUnixTimeSeconds()||expiry<=c.IssuedAt))throw new LicenseException("Lisansın süresi dolmuş.");
   return c;
  }
  catch(Exception e) when(e is FormatException or CryptographicException or JsonException or ArgumentException){throw new LicenseException("Lisans doğrulanamadı.");}
 }
}
