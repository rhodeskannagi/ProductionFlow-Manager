using ProductionFlow.Api.Services;
namespace ProductionFlow.Desktop;
public sealed class ConnectionDialog : Form
{
 readonly TextBox server=new(){Width=420,PlaceholderText=@"Sunucu\SQL örneği"};
 readonly TextBox database=new(){Width=420,Text="UretimAkis",MaxLength=100};
 readonly CheckBox windows=new(){Text="Windows kimlik doğrulaması",Checked=true,AutoSize=true};
 readonly TextBox user=new(){Width=420};
 readonly TextBox password=new(){Width=420,UseSystemPasswordChar=true};
 readonly CheckBox trust=new(){Text="Yerel sunucunun kendinden imzalı sertifikasına güven",AutoSize=true};
 readonly Label message=new(){AutoSize=false,Width=460,Height=80};
 bool running;
 public ConnectionProfile? Profile {get;private set;}
 public ConnectionDialog(Action requireLicense,ConnectionProfile? previous=null)
 {
  Text="ProductionFlow • İlk kurulum / bağlantı ayarları";Size=new(540,610);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);
  var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new(18),WrapContents=false,AutoScroll=true};Controls.Add(panel);
  void Field(string title,Control c){panel.Controls.Add(new Label{Text=title,AutoSize=true});panel.Controls.Add(c);}
  panel.Controls.Add(new Label{AutoSize=false,Width=460,Height=55,Text="Ayrı ProductionFlow veritabanı yoksa kurulur. Ürün ve stoklar boş başlar. Mevcut diğer veritabanlarına işlem yapılmaz."});
  Field("Sunucu / örnek",server);Field("Bağımsız veritabanı",database);panel.Controls.Add(windows);Field("SQL kullanıcı adı",user);Field("Parola",password);panel.Controls.Add(trust);
  panel.Controls.Add(new Label{AutoSize=false,Width=460,Height=48,Text="Giriş bilgileri bu Windows hesabına bağlı şifreli kaydedilir. Sonraki açılışlarda otomatik bağlanılır."});
  var connect=new Button{Text="Bağlan / kur ve şifreli kaydet",AutoSize=true};panel.Controls.Add(connect);panel.Controls.Add(message);
  if(previous!=null){server.Text=previous.Server;database.Text=previous.Database;windows.Checked=previous.WindowsAuthentication;user.Text=previous.UserName;password.Text=previous.Password;trust.Checked=previous.TrustServerCertificate;}
  user.Enabled=password.Enabled=!windows.Checked;
  windows.CheckedChanged+=(_,_)=>{user.Enabled=password.Enabled=!windows.Checked;};
  connect.Click+=async(_,_)=>
  {
   if(running)return;running=true;panel.Enabled=false;UseWaitCursor=true;message.Text="Bağlanılıyor; gerekiyorsa bağımsız veritabanı kuruluyor…";
   try
   {
    requireLicense();
    var profile=new ConnectionProfile{Server=server.Text.Trim(),Database=database.Text.Trim(),WindowsAuthentication=windows.Checked,UserName=windows.Checked?"":user.Text.Trim(),Password=windows.Checked?"":password.Text,TrustServerCertificate=trust.Checked};
    await DatabaseBootstrap.EnsureAsync(profile,CancellationToken.None);
    _=await new ProductionService(new ProductionDb(profile.Build())).GetOverviewAsync(CancellationToken.None);
    new ProfileStore().Save(profile);Profile=profile;DialogResult=DialogResult.OK;Close();
   }
   catch(Exception e){message.Text=e.Message;}
   finally{running=false;panel.Enabled=true;UseWaitCursor=false;}
  };
  FormClosing+=(_,e)=>{if(running&&DialogResult!=DialogResult.OK)e.Cancel=true;};
 }
}
