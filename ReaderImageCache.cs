using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LeafReader {
 // File size estimates I/O cost; pixels and measured loads cover compressed
 // camera JPEGs, slow disks and remote sources. Normal books still keep a cache.
 internal sealed class ReaderLoadPolicy {
  internal double AverageBytes,AveragePixels,LoadMilliseconds;
  internal bool Expanded {get{return AverageBytes>=1024*1024||AveragePixels>=8000000||LoadMilliseconds>=45;}}
  internal int Ahead {get{return Expanded?3:1;}}
  internal void Open(Book book){AverageBytes=book.Pages.Where(p=>p.EncodedBytes>0).Select(p=>(double)p.EncodedBytes).DefaultIfEmpty(0).Average();AveragePixels=book.Pages.Select(p=>(double)p.Width*p.Height).DefaultIfEmpty(0).Average();LoadMilliseconds=0;}
  internal void Observe(double ms){LoadMilliseconds=LoadMilliseconds==0?ms:LoadMilliseconds*.75+ms*.25;}
 }
 internal sealed class ReaderImageCache {
  internal const long Budget=192L*1024*1024;
  internal sealed class Wish {
   internal Page Page;internal string PageKey;internal int Pixels;internal bool Visible;
   internal Wish(Page page,int pixels,bool visible){Page=page;PageKey=page.Key;Pixels=Resolution(page,pixels);Visible=visible;}
   internal string Key {get{return PageKey+"@"+Pixels;}}
   internal long Bytes {get{return Math.Max(1,(long)Math.Ceiling((double)Pixels*Pixels*Math.Max(1,Page.Height)/Math.Max(1,Page.Width)))*4;}}
  }
  internal sealed class Cached {internal BitmapSource Image;internal string Error,PageKey;internal int Pixels;internal long Used,Bytes;}
  sealed class Job {internal Wish Wish;internal int Generation;internal bool Active;internal CancellationTokenSource Cancellation=new CancellationTokenSource();}
  readonly Dispatcher dispatcher;readonly Action changed;readonly ReaderLoadPolicy policy;
  readonly Dictionary<string,Cached> cache=new Dictionary<string,Cached>();
  readonly Dictionary<string,Job> pending=new Dictionary<string,Job>();
  List<Wish> plan=new List<Wish>();int generation,active;long clock;
  internal int CompletedLoads,CancelledLoads,PeakWorkers;
  internal long Bytes {get{return cache.Values.Sum(c=>c.Bytes);}}
  internal int Count {get{return cache.Count;}}
  internal int PendingCount {get{return pending.Count;}}
  internal ReaderImageCache(Dispatcher dispatcher,Action changed,ReaderLoadPolicy policy){this.dispatcher=dispatcher;this.changed=changed;this.policy=policy;}
  internal static int Resolution(Page page,int pixels){
   int bucket=Math.Max(256,Math.Min(8192,((Math.Max(1,Math.Min(8192,pixels))+255)/256)*256));
   double cap=Math.Min(Math.Max(1,page.Width),Math.Max(1,page.Width)*Math.Min(1,Math.Sqrt(16000000.0/(Math.Max(1,(double)page.Width)*Math.Max(1,page.Height)))));
   return Math.Max(1,Math.Min(bucket,(int)cap));
  }
  internal void Reset(){generation++;foreach(var job in pending.Values){job.Cancellation.Cancel();if(!job.Active)job.Cancellation.Dispose();}pending.Clear();cache.Clear();plan.Clear();}
  Cached Adequate(Wish wish){return cache.Values.Where(c=>c.PageKey==wish.PageKey&&c.Pixels>=wish.Pixels).OrderBy(c=>c.Pixels).FirstOrDefault();}
  internal Cached Get(Page page,int pixels){
   var wish=new Wish(page,pixels,true);var hit=Adequate(wish);
   if(hit==null||hit.Image==null){var fallback=cache.Values.Where(c=>c.PageKey==page.Key&&c.Image!=null).OrderByDescending(c=>c.Pixels).FirstOrDefault();if(fallback!=null)hit=fallback;}
   if(hit!=null)hit.Used=++clock;return hit;
  }
  internal bool Ready(Page page,int pixels){var entry=Adequate(new Wish(page,pixels,true));return entry!=null&&entry.Image!=null;}
  static BitmapSource Materialize(BitmapSource image,CancellationToken cancellation){
   // Store finished pixels, not a decoder or lazy EXIF transform. Otherwise
   // the first DrawImage can still perform pixel conversion on the UI path.
   BitmapSource source=image.Format==PixelFormats.Pbgra32?image:new FormatConvertedBitmap(image,PixelFormats.Pbgra32,null,0);
   int stride=checked(source.PixelWidth*4);var pixels=new byte[checked(stride*source.PixelHeight)];source.CopyPixels(pixels,stride,0);cancellation.ThrowIfCancellationRequested();
   var ready=BitmapSource.Create(source.PixelWidth,source.PixelHeight,source.DpiX,source.DpiY,PixelFormats.Pbgra32,null,pixels,stride);ready.Freeze();return ready;
  }
  internal void Plan(IEnumerable<Wish> wishes){
   // Protect foreground work first. Bound the selected prefetch window by its
   // decoded size, so zooming cannot repeatedly load and evict the same neighbors.
   var selected=new List<Wish>();var keys=new HashSet<string>();long bytes=0;
   foreach(var wish in wishes){if(!keys.Add(wish.Key))continue;var ready=Adequate(wish);long estimate=ready!=null&&ready.Image!=null?ready.Bytes:wish.Bytes;if(!wish.Visible&&(bytes+estimate>Budget||selected.Count>=8))continue;selected.Add(wish);bytes+=estimate;}
   plan=selected;var wantedPages=new HashSet<string>(plan.Select(w=>w.PageKey));var wantedKeys=new HashSet<string>(plan.Select(w=>w.Key));
   foreach(var pair in pending.ToArray()){
    var job=pair.Value;
    // Keep an in-flight preview of a page still in the window. It can be shown
    // while a larger version loads, and survives an ordinary adjacent turn.
    if(wantedKeys.Contains(pair.Key)||job.Active&&wantedPages.Contains(job.Wish.PageKey))continue;
    job.Cancellation.Cancel();pending.Remove(pair.Key);if(!job.Active)job.Cancellation.Dispose();
   }
   QueueMissing();
   Trim();Pump();
  }
  void QueueMissing(){foreach(var wish in plan){if(pending.Count>=8)break;if(Adequate(wish)==null&&!pending.ContainsKey(wish.Key))pending.Add(wish.Key,new Job{Wish=wish,Generation=generation});}}
  void Pump(){
   while(active<2){Job job=null;foreach(var wish in plan){Job candidate;if(pending.TryGetValue(wish.Key,out candidate)&&!candidate.Active){if(Adequate(wish)!=null){pending.Remove(wish.Key);candidate.Cancellation.Dispose();continue;}job=candidate;break;}}
    if(job==null)return;job.Active=true;active++;PeakWorkers=Math.Max(PeakWorkers,active);Start(job);
   }
  }
  async void Start(Job job){
   var entry=new Cached{PageKey=job.Wish.PageKey,Pixels=job.Wish.Pixels};var watch=new Stopwatch();bool cancelled=false;
   try{await Task.Run(delegate{watch.Start();try{job.Cancellation.Token.ThrowIfCancellationRequested();entry.Image=Materialize(job.Wish.Page.Decode(job.Wish.Pixels,job.Cancellation.Token),job.Cancellation.Token);job.Cancellation.Token.ThrowIfCancellationRequested();entry.Bytes=(long)entry.Image.PixelWidth*entry.Image.PixelHeight*4;}finally{watch.Stop();}});}
   catch(OperationCanceledException){cancelled=true;}catch(Exception ex){if(job.Cancellation.IsCancellationRequested)cancelled=true;else entry.Error=ex.Message;}
   // Await resumes on WPF's dispatcher. Reset may have replaced a same-key job.
   active--;Job current;if(pending.TryGetValue(job.Wish.Key,out current)&&ReferenceEquals(current,job))pending.Remove(job.Wish.Key);
   cancelled|=job.Cancellation.IsCancellationRequested;job.Cancellation.Dispose();
   if(cancelled){CancelledLoads++;}else if(job.Generation==generation&&plan.Any(w=>w.PageKey==entry.PageKey)){
    CompletedLoads++;if(entry.Image!=null){policy.Observe(watch.Elapsed.TotalMilliseconds);foreach(var old in cache.Where(pair=>pair.Value.PageKey==entry.PageKey&&pair.Value.Pixels<=entry.Pixels).Select(pair=>pair.Key).ToArray())cache.Remove(old);}
    entry.Used=++clock;cache[job.Wish.Key]=entry;Trim();if(!dispatcher.HasShutdownStarted)changed();
   }
   if(!dispatcher.HasShutdownStarted){QueueMissing();Pump();}
  }
  void Trim(){
   var foreground=new HashSet<string>();foreach(var wish in plan.Where(w=>w.Visible)){var hit=Get(wish.Page,wish.Pixels);if(hit!=null)foreach(var pair in cache)if(ReferenceEquals(pair.Value,hit))foreground.Add(pair.Key);}
   var wanted=new HashSet<string>(plan.Select(w=>w.PageKey));long total=Bytes;
   // Very short webtoon strips can put more than 16 tiny images on screen.
   // Retain those visible entries; the byte budget remains the hard limit.
   int limit=Math.Max(16,foreground.Count);
   while(cache.Count>limit||total>Budget){var old=cache.OrderBy(pair=>foreground.Contains(pair.Key)?2:wanted.Contains(pair.Value.PageKey)?1:0).ThenBy(pair=>pair.Value.Used).First();total-=old.Value.Bytes;cache.Remove(old.Key);}
  }
 }
}
