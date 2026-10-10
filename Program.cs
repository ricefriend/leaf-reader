using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

[assembly: AssemblyTitle("Leaf Reader")]
[assembly: AssemblyDescription("Lightweight comic and image reader")]
[assembly: AssemblyCompany("Leaf Reader")]
[assembly: AssemblyProduct("Leaf Reader")]
[assembly: AssemblyVersion("2.3.2.0")]
namespace LeafReader {
 public static class Program {
  [STAThread] public static int Main(string[] args){
   AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{if(new AssemblyName(e.Name).Name!="QRCoder")return null;using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("QRCoder.dll"))using(var bytes=new MemoryStream()){input.CopyTo(bytes);return Assembly.Load(bytes.ToArray());}};
   var app=new Application();
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LeafReader.Theme.xaml"))app.Resources=(ResourceDictionary)XamlReader.Load(stream);
   app.DispatcherUnhandledException+=(s,e)=>{
    string path=Path.Combine(Path.GetDirectoryName(Settings.StatePath),"error.log");try{Directory.CreateDirectory(Path.GetDirectoryName(path));File.AppendAllText(path,DateTime.Now+"\r\n"+e.Exception+"\r\n");}catch{}
    MessageBox.Show(L10n.T("出现错误：")+e.Exception.Message+L10n.T("\n错误详情：")+path,"Leaf Reader");e.Handled=true;
   };
   if(args.Length>=2&&args[0]=="--self-test"){
    app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
    app.Startup+=async(s,e)=>{int result=await SelfTests.Run(Path.GetFullPath(args[1]));app.Shutdown(result);};return app.Run();
   }
   if(args.Length>=2&&args[0]=="--protocol-test"){app.ShutdownMode=ShutdownMode.OnExplicitShutdown;app.Startup+=async(s,e)=>{int result=await ProtocolTests.Run(Path.GetFullPath(args[1]));app.Shutdown(result);};return app.Run();}
   if(args.Length>=2&&args[0]=="--photo-cache-test"){app.ShutdownMode=ShutdownMode.OnExplicitShutdown;app.Startup+=async(s,e)=>{int result=await PhotoCacheTests.Run(Path.GetFullPath(args[1]));app.Shutdown(result);};return app.Run();}
   if(args.Length>=2&&args[0]=="--fixture-server"){app.ShutdownMode=ShutdownMode.OnExplicitShutdown;app.Startup+=async(s,e)=>await FixtureHost.Run(Path.GetFullPath(args[1]),app);return app.Run();}
   bool created;using(var mutex=new Mutex(true,"Local\\LeafReader.Desktop.1",out created)){
    if(!created){MessageBox.Show(L10n.T("阅读器已经运行，请使用现有窗口导入文件或文件夹。"),"Leaf Reader");return 0;}
    return app.Run(new MainWindow(false,args));
   }
  }
 }
}
