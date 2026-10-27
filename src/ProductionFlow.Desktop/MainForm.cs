using System.Text;
using ProductionFlow.Api.Services;
namespace ProductionFlow.Desktop;
public sealed class MainForm : Form
{
 readonly string demoPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ProductionFlow","desktop-demo-v1.json");
 IWorkspace workspace;
 Overview? snapshot;
 readonly Label mode=new(){AutoSize=true,ForeColor=Color.FromArgb(112,214,179),Margin=new(18,14,10,10)};
 readonly Label metrics=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new(20,0,0,0),Font=new("Segoe UI Semibold",13)};
 readonly Label status=new(){Dock=DockStyle.Bottom,Height=30,TextAlign=ContentAlignment.MiddleLeft,Padding=new(18,0,0,0),BackColor=Color.FromArgb(230,235,241)};
 readonly TextBox search=new(){Width=280,PlaceholderText="Emir, stok kodu veya ürün ara…"};
 readonly ComboBox filter=new(){Width=160,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly TextBox actor=new(){Width=180,Text=Environment.UserName,MaxLength=80};
 readonly NumericUpDown quantity=new(){Minimum=1,Maximum=100000,Value=1,Width=100};
 readonly DataGridView orders=Grid(),inventory=Grid(),history=Grid(),audit=Grid(),quality=Grid();
 readonly TabControl tabs=new(){Dock=DockStyle.Fill};
 ConnectionProfile? profile;
 readonly CancellationTokenSource lifetime=new();
 readonly Panel content=new(){Dock=DockStyle.Fill};
 bool busy;
 Guid pendingRequest=Guid.NewGuid();
 int pendingOrder;
 decimal pendingQuantity;
 string pendingActor="";
 public MainForm()
 {
  workspace=new DisconnectedWorkspace();
  Text="ProductionFlow Manager • Masaüstü 0.4";Size=new(1300,850);MinimumSize=new(1000,680);StartPosition=FormStartPosition.CenterScreen;Font=new("Segoe UI",10);BackColor=Color.FromArgb(246,248,251);
  var header=new FlowLayoutPanel{Dock=DockStyle.Top,Height=64,BackColor=Color.FromArgb(21,36,56),Padding=new(14,6,0,0),WrapContents=false};
  header.Controls.Add(new Label{Text="PRODUCTIONFLOW",AutoSize=true,ForeColor=Color.White,Font=new("Segoe UI Semibold",18),Margin=new(4,8,12,0)});header.Controls.Add(mode);
  header.Controls.Add(Button("Bağlantı ayarları",Connect));header.Controls.Add(Button("Demo moduna geç",async()=>{if(MessageBox.Show(this,"SQL bağlantısından ayrılıp yerel demo verilerine geçilsin mi?","Çalışma alanı",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;workspace=new DemoWorkspace(demoPath);pendingRequest=Guid.NewGuid();SetMode("DEMO • Yerel kayıt");await RefreshData();}));header.Controls.Add(Button("Yenile",RefreshData));
  content.Controls.Add(tabs);var metricPanel=new Panel{Dock=DockStyle.Top,Height=62};metricPanel.Controls.Add(metrics);content.Controls.Add(metricPanel);
  Controls.Add(content);Controls.Add(status);Controls.Add(header);
  var ordersPage=Page("Üretim emirleri",orders);var bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=78,Padding=new(10),WrapContents=true};
  filter.Items.AddRange(["Tüm emirler","Açık emirler","Kapandı"]);filter.SelectedIndex=1;
  bar.Controls.AddRange([search,filter,new Label{Text="Miktar",AutoSize=true,Margin=new(10,7,0,0)},quantity,Button("Önizle ve üret",Produce),new Label{Text="Operatör",AutoSize=true,Margin=new(10,7,0,0)},actor]);ordersPage.Controls.Add(bar);
  Page("Stoklar",inventory);var management=new TabPage("Yönetim"){Padding=new(18)};management.Controls.Add(TopBar(Button("Ürün / ilk stok",()=>Manage("product")),Button("Stok düzelt",()=>Manage("stock")),Button("Reçete tanımla",()=>Manage("recipe")),Button("Yeni üretim emri",()=>Manage("order"))));tabs.TabPages.Add(management);Page("Üretim geçmişi",history).Controls.Add(TopBar(Button("Seçili üretimi geri al",Undo)));
  Page("İşlem günlüğü",audit);Page("Veri kalitesi",quality).Controls.Add(TopBar(Button("Kontrolleri çalıştır",Check)));
  foreach(TabPage page in tabs.TabPages)page.Controls.Add(new Panel{Dock=DockStyle.Bottom,Height=44,Controls={Button("Görünümü CSV aktar",Export)}});
  search.TextChanged+=(_,_)=>BindOrders();filter.SelectedIndexChanged+=(_,_)=>BindOrders();
  Shown+=async(_,_)=>await Guard(Startup);
  FormClosing+=(_,e)=>{if(busy){e.Cancel=true;status.Text="Devam eden işlemin bitmesini bekleyin.";}else lifetime.Cancel();};
  FormClosed+=(_,_)=>lifetime.Dispose();SetMode("BAĞLANTI BEKLENİYOR");
 }
 void SetMode(string title){mode.Text=title;Text="ProductionFlow Manager • "+title;}
 static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoGenerateColumns=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,RowHeadersVisible=false,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,AlternatingRowsDefaultCellStyle=new(){BackColor=Color.FromArgb(244,247,250)},ColumnHeadersHeight=38};
 Button Button(string text,Func<Task> action){var b=new Button{Text=text,AutoSize=true,Height=34,BackColor=Color.White,ForeColor=Color.FromArgb(28,48,70),FlatStyle=FlatStyle.Flat,Margin=new(6,6,6,4)};b.Click+=async(_,_)=>await Guard(action);return b;}
 TabPage Page(string title,DataGridView grid){var p=new TabPage(title){Padding=new(10),BackColor=Color.White};p.Controls.Add(grid);tabs.TabPages.Add(p);return p;}
 static FlowLayoutPanel TopBar(params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Top,Height=50};p.Controls.AddRange(controls);return p;}
 async Task Guard(Func<Task> action)
 {
  if(busy)return;busy=true;content.Enabled=false;UseWaitCursor=true;
  try{await action();}
  catch(Exception e){status.Text="İşlem tamamlanamadı.";MessageBox.Show(this,e.Message+"\n\nÜretim/geri alma sırasında bağlantı kesildiyse, yeniden denemeden önce Yenile ile geçmişi kontrol edin.","İşlem sonucu",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  finally{busy=false;content.Enabled=true;UseWaitCursor=false;}
 }
 async Task Startup()
 {
  try
  {
   profile=new ProfileStore().Load();
   if(profile==null){await Connect();return;}
   status.Text="Kayıtlı sunucuya bağlanılıyor…";
   await DatabaseBootstrap.EnsureAsync(profile,lifetime.Token);
   await OpenProfile(profile);
  }
  catch(Exception e)
  {
   SetMode("BAĞLANTI KURULAMADI");
   MessageBox.Show(this,"Otomatik bağlantı kurulamadı. Kayıtlı ayarlar silinmedi.\n"+e.Message,"Bağlantı",MessageBoxButtons.OK,MessageBoxIcon.Warning);
   await Connect();
  }
 }
 async Task Connect()
 {
  using var dialog=new ConnectionDialog(profile);if(dialog.ShowDialog(this)!=DialogResult.OK)return;
  profile=dialog.Profile!;await OpenProfile(profile);
 }
 async Task OpenProfile(ConnectionProfile selected)
 {
  var candidate=new SqlWorkspace(new ProductionDb(selected.Build()));var data=await candidate.Read(lifetime.Token);
  workspace=candidate;snapshot=data;pendingRequest=Guid.NewGuid();SetMode("SQL • "+selected.Database);BindAll();quality.DataSource=null;
 }
 async Task RefreshData(){snapshot=await workspace.Read(lifetime.Token);BindAll();quality.DataSource=null;}
 void BindAll()
 {
  if(snapshot==null)return;BindOrders();
  inventory.DataSource=snapshot.Inventory.Select(p=>new{Kod=p.Sku,Ürün=p.Name,Birim=p.Unit,Tür=p.Kind=="finished"?"Mamul":"Hammadde",Stok=p.OnHand}).ToList();
  history.DataSource=snapshot.Runs.Select(r=>new{Id=r.Id,Emir=r.OrderCode,Ürün=r.ProductName,Miktar=r.Quantity,Tarih=r.CreatedAt.ToLocalTime(),Durum=r.ReversedAt==null?"Aktif":"Geri alındı",GeriAlma=r.ReversedAt?.ToLocalTime(),Operatör=r.Operator,Gerekçe=r.Reason}).ToList();if(history.Columns[nameof(RunView.Id)] is {} hc)hc.Visible=false;
  audit.DataSource=snapshot.Audit.Select(a=>new{Tarih=a.At.ToLocalTime(),İşlem=a.Action,Açıklama=a.Details}).ToList();
  metrics.Text=$"{snapshot.Orders.Count(o=>o.Status!="Kapandı")} açık emir     |     {snapshot.Orders.Sum(o=>o.Planned-o.Produced):N0} kalan adet     |     {snapshot.Orders.Sum(o=>o.Produced):N0} üretilen     |     {snapshot.Runs.Count(r=>r.ReversedAt!=null)} geri alma";
  status.Text=$"Son yenileme: {DateTime.Now:dd.MM.yyyy HH:mm:ss} • {mode.Text}";
 }
 void BindOrders()
 {
  if(snapshot==null)return;var term=search.Text.Trim();var selected=orders.CurrentRow?.Cells["Id"].Value as int?;
  orders.DataSource=snapshot.Orders.Where(o=>(filter.SelectedIndex!=1||o.Status!="Kapandı")&&(filter.SelectedIndex!=2||o.Status=="Kapandı")&&(o.Code+" "+o.ProductName+" "+o.Sku).Contains(term,StringComparison.CurrentCultureIgnoreCase)).Select(o=>new{Id=o.Id,Emir=o.Code,StokKodu=o.Sku,Ürün=o.ProductName,Planlanan=o.Planned,Üretilen=o.Produced,Kalan=o.Planned-o.Produced,Durum=o.Status}).ToList();
  if(orders.Columns["Id"] is {} oc)oc.Visible=false;
  foreach(DataGridViewRow row in orders.Rows)if(row.Cells["Id"].Value is int rid&&rid==selected){orders.CurrentCell=row.Cells["Emir"];break;}
 }
 async Task Manage(string operation)
 {
  if(workspace is not SqlWorkspace || profile==null)throw new DomainException("Bu ekranlar bağımsız SQL veritabanında kullanılır. Önce bağlantı ayarlarını tamamlayın.");
  using var dialog=new CatalogDialog(operation,new CatalogService(new ProductionDb(profile.Build())),actor.Text);
  if(dialog.ShowDialog(this)==DialogResult.OK)await RefreshData();
 }
 async Task Produce()
 {
  if(orders.CurrentRow?.Cells["Id"].Value is not int id)throw new DomainException("Bir üretim emri seçin.");var q=quantity.Value;var who=actor.Text.Trim();if(who.Length==0)throw new DomainException("Operatör adı gerekli.");
  var preview=await workspace.Preview(id,q,lifetime.Token);
  var rows=preview.Materials.Select(m=>new{Kod=m.Sku,Malzeme=m.Name,Birim=m.Unit,Gerekli=m.Needed,Mevcut=m.Available,Durum=m.Enough?"Yeterli":"Yetersiz"}).ToList();
  using var form=new Form{Text=$"Üretim önizleme • {preview.OrderCode} • {q:N0} adet",Size=new(900,480),StartPosition=FormStartPosition.CenterParent,Font=Font};
  var grid=Grid();grid.DataSource=rows;form.Controls.Add(grid);
  var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=92,FlowDirection=FlowDirection.TopDown};
  footer.Controls.Add(new Label{AutoSize=true,Text=preview.CanProduce?"Kaydet: stok düşer, mamul girişi ve işlem geçmişi oluşur.":string.Join(" ",preview.Warnings)});
  var ok=new Button{Text="Üretimi kaydet",AutoSize=true,Enabled=preview.CanProduce,DialogResult=DialogResult.OK};footer.Controls.Add(ok);form.Controls.Add(footer);
  if(form.ShowDialog(this)!=DialogResult.OK)return;
  if(id!=pendingOrder||q!=pendingQuantity||who!=pendingActor){pendingRequest=Guid.NewGuid();pendingOrder=id;pendingQuantity=q;pendingActor=who;}
  await workspace.Produce(id,new(q,pendingRequest,who),lifetime.Token);pendingRequest=Guid.NewGuid();
  await RefreshData();status.Text=$"{preview.OrderCode}: {q:N0} adet üretim kaydedildi.";
 }
 async Task Undo()
 {
  if(history.CurrentRow?.Cells["Id"].Value is not Guid id)throw new DomainException("Geçmişten bir üretim seçin.");
  var p=await workspace.UndoPreview(id,lifetime.Token);if(!p.CanUndo)throw new DomainException(string.Join(" ",p.Warnings));
  using var dialog=new Form{Text="Üretimi geri al",Size=new(540,270),StartPosition=FormStartPosition.CenterParent,Font=Font};
  var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),FlowDirection=FlowDirection.TopDown};dialog.Controls.Add(panel);
  panel.Controls.Add(new Label{AutoSize=true,Text=$"{p.OrderCode}: {p.Quantity:N0} adet geri alınacak.\nTüketilen hammaddeler stoğa iade edilir."});
  var reason=new TextBox{Multiline=true,Width=470,Height=70,MaxLength=300,PlaceholderText="Gerekçe (8-300 karakter)"};panel.Controls.Add(reason);
  panel.Controls.Add(new Button{Text="Geri almayı onayla",AutoSize=true,DialogResult=DialogResult.OK});
  if(dialog.ShowDialog(this)!=DialogResult.OK)return;
  await workspace.Undo(id,new(reason.Text,actor.Text),lifetime.Token);await RefreshData();status.Text=$"{p.OrderCode}: {p.Quantity:N0} adet geri alındı.";
 }
 async Task Check()
 {
  var report=await workspace.Check(lifetime.Token);quality.DataSource=report.Issues.Select(i=>new{Önem=i.Severity=="error"?"Hata":"Uyarı",Kod=i.Code,Referans=i.Reference,Açıklama=i.Details}).ToList();
  status.Text=$"Kontrol: {report.CheckedOrders} emir / {report.CheckedRuns} üretim • {report.Errors} hata, {report.Warnings} uyarı • {report.CheckedAt.ToLocalTime():HH:mm:ss}";
 }
 Task Export()
 {
  var grid=tabs.SelectedTab?.Controls.OfType<DataGridView>().SingleOrDefault();if(grid==null||grid.Rows.Count==0)throw new DomainException("Aktarılacak satır yok.");
  using var save=new SaveFileDialog{Filter="CSV dosyası|*.csv",FileName=$"ProductionFlow-{DateTime.Now:yyyyMMdd-HHmmss}.csv"};if(save.ShowDialog(this)!=DialogResult.OK)return Task.CompletedTask;
  var columns=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible).OrderBy(c=>c.DisplayIndex).ToList();
  using var writer=new StreamWriter(save.FileName,false,new UTF8Encoding(true));writer.WriteLine(Csv.Line(columns.Select(c=>(object)c.HeaderText)));
  foreach(DataGridViewRow row in grid.Rows)writer.WriteLine(Csv.Line(columns.Select(c=>row.Cells[c.Index].Value)));
  status.Text=$"{grid.Rows.Count} satır CSV dosyasına aktarıldı.";return Task.CompletedTask;
 }
}
