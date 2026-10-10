using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.IO.Compression;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Threading;

namespace LeafReader {
 public sealed class NaturalOrder : IComparer<string> {
  public static readonly NaturalOrder Instance = new NaturalOrder();
  public int Compare(string a,string b) {
   a=a??""; b=b??""; int i=0,j=0;
   while(i<a.Length && j<b.Length) {
    if(char.IsDigit(a[i]) && char.IsDigit(b[j])) {
     int ai=i,bj=j; while(i<a.Length&&char.IsDigit(a[i]))i++; while(j<b.Length&&char.IsDigit(b[j]))j++;
     string aa=a.Substring(ai,i-ai).TrimStart('0'),bb=b.Substring(bj,j-bj).TrimStart('0');
     int c=aa.Length.CompareTo(bb.Length); if(c!=0)return c; c=string.CompareOrdinal(aa,bb); if(c!=0)return c;
    } else { int c=char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j])); if(c!=0)return c; i++;j++; }
   }
   int end=(a.Length-i).CompareTo(b.Length-j); return end!=0?end:string.CompareOrdinal(a,b);
  }
 }
 public class Page {
  readonly object decodeGate=new object();
  public IImageSource Source;
  public string FileId,FileName,Version;public long EncodedBytes;
  public string Path,Entry; public int Width=800,Height=1200; int rawWidth=800,rawHeight=1200,orientation=1; public string Error;
  public string Name {get{return FileName??Entry??System.IO.Path.GetFileName(Path);}}
  public string Identity {get{return Source is RemoteImageSource?Source.Id+"|"+FileId:Path+"|"+Entry;}}
  public string Key {get{return Identity+"|"+Version;}}
  public void SetInfo(int width,int height){Width=rawWidth=width;Height=rawHeight=height;orientation=1;}
  public Stream Open(){return (Source??LocalImageSource.Instance).Open(this,"original",0,CancellationToken.None);}
  public Stream OpenLocal() {
   if(Entry==null)return new FileStream(Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
   using(var zip=ZipFile.OpenRead(Path)) {
    var item=zip.GetEntry(Entry); if(item==null)throw new IOException(L10n.T("压缩包内的图片已不存在"));
    if(item.Length>200*1024*1024)throw new IOException(L10n.T("单张图片超过 200 MB"));
    var data=new MemoryStream(); using(var input=item.Open())input.CopyTo(data); data.Position=0; return data;
   }
  }
  public void ReadInfo(){lock(decodeGate)ReadInfoCore();}
  void ReadInfoCore() {
   if(Source is RemoteImageSource)return;
   Error=null;orientation=1;
   try {if(File.Exists(Path)){var file=new FileInfo(Path);if(Entry==null)EncodedBytes=file.Length;Version=file.LastWriteTimeUtc.Ticks+"-"+file.Length;}using(var stream=Open()) {var d=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand); rawWidth=d.Frames[0].PixelWidth;rawHeight=d.Frames[0].PixelHeight;
    try{var metadata=d.Frames[0].Metadata as BitmapMetadata;if(metadata!=null){object value=metadata.GetQuery("/app1/ifd/{ushort=274}")??metadata.GetQuery("/ifd/{ushort=274}");if(value!=null)orientation=Convert.ToInt32(value);}}catch{}
    Width=orientation>=5&&orientation<=8?rawHeight:rawWidth;Height=orientation>=5&&orientation<=8?rawWidth:rawHeight;if(Width<1||Height<1)throw new IOException(L10n.T("无效尺寸"));}}
   catch(Exception ex){Error=L10n.T("无法读取：")+ex.Message;}
  }
  public BitmapSource Decode(int size){return Decode(size,CancellationToken.None);}
  public BitmapSource Decode(int size,CancellationToken cancellation){lock(decodeGate)return DecodeCore(size,cancellation);}
  BitmapSource DecodeCore(int size,CancellationToken cancellation) {
   using(var stream=(Source??LocalImageSource.Instance).Open(this,size<=512?"thumbnail":"preview",size,cancellation)) {cancellation.ThrowIfCancellationRequested();var b=new BitmapImage(); b.BeginInit();b.CacheOption=BitmapCacheOption.OnLoad;
    // Bound both extreme webtoon images and wide panoramas to 16 megapixels.
    double scale=Math.Min(1,Math.Min((double)size/Math.Max(1,Width),Math.Sqrt(16000000.0/((double)Width*Height))));
    b.DecodePixelWidth=Math.Max(1,(int)(rawWidth*scale)); b.StreamSource=stream;b.EndInit();b.Freeze();
    if(orientation<2||orientation>8)return b;Matrix m=Matrix.Identity;
    switch(orientation){case 2:m=new Matrix(-1,0,0,1,0,0);break;case 3:m=new Matrix(-1,0,0,-1,0,0);break;case 4:m=new Matrix(1,0,0,-1,0,0);break;case 5:m=new Matrix(0,1,1,0,0,0);break;case 6:m=new Matrix(0,1,-1,0,0,0);break;case 7:m=new Matrix(0,-1,-1,0,0,0);break;case 8:m=new Matrix(0,-1,1,0,0,0);break;}
    var oriented=new TransformedBitmap(b,new MatrixTransform(m));oriented.Freeze();return oriented;}
  }
 }
 public class Book {
  public string SourceKey,SourceName,GroupId;public int GroupCount;
  public string RemoteId,SourceLabel;public int RemotePageCount;
  public int PageCount {get{return RemotePageCount>0?RemotePageCount:Pages.Count;}}
  public string Id,Title,Folder; public DateTime CreatedUtc; public List<Page> Pages=new List<Page>();
 }
 public class ScanResult { public List<Book> Books=new List<Book>(); public List<string> Warnings=new List<string>(); }
 public static class Library {
  static readonly HashSet<string> Extensions=new HashSet<string>(new[]{".jpg",".jpeg",".png",".bmp",".gif",".tif",".tiff",".webp"},StringComparer.OrdinalIgnoreCase);
  public static bool IsImage(string p){return Extensions.Contains(System.IO.Path.GetExtension(p));}
  public static IEnumerable<Book> ByCreated(IEnumerable<Book> books,bool newestFirst){
   var ordered=newestFirst?books.OrderByDescending(b=>b.CreatedUtc):books.OrderBy(b=>b.CreatedUtc);
   return ordered.ThenBy(b=>b.Title,NaturalOrder.Instance).ThenBy(b=>b.Id,NaturalOrder.Instance);
  }
  public static bool IsArchive(string p){return string.Equals(System.IO.Path.GetExtension(p),".cbz",StringComparison.OrdinalIgnoreCase)||string.Equals(System.IO.Path.GetExtension(p),".zip",StringComparison.OrdinalIgnoreCase);}
  public static ScanResult Scan(IEnumerable<string> roots) {
   var result=new ScanResult();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var archives=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var pending=new Stack<string>(roots.Reverse());
   while(pending.Count>0) {
    string root=pending.Pop();
    try {
     root=System.IO.Path.GetFullPath(root);
     if(File.Exists(root)&&IsArchive(root)) {if(archives.Add(root))AddArchive(root,result);continue;}
     if(!seen.Add(root))continue;
     if(!Directory.Exists(root)){result.Warnings.Add(L10n.T("找不到文件夹：")+root);continue;}
     var files=Directory.GetFiles(root).OrderBy(p=>System.IO.Path.GetFileName(p),NaturalOrder.Instance).ToArray();
     var pages=files.Where(IsImage).Select(p=>new Page{Path=p}).ToList();
     if(pages.Count>0)result.Books.Add(new Book{Id=root,Title=new DirectoryInfo(root).Name,Folder=root,CreatedUtc=Directory.GetCreationTimeUtc(root),Pages=pages});
     foreach(var zip in files.Where(IsArchive))if(archives.Add(zip))AddArchive(zip,result);
     foreach(var dir in Directory.GetDirectories(root).OrderByDescending(p=>p,NaturalOrder.Instance)) {
      try {if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)==0)pending.Push(dir);}catch(Exception ex){result.Warnings.Add(dir+"："+ex.Message);}
     }
    } catch(Exception ex){result.Warnings.Add(root+"："+ex.Message);}
   }
   foreach(var book in result.Books)ShelfGroups.AssignLocal(book,roots);
   result.Books=result.Books.OrderBy(b=>b.Title,NaturalOrder.Instance).ThenBy(b=>b.Id,NaturalOrder.Instance).ToList();return result;
  }
  static void AddArchive(string path,ScanResult result){
   try{using(var zip=ZipFile.OpenRead(path)) {
    var pages=zip.Entries.Where(e=>!string.IsNullOrEmpty(e.Name)&&IsImage(e.Name)&&!e.FullName.StartsWith("__MACOSX/",StringComparison.OrdinalIgnoreCase)).OrderBy(e=>e.FullName,NaturalOrder.Instance).Select(e=>new Page{Path=path,Entry=e.FullName,EncodedBytes=e.Length}).ToList();
    if(pages.Count>0)result.Books.Add(new Book{Id=path,Title=System.IO.Path.GetFileNameWithoutExtension(path),Folder=System.IO.Path.GetDirectoryName(path),CreatedUtc=File.GetCreationTimeUtc(path),Pages=pages});
   }}catch(Exception ex){result.Warnings.Add(path+"："+ex.Message);}
  }
 }
 [DataContract] public class Progress {
  [DataMember] public int Page;
  [DataMember] public string PageId;
  [DataMember] public string PairAnchorId;
  [DataMember] public List<string> BookmarkIds;
  [DataMember] public int Mode;
  [DataMember] public int PairAnchor;
  [DataMember] public double Fraction;
  [DataMember] public bool Cover=true;
  [DataMember] public bool Wide=true;
  [DataMember] public string LastRead;
  [DataMember] public List<int> Bookmarks=new List<int>();
 }
 [DataContract] public class Settings {
  [DataMember] public List<string> Roots=new List<string>();
  [DataMember] public Dictionary<string,Progress> Books=new Dictionary<string,Progress>();
  [DataMember] public double Width=1220,Height=820;
  [DataMember] public int DefaultMode=1;
  [DataMember] public bool Light;
  [DataMember] public string Language;
  [DataMember] public bool? AutoHide,CoverDefault,WideDefault;
  [DataMember] public int ShelfSort;
  [DataMember] public bool ShelfGrouped;
  [DataMember] public List<ServerProfile> Servers=new List<ServerProfile>();
  public static string StatePath=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LeafReader","library.json");
  public static string LoadWarning;
  public static Settings Load(){
   if(!File.Exists(StatePath))return new Settings();
   try{using(var s=File.OpenRead(StatePath)){var v=(Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(s); if(v.Roots==null)v.Roots=new List<string>();if(v.Books==null)v.Books=new Dictionary<string,Progress>();if(v.Servers==null)v.Servers=new List<ServerProfile>();return v;}}
   catch{LoadWarning=L10n.T("阅读记录无法读取，已载入空书架；原记录会保留为 .corrupt 文件。");try{File.Copy(StatePath,StatePath+".corrupt",true);}catch{} return new Settings();}
  }
  public string Save(){
   try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(StatePath));string temp=StatePath+".tmp";
    using(var s=File.Create(temp))new DataContractJsonSerializer(typeof(Settings)).WriteObject(s,this);
    if(File.Exists(StatePath))File.Replace(temp,StatePath,StatePath+".bak",true);else File.Move(temp,StatePath);return null;
   }catch(Exception e){return L10n.T("无法保存阅读记录：")+e.Message;}
  }
  public Progress Get(Book b){Progress p;if(!Books.TryGetValue(b.Id,out p)){p=new Progress{Mode=DefaultMode,Cover=CoverDefault!=false,Wide=WideDefault!=false};Books[b.Id]=p;}if(!string.IsNullOrEmpty(p.PageId)){int found=b.Pages.FindIndex(page=>page.Identity==p.PageId);if(found>=0)p.Page=found;}if(!string.IsNullOrEmpty(p.PairAnchorId)){int anchor=b.Pages.FindIndex(page=>page.Identity==p.PairAnchorId);if(anchor>=0)p.PairAnchor=anchor;}if(p.Bookmarks==null)p.Bookmarks=new List<int>();if(p.BookmarkIds!=null)p.Bookmarks=b.Pages.Select((page,index)=>new{page,index}).Where(x=>p.BookmarkIds.Contains(x.page.Identity)).Select(x=>x.index).ToList();p.Page=Math.Max(0,Math.Min(b.Pages.Count-1,p.Page));p.Mode=Math.Max(0,Math.Min(3,p.Mode));return p;}
 }
 public static class Paging {
  // 0: single, 1: LTR spread, 2: RTL spread, 3: webtoon.
  public static int Count(Book b,int page,int mode,bool cover,bool wide,int anchor=0){
   if(b==null||b.Pages.Count==0)return 0;page=Math.Max(0,Math.Min(page,b.Pages.Count-1));
   if(mode==0||mode==3||(cover&&page==0)||page==b.Pages.Count-1||(page<anchor&&page+2>anchor))return 1;
   if(wide&&(IsWide(b.Pages[page])||IsWide(b.Pages[page+1])))return 1;return 2;
  }
  public static bool IsWide(Page p){return p.Width>p.Height*1.18;}
  public static int Next(Book b,int p,int mode,bool cover,bool wide,int anchor=0){int next=p+Count(b,p,mode,cover,wide,anchor);return next>=b.Pages.Count?p:next;}
  public static int Previous(Book b,int p,int mode,bool cover,bool wide,int anchor=0){
   if(p<=0)return 0;if(mode==0||mode==3)return p-1;
   // Reconstruct the same groups from the persisted anchor, including single
   // portraits before wide pages. A local p-2 guess loses that pairing history.
   int current=0,previous=0;
   while(current<p){previous=current;current+=Count(b,current,mode,cover,wide,anchor);}
   return previous;
  }
 }
}
