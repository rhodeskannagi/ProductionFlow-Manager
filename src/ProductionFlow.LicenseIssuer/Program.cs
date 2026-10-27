using System.Security.Cryptography;
using ProductionFlow.Licensing;
namespace ProductionFlow.LicenseIssuer;
static class Program
{
 [STAThread] static void Main(){ApplicationConfiguration.Initialize();Application.Run(new IssuerForm());}
}
public sealed class IssuerForm:Form
{
 public IssuerForm()
 {
  Text="ProductionFlow • Ömer Akyol Lisans Üretici";Size=new(790,640);StartPosition=FormStartPosition.CenterScreen;Font=new("Segoe UI",10);
  var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(18)};Controls.Add(panel);
  panel.Controls.Add(new Label{Width=730,Height=45,Text="Bu araç ve owner-private.pem yalnızca sende kalmalı. Müşteriye sadece üretilen lisans anahtarını gönder."});
  void Label(string t)=>panel.Controls.Add(new Label{Text=t,AutoSize=true});
  Label("Müşteri / firma adı");var customer=new TextBox{Width=720,MaxLength=120};panel.Controls.Add(customer);
  Label("Müşterinin uygulamada görünen cihaz kodu");var device=new TextBox{Width=720,MaxLength=100};panel.Controls.Add(device);
  var forever=new CheckBox{Text="Süresiz tam sürüm",Checked=true,AutoSize=true};panel.Controls.Add(forever);
  var expiry=new DateTimePicker{Width=230,Value=DateTime.Today.AddYears(1),Enabled=false};panel.Controls.Add(expiry);forever.CheckedChanged+=(_,_)=>expiry.Enabled=!forever.Checked;
  var issue=new Button{Text="Lisans anahtarı üret",AutoSize=true};panel.Controls.Add(issue);
  var result=new TextBox{Width=720,Height=190,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};panel.Controls.Add(result);
  var copy=new Button{Text="Anahtarı kopyala",AutoSize=true};panel.Controls.Add(copy);copy.Click+=(_,_)=>{if(result.Text.Length>0)Clipboard.SetText(result.Text);};
  var info=new Label{Width=720,Height=70};panel.Controls.Add(info);
  issue.Click+=(_,_)=>
  {
   try
   {
    using var key=RSA.Create();key.ImportFromPem(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"owner-private.pem")));
    using var expected=RSA.Create();expected.ImportFromPem(LicenseToken.PublicKey);
    if(!key.ExportSubjectPublicKeyInfo().SequenceEqual(expected.ExportSubjectPublicKeyInfo()))throw new LicenseException("Özel anahtar bu uygulama sürümünün yayıncı anahtarıyla eşleşmiyor.");
    DateTimeOffset? until=forever.Checked?null:new DateTimeOffset(DateTime.SpecifyKind(expiry.Value.Date.AddDays(1),DateTimeKind.Unspecified),TimeSpan.FromHours(3)).ToUniversalTime();
    result.Text=LicenseToken.Issue(key,device.Text,customer.Text,DateTimeOffset.UtcNow,until);
    info.Text=forever.Checked?"Süresiz lisans hazır. Yalnız bu cihaz/Windows kullanıcısı için geçerlidir.":$"Lisans {expiry.Value:dd.MM.yyyy} Türkiye saati gün sonuna kadar geçerlidir.";
   }
   catch(Exception e){result.Clear();info.Text=e is FileNotFoundException?"owner-private.pem bulunamadı. Sana özel paketin tüm dosyalarını aynı klasöre çıkar.":e.Message;}
  };
 }
}
