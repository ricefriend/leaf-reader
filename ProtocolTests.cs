using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LeafReader {
 static class ProtocolTests {
  static List<string> checks=new List<string>();
  static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL "+name);checks.Add("PASS "+name);}
  static int FreePort(){var l=new TcpListener(IPAddress.Loopback,0);l.Start();int port=((IPEndPoint)l.LocalEndpoint).Port;l.Stop();return port;}
  static string Raw(int port,string target,string token=null){using(var client=new TcpClient("127.0.0.1",port)){var stream=client.GetStream();client.ReceiveTimeout=10000;byte[] request=Encoding.ASCII.GetBytes("GET "+target+" HTTP/1.1\r\nHost: localhost\r\n"+(token==null?"":"Authorization: Bearer "+token+"\r\n")+"\r\n");stream.Write(request,0,request.Length);using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}
  static void Expect(Action action,string code){try{action();throw new Exception("Expected "+code);}catch(ApiException ex){Check(ex.Code==code,"unified error "+code);}}
  public static async Task<int> Run(string directory){
   Directory.CreateDirectory(directory);Settings.StatePath=System.IO.Path.Combine(directory,"client.json");DiskCache.DirectoryPath=System.IO.Path.Combine(directory,"cache");
   ShareServer service=null,other=null;try{
    string root=System.IO.Path.Combine(directory,"authorized 中文 # & 空格");string nested=System.IO.Path.Combine(root,"漫画 2");Directory.CreateDirectory(nested);string outside=System.IO.Path.Combine(directory,"private");Directory.CreateDirectory(outside);File.WriteAllText(System.IO.Path.Combine(outside,"secret.txt"),"not shared");
    var bitmap=new System.Windows.Media.Imaging.WriteableBitmap(600,900,96,96,System.Windows.Media.PixelFormats.Bgra32,null);int stride=600*4;byte[] pixels=new byte[stride*900];for(int i=0;i<pixels.Length;i+=4){pixels[i]=30;pixels[i+1]=140;pixels[i+2]=80;pixels[i+3]=255;}bitmap.WritePixels(new System.Windows.Int32Rect(0,0,600,900),pixels,stride,0);
    foreach(string name in new[]{"1 中文 # &.png","2.png","10.png"}){var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using(var f=File.Create(System.IO.Path.Combine(nested,name)))encoder.Save(f);}
    using(Boundary.Open(root,System.IO.Path.Combine(nested,"2.png"))){}Check(true,"authorized handle final path validation");
    var config=new ShareConfig{port=FreePort()};config.AddRoot(root);service=new ShareServer(config);service.Start();Check(service.Running,"server explicit startup");var unauth=new ServerProfile{Address="http://127.0.0.1:"+config.port,ProtectedToken=""};var publicApi=new RemoteApi(unauth);Check(Wire.I(publicApi.Get("/v1/info"),"protocol")==1,"versioned public service information");Expect(()=>publicApi.Get("/v1/books"),"AUTH_REQUIRED");
    Expect(()=>RemoteApi.Pair(unauth.Address,"wrong-code"),"PAIR_FAILED");
    var paired=RemoteApi.Pair(unauth.Address,service.PairCode);var api=new RemoteApi(paired);Check(api.Get("/v1/roots")["roots"] is object[],"authorized root catalog");var books=api.Books();Check(books.Count==1,"only authorized image folders exposed");api.LoadPages(books[0]);Check(books[0].Pages.Count==3&&books[0].Pages[1].Name=="2.png"&&books[0].Pages[2].Name=="10.png","natural order and unicode metadata end to end");var page=books[0].Pages[0];Check(page.Decode(256).PixelWidth==256,"desktop client reads remote thumbnail into existing Page pipeline");Check(page.Decode(1200).PixelWidth==600,"desktop compatible reading preview");
    Check(Raw(config.port,"/v1/files/%2e%2e%2fprivate%2fsecret.txt/original",paired.Token).StartsWith("HTTP/1.1 403"),"encoded path traversal denied");Expect(()=>api.Get("/v1/files/unknown/meta"),"NOT_FOUND");Check(!Boundary.Within(root,outside)&&!Boundary.Within(root,root+"-sibling"),"directory boundary checks sibling prefix");
    try{using(Boundary.Open(root,System.IO.Path.Combine(outside,"secret.txt"))){}throw new Exception("Outside file opened");}catch(ApiException ex){Check(ex.Code=="OUTSIDE_ROOT","direct source boundary denies outside file");}
    string junction=System.IO.Path.Combine(root,"junction");if(Directory.Exists(junction)){try{using(Boundary.Open(root,System.IO.Path.Combine(junction,"secret.txt"))){}throw new Exception("Junction opened");}catch(ApiException ex){Check(ex.Code=="OUTSIDE_ROOT","actual NTFS junction cannot escape authorized directory");}}
    try{config.AddRoot(System.IO.Path.GetPathRoot(root));throw new Exception("Whole volume shared");}catch(IOException){Check(true,"whole volume cannot be selected as a share");}
    string id=page.Identity,version=page.Version;File.SetLastWriteTimeUtc(System.IO.Path.Combine(nested,page.Name),DateTime.UtcNow.AddMinutes(1));Expect(()=>api.Request("/v1/files/"+page.FileId+"/preview?w=1200&version="+version,null,CancellationToken.None),"STALE_VERSION");api.LoadPages(books[0]);Check(books[0].Pages[0].Identity==id&&books[0].Pages[0].Version!=version,"stable file identity with version invalidation");
    Check(DiskCache.PathFor(Wire.Hash(paired.ServerId+"|"+page.FileId+"|"+version))!=DiskCache.PathFor(Wire.Hash("other|"+page.FileId+"|"+version)),"cache isolation between servers");
    var state=new Settings();state.Servers.Add(paired);state.Books[books[0].Id]=new Progress{PageId=books[0].Pages[2].Identity,Page=0};state.Save();var loaded=Settings.Load();Check(loaded.Servers[0].Token==paired.Token&&!File.ReadAllText(Settings.StatePath).Contains(paired.Token),"desktop credentials encrypted and reloadable");Check(loaded.Get(books[0]).Page==2,"progress resolves stable image identity rather than list index");
    var window=new MainWindow(true,new string[0]);window.WindowStartupLocation=System.Windows.WindowStartupLocation.Manual;window.Left=-20000;window.Top=-20000;window.Show();window.SetTestBooks(books);await window.OpenBook(books[0]);window.Reader.SetMode(2);window.Reader.Jump(1);await Task.Delay(600);window.UpdateLayout();Check(window.Reader.Book.RemoteId!=null&&window.Reader.VisibleCount==2,"existing WPF reader opens actual remote metadata and spread");var surface=(System.Windows.FrameworkElement)window.Content;var screen=new System.Windows.Media.Imaging.RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);screen.Render(surface);var shot=new System.Windows.Media.Imaging.PngBitmapEncoder();shot.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(screen));using(var imageFile=File.Create(System.IO.Path.Combine(directory,"windows-remote.png")))shot.Save(imageFile);window.Close();
    var canceled=new CancellationTokenSource();canceled.Cancel();try{api.Request("/v1/books",null,canceled.Token);throw new Exception("Request did not cancel");}catch(OperationCanceledException){Check(true,"outdated request cancellation");}
    byte[] original=api.Request("/v1/files/"+page.FileId+"/original",null,CancellationToken.None);Check(original.SequenceEqual(File.ReadAllBytes(System.IO.Path.Combine(nested,page.Name))),"original image stream exact bytes");
    other=new ShareServer(new ShareConfig{port=FreePort(),roots=config.roots});other.Start();var otherProfile=RemoteApi.Pair("http://127.0.0.1:"+other.Config.port,other.PairCode);Check(new RemoteApi(otherProfile).Books().Count==1&&otherProfile.ServerId!=paired.ServerId,"two independent server instances and desktop client");
    service.ResetPairing();Expect(()=>api.Get("/v1/books"),"AUTH_REQUIRED");paired=RemoteApi.Pair(unauth.Address,service.PairCode);api=new RemoteApi(paired);File.Delete(System.IO.Path.Combine(nested,page.Name));Expect(()=>api.Get("/v1/files/"+page.FileId+"/meta"),"NOT_FOUND");
    service.Dispose();try{api.Get("/v1/books");throw new Exception("Stopped service connected");}catch(IOException){Check(true,"service stopped yields actionable connection error");}service.Start();Check(api.Books().Count==1,"reconnect after service restart");
    Check(File.Exists(System.IO.Path.Combine(nested,"2.png")),"stopping shared service leaves local files intact");DiskCache.Clear();Check(Directory.GetFiles(DiskCache.DirectoryPath).Length==0,"cache cleanup entry point");
    string archivePath=System.IO.Path.Combine(root,"remote.cbz");if(File.Exists(archivePath))File.Delete(archivePath);using(var zip=System.IO.Compression.ZipFile.Open(archivePath,System.IO.Compression.ZipArchiveMode.Create)){System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(zip,System.IO.Path.Combine(nested,"2.png"),"chapter/2.png");}service.Refresh();var archiveBook=api.Books().First(b=>b.Title=="remote");api.LoadPages(archiveBook);Check(archiveBook.Pages.Count==1&&archiveBook.Pages[0].Decode(256).PixelWidth==256,"remote CBZ entry preview uses validated temporary stream without full archive transfer");File.Delete(archivePath);
    for(int i=0;i<2;i++)Expect(()=>RemoteApi.Pair(unauth.Address,"wrong-code"),"PAIR_FAILED");Expect(()=>RemoteApi.Pair(unauth.Address,"wrong-code"),"PAIR_RATE_LIMIT");
    service.Dispose();other.Dispose();checks.Add("ALL PROTOCOL TESTS PASSED");File.WriteAllLines(System.IO.Path.Combine(directory,"protocol-results.txt"),checks);await Task.CompletedTask;return 0;
   }catch(Exception ex){checks.Add(ex.ToString());File.WriteAllLines(System.IO.Path.Combine(directory,"protocol-results.txt"),checks);if(service!=null)service.Dispose();if(other!=null)other.Dispose();return 1;}
  }
 }
}
