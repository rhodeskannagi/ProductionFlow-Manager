using ProductionFlow.Licensing;
namespace ProductionFlow.Desktop;
public sealed class LicenseDialog:Form
{
 public LicenseDialog(UsagePolicy policy)
 {
  Text="ProductionFlow • Lisans etkinleştir";Size=new(720,470);StartPosition=FormStartPosition.CenterParent;Font=new("Segoe UI",10);
  var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(16)};Controls.Add(panel);
  panel.Controls.Add(new Label{AutoSize=true,Text="Bu cihaz kodunu Ömer İrfan Akyol'a gönderip lisans anahtarını isteyin:"});
  var device=new TextBox{Width=660,ReadOnly=true,Text=policy.Device};panel.Controls.Add(device);
  var copy=new Button{Text="Cihaz kodunu kopyala",AutoSize=true};copy.Click+=(_,_)=>Clipboard.SetText(policy.Device);panel.Controls.Add(copy);
  panel.Controls.Add(new Label{Text="Lisans anahtarı",AutoSize=true});var token=new TextBox{Multiline=true,Width=660,Height=170,MaxLength=8192,ScrollBars=ScrollBars.Vertical};panel.Controls.Add(token);
  var activate=new Button{Text="Tam sürümü etkinleştir",AutoSize=true};panel.Controls.Add(activate);var info=new Label{Width=660,Height=60};panel.Controls.Add(info);
  activate.Click+=(_,_)=>{try{policy.Activate(token.Text);DialogResult=DialogResult.OK;Close();}catch(Exception e){info.Text=e.Message;}};
 }
}
