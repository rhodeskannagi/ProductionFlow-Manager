namespace ProductionFlow.Desktop;
internal static class Program
{
 [STAThread] static void Main()
 {
  using var mutex=new Mutex(true,"Local\\ProductionFlowDesktop-"+Environment.UserName,out var first);
  if(!first){MessageBox.Show("ProductionFlow zaten açık.");return;}
  ApplicationConfiguration.Initialize();
  try{Application.Run(new MainForm());}
  catch(Exception e){MessageBox.Show("Uygulama açılamadı. Mevcut demo verisi korunmuştur.\n"+e.Message,"ProductionFlow",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  finally{mutex.ReleaseMutex();}
 }
}
