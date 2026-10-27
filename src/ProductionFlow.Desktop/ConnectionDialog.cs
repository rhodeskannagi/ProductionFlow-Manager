using Microsoft.Data.SqlClient;
namespace ProductionFlow.Desktop;
public sealed class ConnectionDialog : Form
{
 readonly TextBox server=new(){Text="localhost",Width=320};
 readonly ComboBox database=new(){Width=320,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly CheckBox windows=new(){Text="Windows kimlik doğrulaması",Checked=true,AutoSize=true};
 readonly TextBox user=new(){Width=320,Enabled=false};
 readonly TextBox password=new(){Width=320,UseSystemPasswordChar=true,Enabled=false};
 readonly CheckBox trust=new(){Text="Yerel test sertifikasına güven",AutoSize=true};
 readonly Label message=new(){AutoSize=false,Width=440,Height=65};
 public string? ConnectionString {get;private set;}
 public string Caption=>server.Text+" / "+database.Text;
 public ConnectionDialog()
 {
  Text="SQL Server bağlantısı";Size=new(510,510);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);
  var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new(18),WrapContents=false,AutoScroll=true};Controls.Add(panel);
  void Field(string title,Control c){panel.Controls.Add(new Label{Text=title,AutoSize=true});panel.Controls.Add(c);}
  Field("Sunucu / örnek",server);panel.Controls.Add(windows);Field("SQL kullanıcı adı",user);Field("Parola (kaydedilmez)",password);panel.Controls.Add(trust);
  var list=new Button{Text="Veritabanlarını listele",AutoSize=true};panel.Controls.Add(list);Field("ProductionFlow veritabanı",database);
  var connect=new Button{Text="Bağlan",AutoSize=true};panel.Controls.Add(connect);panel.Controls.Add(message);
  windows.CheckedChanged+=(_,_)=>{user.Enabled=password.Enabled=!windows.Checked;};
  list.Click+=async(_,_)=>await Run(async()=>{
   await using var conn=new SqlConnection(Build("master"));await conn.OpenAsync();
   await using var cmd=new SqlCommand("SELECT name FROM sys.databases WHERE state=0 AND database_id>4 AND HAS_DBACCESS(name)=1 ORDER BY name",conn){CommandTimeout=15};
   await using var reader=await cmd.ExecuteReaderAsync();database.Items.Clear();while(await reader.ReadAsync())database.Items.Add(reader.GetString(0));
   if(database.Items.Contains("ProductionFlowDemo"))database.SelectedItem="ProductionFlowDemo";
   message.Text=$"{database.Items.Count} erişilebilir veritabanı. ProductionFlow şeması bulunan test veritabanını seçin.";
  });
  connect.Click+=async(_,_)=>await Run(async()=>{
   if(database.SelectedItem==null)throw new InvalidOperationException("Önce veritabanını listeleyip seçin.");
   var cs=Build(database.Text);await using var conn=new SqlConnection(cs);await conn.OpenAsync();
   await using var cmd=new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name IN ('Orders','Products','Recipes','StockBalances','ProductionRuns','StockMovements','AuditEvents')",conn){CommandTimeout=15};
   if(Convert.ToInt32(await cmd.ExecuteScalarAsync())!=7)throw new InvalidOperationException("ProductionFlow şeması bulunamadı. db/001_create_and_seed.sql dosyasını ayrı test veritabanında kurun.");
   ConnectionString=cs;DialogResult=DialogResult.OK;Close();
  });
 }
 string Build(string db)
 {
  if(string.IsNullOrWhiteSpace(server.Text))throw new InvalidOperationException("Sunucu adı gerekli.");
  var b=new SqlConnectionStringBuilder{DataSource=server.Text.Trim(),InitialCatalog=db,IntegratedSecurity=windows.Checked,Encrypt=true,TrustServerCertificate=trust.Checked,ConnectTimeout=10,ApplicationName="ProductionFlow Desktop"};
  if(!windows.Checked){b.UserID=user.Text.Trim();b.Password=password.Text;}return b.ConnectionString;
 }
 async Task Run(Func<Task> action)
 {
  Enabled=false;UseWaitCursor=true;try{await action();}catch(Exception e){message.Text=e.Message;}finally{Enabled=true;UseWaitCursor=false;}
 }
}
