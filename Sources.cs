using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Security.Cryptography;

namespace LeafReader {
 public interface IImageSource {
  string Id {get;}
  string Label {get;}
  Stream Open(Page page,string variant,int width,CancellationToken cancellation);
 }
 public sealed class LocalImageSource:IImageSource {
  public static readonly LocalImageSource Instance=new LocalImageSource();
  public string Id {get{return "local";}} public string Label {get{return L10n.T("本地");}}
  public Stream Open(Page page,string variant,int width,CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();return page.OpenLocal();}
 }
 [DataContract] public sealed class ServerProfile {
  [DataMember] public string ServerId,Address,Name,ProtectedToken;
  public string Token {get{try{return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedToken),null,DataProtectionScope.CurrentUser));}catch{return "";}}set{ProtectedToken=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));}}
 }
 public sealed class ApiException:IOException {public readonly string Code;public readonly int Status;public ApiException(int status,string code,string message):base(message){Status=status;Code=code;}}
 public static class Wire {
  public static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024,RecursionLimit=64};
  public static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
  public static string RandomToken(int bytes=32){var data=new byte[bytes];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(data);return Convert.ToBase64String(data).TrimEnd('=').Replace('+','-').Replace('/','_');}
  public static bool PrivateAddress(IPAddress ip){if(IPAddress.IsLoopback(ip))return true;var b=ip.GetAddressBytes();if(b.Length==4)return b[0]==100&&b[1]>=64&&b[1]<=127||b[0]==10||b[0]==192&&b[1]==168||b[0]==172&&b[1]>=16&&b[1]<=31||b[0]==169&&b[1]==254;return ip.IsIPv6LinkLocal||(b[0]&0xfe)==0xfc;}
  public static string Address(string address){
   Uri uri;if(!Uri.TryCreate(address.Trim().TrimEnd('/'),UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!=""||uri.AbsolutePath!="/")throw new IOException(L10n.T("地址应为 http://192.168.1.10:8765 或受保护的 HTTPS 服务根地址。"));
   if(uri.Scheme=="http"&&!Dns.GetHostAddresses(uri.DnsSafeHost).All(PrivateAddress))throw new IOException(L10n.T("HTTP 仅允许局域网、回环或私有 VPN 地址；跨网络请使用 HTTPS。 "));return uri.GetLeftPart(UriPartial.Authority);
  }
  public static Dictionary<string,object> Obj(object value){return (Dictionary<string,object>)value;}
  public static string S(Dictionary<string,object> d,string k){return d.ContainsKey(k)&&d[k]!=null?Convert.ToString(d[k],System.Globalization.CultureInfo.InvariantCulture):"";}
  public static int I(Dictionary<string,object> d,string k){int n;return int.TryParse(S(d,k),out n)?n:0;}
 }
 public sealed class RemoteApi {
  public readonly ServerProfile Profile;
  public RemoteApi(ServerProfile profile){Profile=profile;}
  public byte[] Request(string path,string body,CancellationToken cancellation,int maxBytes=16*1024*1024){
   string address=Wire.Address(Profile.Address);var req=(HttpWebRequest)WebRequest.Create(address+path);req.AllowAutoRedirect=false;req.Timeout=15000;req.ReadWriteTimeout=15000;req.Proxy=null;req.KeepAlive=false;
   if(!string.IsNullOrEmpty(Profile.Token))req.Headers["Authorization"]="Bearer "+Profile.Token;
   using(cancellation.Register(()=>req.Abort())) {
    try{if(body!=null){req.Method="POST";req.ContentType="application/json";byte[] bytes=Encoding.UTF8.GetBytes(body);req.ContentLength=bytes.Length;using(var input=req.GetRequestStream())input.Write(bytes,0,bytes.Length);}
     using(var response=(HttpWebResponse)req.GetResponse()){if((int)response.StatusCode!=200)throw new IOException(L10n.T("服务返回异常响应"));using(var input=response.GetResponseStream())using(var data=new MemoryStream()){byte[] block=new byte[65536];int n;while((n=input.Read(block,0,block.Length))>0){cancellation.ThrowIfCancellationRequested();if(data.Length+n>maxBytes)throw new IOException(L10n.T("响应过大，请使用预览图"));data.Write(block,0,n);}return data.ToArray();}}
    }catch(WebException ex){cancellation.ThrowIfCancellationRequested();var response=ex.Response as HttpWebResponse;if(response!=null){using(response)using(var input=response.GetResponseStream())using(var reader=new StreamReader(input)){string content=reader.ReadToEnd();try{var error=Wire.Obj(Wire.Json.DeserializeObject(content));var detail=Wire.Obj(error["error"]);throw new ApiException((int)response.StatusCode,Wire.S(detail,"code"),L10n.T(Wire.S(detail,"message")));}catch(ApiException){throw;}catch{throw new IOException(L10n.T("连接失败，HTTP ")+(int)response.StatusCode);}}}throw new IOException(L10n.T("无法连接：请检查共享是否开启、地址和端口、电脑休眠或网络连接。"),ex);}
   }
  }
  public Dictionary<string,object> Get(string path){return Wire.Obj(Wire.Json.DeserializeObject(Encoding.UTF8.GetString(Request(path,null,CancellationToken.None))));}
  public static ServerProfile Pair(string address,string code){var profile=new ServerProfile{Address=Wire.Address(address),ProtectedToken=""};var api=new RemoteApi(profile);var info=api.Get("/v1/info");if(Wire.I(info,"protocol")!=1)throw new IOException(L10n.T("服务协议版本不兼容"));var reply=Wire.Obj(Wire.Json.DeserializeObject(Encoding.UTF8.GetString(api.Request("/v1/pair",Wire.Json.Serialize(new{code=code,client="Windows Leaf Reader"}),CancellationToken.None))));profile.ServerId=Wire.S(reply,"serverId");profile.Name=Wire.S(info,"name");profile.Token=Wire.S(reply,"token");return profile;}
  public List<Book> Books(){var info=Get("/v1/info");if(Wire.S(info,"serverId")!=Profile.ServerId)throw new IOException(L10n.T("该地址的服务器身份已改变，请重新配对。"));var result=new List<Book>();var reply=Get("/v1/books");foreach(var item in (object[])reply["books"]){var b=Wire.Obj(item);var remote=new RemoteImageSource(Profile);var book=new Book{Id="remote:"+Profile.ServerId+":"+Wire.S(b,"id"),RemoteId=Wire.S(b,"id"),SourceKey="remote:"+Profile.ServerId+":"+Wire.S(b,"rootId"),SourceName=Profile.Name+" / "+Wire.S(b,"rootName"),Title=Wire.S(b,"name"),Folder=Wire.S(b,"rootName")+" / "+Wire.S(b,"relativePath"),SourceLabel=L10n.T("远程 · ")+Profile.Name,CreatedUtc=new DateTime(long.Parse(Wire.S(b,"createdTicks")),DateTimeKind.Utc)};var cover=Wire.Obj(b["cover"]);book.Pages.Add(remote.PageFrom(cover));book.RemotePageCount=Wire.I(b,"pageCount");result.Add(book);}return result;}
  public void LoadPages(Book book){var reply=Get("/v1/books/"+Uri.EscapeDataString(book.RemoteId)+"/pages");var source=new RemoteImageSource(Profile);book.Pages=((object[])reply["files"]).Select(x=>source.PageFrom(Wire.Obj(x))).ToList();if(book.Pages.Count==0)throw new IOException(L10n.T("远程目录没有可阅读图片，请刷新书架。"));}
 }
 public sealed class RemoteImageSource:IImageSource {
  readonly ServerProfile profile;public RemoteImageSource(ServerProfile value){profile=value;}
  public string Id {get{return profile.ServerId;}}public string Label {get{return L10n.T("远程 · ")+profile.Name;}}
  public Page PageFrom(Dictionary<string,object> d){var p=new Page{Path="remote:"+profile.ServerId+":"+Wire.S(d,"id"),FileId=Wire.S(d,"id"),FileName=Wire.S(d,"name"),Version=Wire.S(d,"version"),Source=this};p.SetInfo(Math.Max(1,Wire.I(d,"width")),Math.Max(1,Wire.I(d,"height")));return p;}
  public Stream Open(Page page,string variant,int width,CancellationToken cancellation){
   string key=Wire.Hash(Id+"|"+page.FileId+"|"+page.Version+"|"+variant+"|"+width);string path=DiskCache.PathFor(key);
   byte[] bytes=DiskCache.Read(path);if(bytes==null){bytes=new RemoteApi(profile).Request("/v1/files/"+Uri.EscapeDataString(page.FileId)+"/"+variant+"?w="+width+"&version="+Uri.EscapeDataString(page.Version),null,cancellation,64*1024*1024);cancellation.ThrowIfCancellationRequested();DiskCache.Write(path,bytes);}
   return new MemoryStream(bytes,false);
  }
 }
 public static class DiskCache {
  static readonly object Gate=new object();public static string DirectoryPath=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LeafReader","remote-cache");
  public static string PathFor(string key){return System.IO.Path.Combine(DirectoryPath,key+".bin");}
  public static byte[] Read(string path){lock(Gate){try{if(!File.Exists(path))return null;File.SetLastWriteTimeUtc(path,DateTime.UtcNow);return File.ReadAllBytes(path);}catch{return null;}}}
  public static void Write(string path,byte[] bytes){lock(Gate){Directory.CreateDirectory(DirectoryPath);string temp=path+".tmp";File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Delete(path);File.Move(temp,path);long total=0;foreach(var f in new DirectoryInfo(DirectoryPath).GetFiles("*.bin").OrderByDescending(f=>f.LastWriteTimeUtc)){total+=f.Length;if(total>256L*1024*1024)f.Delete();}}}
  public static void Clear(){lock(Gate){if(Directory.Exists(DirectoryPath))foreach(string f in Directory.GetFiles(DirectoryPath))File.Delete(f);}}
 }
}
