using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;

namespace ProductionFlow.Api.Services;

public sealed class ConnectionProfile
{
 public string Server {get;set;}="";
 public string Database {get;set;}="ProductionFlowManager";
 public bool WindowsAuthentication {get;set;}=true;
 public string UserName {get;set;}="";
 public string Password {get;set;}="";
 public bool TrustServerCertificate {get;set;}
 public string Build(string? database=null)
 {
  Validate();
  var b=new SqlConnectionStringBuilder{DataSource=Server.Trim(),InitialCatalog=database??Database,IntegratedSecurity=WindowsAuthentication,Encrypt=true,TrustServerCertificate=TrustServerCertificate,ConnectTimeout=10,ApplicationName="ProductionFlow Desktop",PersistSecurityInfo=false};
  if(!WindowsAuthentication){b.UserID=UserName;b.Password=Password;}
  return b.ConnectionString;
 }
 public void Validate()
 {
  if(string.IsNullOrWhiteSpace(Server)||Server.Length>200)throw new DomainException("Sunucu adını girin.");
  if(!Regex.IsMatch(Database,@"\AProductionFlow(?:Manager|_[A-Za-z0-9_]{1,80})\z",RegexOptions.CultureInvariant))
   throw new DomainException("Bağımsız veritabanı adı ProductionFlowManager veya ProductionFlow_ ile başlayan harf/rakam/alt çizgi olmalı.");
  if(!WindowsAuthentication&&(string.IsNullOrWhiteSpace(UserName)||string.IsNullOrEmpty(Password)))throw new DomainException("SQL kullanıcı adı ve parola gerekli.");
 }
}

public static class DatabaseBootstrap
{
 public static readonly Guid ApplicationId=new("F8F82B35-62A6-4932-B02B-893981A2C860");
 public const int CurrentVersion=1;
 public static string SchemaSql
 {
  get{using var stream=typeof(DatabaseBootstrap).Assembly.GetManifestResourceStream("ProductionFlow.Core.Database.001-owned-schema.sql")!;using var reader=new StreamReader(stream);return reader.ReadToEnd();}
 }
 public static IReadOnlyList<string> SchemaBatches=>Regex.Split(SchemaSql,@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
 public static async Task EnsureAsync(ConnectionProfile profile,CancellationToken ct)
 {
  profile.Validate();
  // Initial missing-database failures must not poison the application connection pool.
  var target=new SqlConnectionStringBuilder(profile.Build()){Pooling=false}.ConnectionString;
  // The normal path needs no master DDL privilege and performs no schema writes.
  try
  {
   await using var current=new SqlConnection(target);await current.OpenAsync(ct);
   if(await IsOwnedAsync(current,ct))return;
  }
  catch(SqlException ex) when(ex.Number==4060){ /* Missing OR inaccessible: master check below distinguishes these. */ }

  var b=new SqlConnectionStringBuilder(profile.Build("master")){Pooling=false};
  await using var master=new SqlConnection(b.ConnectionString);await master.OpenAsync(ct);
  var resource="ProductionFlow.Bootstrap."+profile.Database.ToUpperInvariant();
  await using(var command=new SqlCommand("DECLARE @r INT; EXEC @r=sys.sp_getapplock @Resource=@name,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=15000; SELECT @r;",master){CommandTimeout=20})
  {
   command.Parameters.AddWithValue("@name",resource);
   if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))<0)throw new DomainException("Başka kurulum çalışıyor. Biraz sonra tekrar deneyin.");
  }
  // Pooling is off: disposal physically releases the session-owned lock even on failure.
  bool exists;
  await using(var cmd=new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name=@name",master))
  {cmd.Parameters.AddWithValue("@name",profile.Database);exists=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct))!=0;}
  if(!exists)
  {
   // CREATE DATABASE must be outside a transaction. The identifier is validated above.
   await using var cmd=new SqlCommand($"CREATE DATABASE [{profile.Database}]",master){CommandTimeout=90};
   await cmd.ExecuteNonQueryAsync(ct);
  }
  await using var conn=new SqlConnection(target);await conn.OpenAsync(ct);
  if(await IsOwnedAsync(conn,ct))return;
  // Never adopt/alter another application's nonempty database.
  await using(var check=new SqlCommand("SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped=0",conn))
   if(Convert.ToInt32(await check.ExecuteScalarAsync(ct))!=0)throw new DomainException("Veritabanı ProductionFlow'a ait değil veya yarım bir şema içeriyor. Hiçbir tablo değiştirilmedi.");
  await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(ct);
  try
  {
   foreach(var batch in SchemaBatches){await using var cmd=new SqlCommand(batch,conn,tx){CommandTimeout=60};await cmd.ExecuteNonQueryAsync(ct);}
   await tx.CommitAsync(ct);
  }
  catch{await tx.RollbackAsync(CancellationToken.None);throw;}
 }
 static async Task<bool> IsOwnedAsync(SqlConnection conn,CancellationToken ct)
 {
  await using var check=new SqlCommand("SELECT OBJECT_ID(N'dbo.AppSchema',N'U')",conn);
  if(await check.ExecuteScalarAsync(ct) is DBNull)return false;
  await using var cmd=new SqlCommand("SELECT ApplicationId,Version FROM dbo.AppSchema WHERE Id=1",conn);
  await using var reader=await cmd.ExecuteReaderAsync(ct);
  if(!await reader.ReadAsync(ct)||reader.GetGuid(0)!=ApplicationId)throw new DomainException("Veritabanı sahiplik işareti uyuşmuyor. İşlem yapılmadı.");
  if(reader.GetInt32(1)!=CurrentVersion)throw new DomainException("Veritabanı sürümü bu uygulamayla uyumlu değil. Otomatik değişiklik yapılmadı.");
  return true;
 }
}
