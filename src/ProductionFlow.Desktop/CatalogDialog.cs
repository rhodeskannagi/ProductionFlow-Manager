using ProductionFlow.Api.Services;
namespace ProductionFlow.Desktop;
public sealed class CatalogDialog : Form
{
 readonly FlowLayoutPanel panel=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new(16),WrapContents=false,AutoScroll=true};
 readonly TextBox code=new(){Width=430},name=new(){Width=430},unit=new(){Width=430,Text="adet"},reason=new(){Width=430,Multiline=true,Height=65};
 readonly ComboBox product=new(){Width=430,DropDownStyle=ComboBoxStyle.DropDownList},material=new(){Width=430,DropDownStyle=ComboBoxStyle.DropDownList},kind=new(){Width=430,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly NumericUpDown amount=new(){Width=200,Minimum=-100000000,Maximum=100000000,DecimalPlaces=4};
 readonly Label message=new(){Width=450,Height=75};
 bool busy;
 public CatalogDialog(string operation,CatalogService service,string actor)
 {
  Text="ProductionFlow • "+operation;Size=new(510,580);StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);Controls.Add(panel);
  void Field(string label,Control control){panel.Controls.Add(new Label{Text=label,AutoSize=true});panel.Controls.Add(control);}
  switch(operation)
  {
   case "product":Text="Yeni ürün / ilk stok";Field("Stok kodu",code);Field("Ürün adı",name);Field("Birim",unit);kind.Items.AddRange(["Hammadde","Mamul"]);kind.SelectedIndex=0;Field("Tür",kind);amount.Minimum=0;Field("İlk stok",amount);break;
   case "stock":Text="Gerekçeli stok düzeltme";Field("Ürün",product);Field("Stok değişimi (+ giriş, − çıkış)",amount);Field("Gerekçe (en az 8 karakter)",reason);break;
   case "recipe":Text="Mamul reçetesi";Field("Mamul",product);Field("Hammadde",material);amount.Minimum=0;Field("Bir mamul için tüketim (0 = bu satırı sil)",amount);panel.Controls.Add(new Label{Width=450,Height=65,Text="Her hammadde satırını ayrı kaydedin. Aynı mamul/hammadde eşleşmesi güncellenir. Üretim geçmişi bulunan mamulün reçetesi değiştirilemez."});break;
   case "order":Text="Yeni üretim emri";Field("Emir kodu",code);Field("Mamul",product);amount.Minimum=1;amount.Value=1;amount.DecimalPlaces=0;Field("Planlanan adet",amount);break;
  }
  var save=new Button{Text="Kaydet",AutoSize=true};panel.Controls.Add(save);panel.Controls.Add(message);
  Shown+=async(_,_)=>await Run(async()=>
  {
   var view=await service.Read(CancellationToken.None);
   product.DisplayMember="Label";product.ValueMember="Id";product.DataSource=view.Inventory.Where(p=>operation=="stock"||p.Kind=="finished").Select(p=>new Choice(p.Id,p.Sku+" • "+p.Name)).ToList();
   material.DisplayMember="Label";material.ValueMember="Id";material.DataSource=view.Inventory.Where(p=>p.Kind=="material").Select(p=>new Choice(p.Id,p.Sku+" • "+p.Name)).ToList();
  });
  save.Click+=async(_,_)=>await Run(async()=>
  {
   int Id(ComboBox box)=>box.SelectedValue is int id?id:throw new DomainException("Önce ürün tanımlayıp seçim yapın.");
   switch(operation)
   {
    case "product":await service.AddProduct(code.Text,name.Text,unit.Text,kind.SelectedIndex==0?"material":"finished",amount.Value,actor,CancellationToken.None);break;
    case "stock":await service.AdjustStock(Id(product),amount.Value,reason.Text,actor,CancellationToken.None);break;
    case "recipe":await service.SetRecipe(Id(product),Id(material),amount.Value,actor,CancellationToken.None);break;
    case "order":await service.AddOrder(code.Text,Id(product),amount.Value,actor,CancellationToken.None);break;
   }
   DialogResult=DialogResult.OK;Close();
  });
  FormClosing+=(_,e)=>{if(busy&&DialogResult!=DialogResult.OK)e.Cancel=true;};
 }
 async Task Run(Func<Task> task){if(busy)return;busy=true;panel.Enabled=false;try{await task();}catch(Exception e){message.Text=e.Message;}finally{panel.Enabled=true;busy=false;}}
 public sealed record Choice(int Id,string Label);
}
