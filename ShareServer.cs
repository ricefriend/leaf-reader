using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Windows.Media.Imaging;

namespace LeafReader {
 public sealed class ShareRoot {public string id,path,name;}
 public sealed class ShareConfig {
  public string serverId=Guid.NewGuid().ToString("N");public int port=8765;
  public List<ShareRoot> roots=new List<ShareRoot>();public List<string> tokenHashes=new List<string>();
  public static string ConfigPath {get{return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Settings.StatePath),"sharing.json");}}
  public static ShareConfig Load(){try{return Wire.Json.Deserialize<ShareConfig>(File.ReadAllText(ConfigPath));}catch{return new ShareConfig();}}
  public void Save(){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ConfigPath));string temp=ConfigPath+".tmp";File.WriteAllText(temp,Wire.Json.Serialize(this));if(File.Exists(ConfigPath))File.Replace(temp,ConfigPath,null);else File.Move(temp,ConfigPath);}
  public void AddRoot(string path){path=System.IO.Path.GetFullPath(path);if(string.Equals(path.TrimEnd('\\','/'),System.IO.Path.GetPathRoot(path).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))throw new IOException("请共享一个具体的图片文件夹，不允许共享整个磁盘。");path=path.TrimEnd(System.IO.Path.DirectorySeparatorChar);Boundary.ValidateRoot(path);if(!roots.Any(r=>string.Equals(r.path,path,StringComparison.OrdinalIgnoreCase)))roots.Add(new ShareRoot{id=Guid.NewGuid().ToString("N"),path=path,name=new DirectoryInfo(path).Name});Save();}
 }
 public static class Boundary {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
  public static void ValidateRoot(string root){if(!Directory.Exists(root))throw new IOException("共享文件夹不存在");RejectLinks(root);}
  static void RejectLinks(string path){string current=System.IO.Path.GetFullPath(path);while(!string.IsNullOrEmpty(current)){if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new ApiException(403,"OUTSIDE_ROOT","不允许共享符号链接或联接点");var parent=Directory.GetParent(current);current=parent==null?null:parent.FullName;}}
  public static bool Within(string root,string path){string r=System.IO.Path.GetFullPath(root).TrimEnd('\\','/');string p=System.IO.Path.GetFullPath(path);return string.Equals(r,p,StringComparison.OrdinalIgnoreCase)||p.StartsWith(r+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  public static FileStream Open(string root,string path){
   if(!Within(root,path))throw new ApiException(403,"OUTSIDE_ROOT","文件位于共享范围之外");RejectLinks(path);
   var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
   try{var buffer=new StringBuilder(32768);if(GetFinalPathNameByHandle(stream.SafeFileHandle,buffer,32768,0)==0)throw new IOException("无法验证文件的实际路径，Windows 错误 "+Marshal.GetLastWin32Error());string final=buffer.ToString();if(final.StartsWith("\\\\?\\UNC\\"))final="\\\\"+final.Substring(8);else if(final.StartsWith("\\\\?\\"))final=final.Substring(4);if(!Within(root,final))throw new ApiException(403,"OUTSIDE_ROOT","文件实际位于共享范围之外");return stream;}catch{stream.Dispose();throw;}
  }
 }
 sealed class SharedSource:IImageSource {
  readonly ShareRoot root;public SharedSource(ShareRoot r){root=r;}public string Id{get{return root.id;}}public string Label{get{return root.name;}}
  public Stream Open(Page page,string variant,int width,CancellationToken cancellation){
   var file=Boundary.Open(root.path,page.Path);if(page.Entry==null)return file;
   using(file)using(var zip=new ZipArchive(file,ZipArchiveMode.Read)){var entry=zip.GetEntry(page.Entry);if(entry==null)throw new FileNotFoundException();if(entry.Length>200L*1024*1024)throw new IOException("压缩包单张图片超过 200 MB");var temporary=new FileStream(System.IO.Path.GetTempFileName(),FileMode.Create,FileAccess.ReadWrite,FileShare.Read,65536,FileOptions.DeleteOnClose);try{using(var input=entry.Open())input.CopyTo(temporary);temporary.Position=0;return temporary;}catch{temporary.Dispose();throw;}}
  }
 }
 sealed class SharedFile {public string id,rootId,version;public Page page;string infoVersion;internal int InfoReads;
  public void EnsureInfo(){lock(this){if(infoVersion==version)return;page.ReadInfo();InfoReads++;infoVersion=version;}}
  public object Metadata(){return new{id=id,name=page.Name,version=version,width=page.Width,height=page.Height,format=System.IO.Path.GetExtension(page.Name).ToLowerInvariant()};}}
 sealed class SharedBook {public string id,rootId,name,rootName,relativePath;public long createdTicks;public List<SharedFile> files;}
 sealed class Catalog {public Dictionary<string,SharedFile> files=new Dictionary<string,SharedFile>();public Dictionary<string,SharedBook> books=new Dictionary<string,SharedBook>();public List<object> directories=new List<object>();}
 public sealed class ShareServer:IDisposable {
  public readonly ShareConfig Config;public string PairCode {get;private set;}public DateTime PairExpires {get;private set;}
  TcpListener listener;CancellationTokenSource stop;readonly SemaphoreSlim workers=new SemaphoreSlim(4);readonly object stateGate=new object();
  Catalog catalog=new Catalog();readonly Dictionary<string,List<DateTime>> attempts=new Dictionary<string,List<DateTime>>();readonly HashSet<TcpClient> clients=new HashSet<TcpClient>();
  internal int MetadataReads {get{return catalog.files.Values.Sum(f=>f.InfoReads);}}
  internal int ActiveRequests {get{lock(stateGate)return clients.Count;}}
  public bool Running {get{return listener!=null;}}
  public ShareServer(ShareConfig config){Config=config;ResetPairing(false);}
  public void ResetPairing(bool revoke=true){lock(stateGate){if(revoke)Config.tokenHashes.Clear();byte[] data=new byte[8];using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(data);PairCode=(BitConverter.ToUInt64(data,0)%100000000UL).ToString("D8");PairExpires=DateTime.UtcNow.AddMinutes(10);Config.Save();}}
  static string Version(Page p){var info=new FileInfo(p.Path);return Wire.Hash(info.Length+"|"+info.LastWriteTimeUtc.Ticks+"|"+p.Entry).Substring(0,24);}
  public void Refresh(){
   var next=new Catalog();foreach(var root in Config.roots.ToArray()){
    Boundary.ValidateRoot(root.path);var result=Library.Scan(new[]{root.path});
    foreach(var b in result.Books){if(!Boundary.Within(root.path,b.Id))continue;string relative=b.Id.Substring(root.path.Length).TrimStart('\\','/');string id=Wire.Hash(root.id+"|book|"+relative.ToUpperInvariant());var shared=new SharedBook{id=id,rootId=root.id,name=b.Title,rootName=root.name,relativePath=relative.Replace('\\','/'),createdTicks=b.CreatedUtc.Ticks,files=new List<SharedFile>()};
     foreach(var p in b.Pages){try{using(Boundary.Open(root.path,p.Path)){}p.Source=new SharedSource(root);p.FileId=Wire.Hash(root.id+"|file|"+p.Path.Substring(root.path.Length).ToUpperInvariant()+"|"+p.Entry);var f=new SharedFile{id=p.FileId,rootId=root.id,version=Version(p),page=p};shared.files.Add(f);next.files[f.id]=f;}catch{}}
     if(shared.files.Count>0){shared.files[0].EnsureInfo();next.books[id]=shared;}
    }
    var pending=new Stack<string>();pending.Push(root.path);while(pending.Count>0){string dir=pending.Pop();try{string rel=dir.Substring(root.path.Length).TrimStart('\\','/');string id=Wire.Hash(root.id+"|dir|"+rel.ToUpperInvariant());string parent=dir==root.path?null:Wire.Hash(root.id+"|dir|"+(Directory.GetParent(dir).FullName.Substring(root.path.Length).TrimStart('\\','/')).ToUpperInvariant());next.directories.Add(new{id=id,rootId=root.id,parentId=parent,name=new DirectoryInfo(dir).Name});foreach(string child in Directory.GetDirectories(dir).OrderByDescending(x=>x,NaturalOrder.Instance))if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Push(child);}catch{}}
   }catalog=next;
  }
  public void Start(){if(Running)return;if(Config.roots.Count==0)throw new IOException("请先选择至少一个允许共享的文件夹。");if(Config.port<1024||Config.port>65535)throw new IOException("端口应为 1024–65535");Refresh();stop=new CancellationTokenSource();var l=new TcpListener(IPAddress.Any,Config.port);l.Start(16);listener=l;Task.Run(()=>Accept(l,stop.Token));}
  async Task Accept(TcpListener l,CancellationToken cancellation){while(!cancellation.IsCancellationRequested){TcpClient client;try{client=await l.AcceptTcpClientAsync();}catch{return;}if(!Wire.PrivateAddress(((IPEndPoint)client.Client.RemoteEndPoint).Address)){client.Close();continue;}if(!workers.Wait(0)){Busy(client);client.Close();continue;}lock(stateGate)clients.Add(client);Task.Run(()=>{try{Serve(client);}catch{}finally{lock(stateGate)clients.Remove(client);client.Close();workers.Release();}});}}
  static void Busy(TcpClient client){try{
   client.ReceiveTimeout=1000;client.SendTimeout=1000;var stream=client.GetStream();Line(stream,8192);int total=0,length=0;
   while(true){string line=Line(stream,8192);total+=line.Length;if(total>32768)return;if(line=="")break;if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)&&(!int.TryParse(line.Substring(15).Trim(),out length)||length<0||length>8192))return;}
   // Drain the bounded request before closing so TCP does not discard the JSON response.
   for(int i=0;i<length;i++)if(stream.ReadByte()<0)return;Error(stream,503,"SERVER_BUSY","共享服务繁忙，请稍后重试");
  }catch{}}
  static string Line(Stream input,int limit){var bytes=new List<byte>();int c;while((c=input.ReadByte())>=0){if(c==10)break;if(bytes.Count>=limit)throw new IOException("请求头过长");if(c!=13)bytes.Add((byte)c);}return Encoding.ASCII.GetString(bytes.ToArray());}
  void Serve(TcpClient client){client.ReceiveTimeout=10000;client.SendTimeout=15000;using(var stream=client.GetStream()){
   try{string first=Line(stream,8192);var parts=first.Split(' ');if(parts.Length!=3)throw new ApiException(400,"BAD_REQUEST","无效 HTTP 请求");var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);int total=0;while(true){string h=Line(stream,8192);total+=h.Length;if(total>32768)throw new ApiException(400,"BAD_REQUEST","请求头过长");if(h=="")break;int colon=h.IndexOf(':');if(colon<1||headers.ContainsKey(h.Substring(0,colon)))throw new ApiException(400,"BAD_REQUEST","无效请求头");headers[h.Substring(0,colon)]=h.Substring(colon+1).Trim();}
    if(headers.ContainsKey("Transfer-Encoding"))throw new ApiException(400,"BAD_REQUEST","不支持分块请求体");int length=0;if(headers.ContainsKey("Content-Length")&&(!int.TryParse(headers["Content-Length"],out length)||length<0||length>8192))throw new ApiException(400,"BAD_REQUEST","无效请求体长度");byte[] body=new byte[length];for(int i=0;i<length;){int n=stream.Read(body,i,length-i);if(n==0)throw new IOException("请求中断");i+=n;}
    string target=parts[1];if(!target.StartsWith("/")||target.Contains("#"))throw new ApiException(400,"BAD_REQUEST","无效地址");int queryIndex=target.IndexOf('?');string path=Uri.UnescapeDataString(queryIndex<0?target:target.Substring(0,queryIndex));var query=System.Web.HttpUtility.ParseQueryString(queryIndex<0?"":target.Substring(queryIndex+1));
    if(path.Contains("..")||path.Contains("\\")||path.Contains("\0"))throw new ApiException(403,"OUTSIDE_ROOT","无效资源标识");
    if(parts[0]=="GET"&&path=="/v1/info"){Json(stream,new{protocol=1,serverId=Config.serverId,name=Environment.MachineName,formats=new[]{"jpg","png","bmp","gif:first","tiff:first","webp:system"},preview="image/jpeg",readOnly=true});return;}
    if(parts[0]=="POST"&&path=="/v1/pair"){
     var request=Wire.Obj(Wire.Json.DeserializeObject(Encoding.UTF8.GetString(body)));string ip=((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();lock(stateGate){List<DateTime> tries;if(!attempts.TryGetValue(ip,out tries)){if(attempts.Count>=512)attempts.Clear();attempts[ip]=tries=new List<DateTime>();}tries.RemoveAll(t=>t<DateTime.UtcNow.AddMinutes(-1));if(tries.Count>=5)throw new ApiException(429,"PAIR_RATE_LIMIT","配对尝试过多，请一分钟后重试");tries.Add(DateTime.UtcNow);if(DateTime.UtcNow>PairExpires)throw new ApiException(401,"PAIR_EXPIRED","配对码已过期，请在电脑上重新生成");if(!ConstantEqual(Wire.S(request,"code"),PairCode))throw new ApiException(401,"PAIR_FAILED","配对码错误");string token=Wire.RandomToken();Config.tokenHashes.Add(Wire.Hash(token));Config.Save();Json(stream,new{serverId=Config.serverId,token=token});return;}
    }
    string auth;headers.TryGetValue("Authorization",out auth);lock(stateGate){if(auth==null||!auth.StartsWith("Bearer ")||!Config.tokenHashes.Contains(Wire.Hash(auth.Substring(7))))throw new ApiException(401,"AUTH_REQUIRED","授权失效，请重新配对");}
    if(parts[0]!="GET")throw new ApiException(405,"READ_ONLY","共享服务仅支持只读访问");var current=catalog;
    if(path=="/v1/roots"){Json(stream,new{roots=Config.roots.OrderBy(r=>r.name,NaturalOrder.Instance).Select(r=>new{id=r.id,name=r.name}).ToArray()});return;}
    if(path=="/v1/directories"){var dirs=current.directories.Cast<object>().Where(d=>{var map=Wire.Obj(Wire.Json.DeserializeObject(Wire.Json.Serialize(d)));return string.IsNullOrEmpty(query["rootId"])||Wire.S(map,"rootId")==query["rootId"];}).ToArray();Json(stream,new{directories=dirs});return;}
    if(path=="/v1/books"){var available=new List<object>();foreach(var b in current.books.Values.OrderBy(b=>b.name,NaturalOrder.Instance)){var coverFile=b.files.FirstOrDefault(f=>File.Exists(f.page.Path));if(coverFile==null)continue;EnsureFile(coverFile,null);coverFile.EnsureInfo();available.Add(new{id=b.id,rootId=b.rootId,rootName=b.rootName,relativePath=b.relativePath,name=b.name,createdTicks=b.createdTicks.ToString(),pageCount=b.files.Count,cover=coverFile.Metadata()});}Json(stream,new{books=available.ToArray()});return;}
    if(path.StartsWith("/v1/books/")&&path.EndsWith("/pages")){string id=path.Substring(10,path.Length-16);SharedBook book;if(!current.books.TryGetValue(id,out book))throw new ApiException(404,"NOT_FOUND","目录已删除，请刷新书架");foreach(var f in book.files){EnsureFile(f,null);f.EnsureInfo();}Json(stream,new{files=book.files.Select(f=>f.Metadata()).ToArray()});return;}
    if(path.StartsWith("/v1/files/")){string[] segments=path.Split('/');if(segments.Length!=5)throw new ApiException(404,"NOT_FOUND","资源不存在");SharedFile file;if(!current.files.TryGetValue(segments[3],out file))throw new ApiException(404,"NOT_FOUND","文件已删除，请刷新书架");EnsureFile(file,query["version"]);string kind=segments[4];if(kind=="meta"){file.EnsureInfo();Json(stream,file.Metadata());return;}
     if(kind=="original"){using(var image=file.page.Open()){SendHeader(stream,200,"application/octet-stream",image.Length,file.version);image.CopyTo(stream);}return;}
     if(kind=="thumbnail"||kind=="preview"){int width;int.TryParse(query["w"],out width);width=Math.Max(64,Math.Min(kind=="thumbnail"?512:8192,width==0?1600:width));file.EnsureInfo();if(file.page.Error!=null)throw new ApiException(415,"UNSUPPORTED_FORMAT","此图片的系统解码器不可用，请转换为 JPG / PNG");
      // Decode at most 8 MP for a compatible, EXIF-oriented JPEG preview.
      width=Math.Min(width,Math.Max(1,(int)Math.Sqrt(8000000.0*file.page.Width/file.page.Height)));var bitmap=file.page.Decode(width);var encoder=new JpegBitmapEncoder{QualityLevel=88};encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var bytes=new MemoryStream()){encoder.Save(bytes);SendHeader(stream,200,"image/jpeg",bytes.Length,file.version);bytes.Position=0;bytes.CopyTo(stream);}return;}
    }throw new ApiException(404,"NOT_FOUND","接口不存在");
   }catch(ApiException ex){Error(stream,ex.Status,ex.Code,ex.Message);}catch(FileNotFoundException){Error(stream,404,"NOT_FOUND","文件已删除，请刷新书架");}catch(DirectoryNotFoundException){Error(stream,404,"NOT_FOUND","共享目录已删除");}catch(Exception ex){Error(stream,500,"READ_FAILED",ex is IOException?"读取失败，请检查文件是否仍可用或重新连接":"请求无法处理");}
  }}
  void EnsureFile(SharedFile f,string version){if(!File.Exists(f.page.Path))throw new FileNotFoundException();using(Boundary.Open(Config.roots.First(r=>r.id==f.rootId).path,f.page.Path)){}string latest=Version(f.page);if(latest!=f.version){f.version=latest;f.page.Error=null;}if(version!=null&&version!=latest)throw new ApiException(409,"STALE_VERSION","文件已更新，请刷新书架后重新打开");}
  static bool ConstantEqual(string a,string b){int difference=a.Length^b.Length;for(int i=0;i<Math.Max(a.Length,b.Length);i++)difference|=(i<a.Length?a[i]:0)^(i<b.Length?b[i]:0);return difference==0;}
  static void SendHeader(Stream s,int status,string type,long length,string etag=null){byte[] bytes=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" Response\r\nContent-Type: "+type+"\r\nContent-Length: "+length+"\r\nConnection: close\r\nCache-Control: private, no-store\r\nX-Content-Type-Options: nosniff\r\n"+(etag==null?"":"ETag: \""+etag+"\"\r\n")+"\r\n");s.Write(bytes,0,bytes.Length);}
  static void Json(Stream s,object value){byte[] bytes=Encoding.UTF8.GetBytes(Wire.Json.Serialize(value));SendHeader(s,200,"application/json; charset=utf-8",bytes.Length);s.Write(bytes,0,bytes.Length);}
  static void Error(Stream s,int status,string code,string message){try{byte[] bytes=Encoding.UTF8.GetBytes(Wire.Json.Serialize(new{error=new{code=code,message=message}}));SendHeader(s,status,"application/json; charset=utf-8",bytes.Length);s.Write(bytes,0,bytes.Length);}catch{}}
  public string[] Addresses(){return Dns.GetHostAddresses(Dns.GetHostName()).Where(ip=>ip.AddressFamily==AddressFamily.InterNetwork&&Wire.PrivateAddress(ip)&&!IPAddress.IsLoopback(ip)).Select(ip=>"http://"+ip+":"+Config.port).Concat(new[]{"http://127.0.0.1:"+Config.port}).ToArray();}
  public void Dispose(){var l=listener;listener=null;if(stop!=null)stop.Cancel();if(l!=null)l.Stop();lock(stateGate)foreach(var client in clients.ToArray())client.Close();}
 }
}
