using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LeafReader {
 internal static class PhotoCacheTests {
  static readonly List<string> report=new List<string>();static int checks;
  static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);checks++;report.Add("PASS "+label);}
  sealed class TrackingSource:IImageSource {
   readonly Dictionary<string,int> counts=new Dictionary<string,int>();
   internal string Held;internal int Delay;internal readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();
   public string Id {get{return "photo-test";}}public string Label {get{return "test";}}
   internal int Loads(Page p){lock(counts){int n;return counts.TryGetValue(p.Path,out n)?n:0;}}
   public Stream Open(Page p,string variant,int width,CancellationToken token){
    if(variant!="original"){lock(counts){int n;counts.TryGetValue(p.Path,out n);counts[p.Path]=n+1;}
     if(p.Path==Held){Entered.Set();Release.Wait(token);}if(Delay>0&&token.WaitHandle.WaitOne(Delay))token.ThrowIfCancellationRequested();
    }
    token.ThrowIfCancellationRequested();return new FileStream(p.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
   }
  }
  static void Jpeg(string path){
   const int width=6000,height=4000;var data=new byte[width*height*4];new Random(20261010).NextBytes(data);for(int i=3;i<data.Length;i+=4)data[i]=255;
   var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,data,width*4);image.Freeze();var jpeg=new JpegBitmapEncoder{QualityLevel=90};jpeg.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(path))jpeg.Save(stream);
  }
  static Book Photos(string directory,TrackingSource source){var book=new Book{Id=directory,Title="Camera-sized JPEG regression",Pages=new List<Page>()};for(int i=0;i<8;i++){string path=Path.Combine(directory,i+".jpg");if(i>0)File.Copy(Path.Combine(directory,"0.jpg"),path,true);var page=new Page{Path=path,Source=source};page.ReadInfo();book.Pages.Add(page);}return book;}
  static void Paint(ReaderView view){view.UpdateLayout();var shot=new RenderTargetBitmap(16,16,96,96,PixelFormats.Pbgra32);shot.Render(view);}
  static async Task Wait(ReaderView view,Func<bool> condition,string label){for(int i=0;i<600;i++){Paint(view);if(condition())return;await Task.Delay(20);}throw new Exception("Timeout: "+label);}
  static bool Ready(ReaderView view,Book book,int index){return view.ImageCache.Ready(book.Pages[index],view.PixelsForPage(index));}
  public static async Task<int> Run(string directory){
   Directory.CreateDirectory(directory);Settings.StatePath=Path.Combine(directory,"settings.json");MainWindow window=null;
   try{
    string photos=Path.Combine(directory,"photos");Directory.CreateDirectory(photos);await Task.Run(()=>Jpeg(Path.Combine(photos,"0.jpg")));var source=new TrackingSource();var book=Photos(photos,source);
    Check(book.Pages.All(p=>p.Width==6000&&p.Height==4000&&p.EncodedBytes>1024*1024),"real 24MP JPEG fixtures record their encoded file sizes");
    window=new MainWindow(true,new string[0]);window.Left=-20000;window.Top=-20000;window.Width=1220;window.Height=820;window.Show();window.State.AutoHide=false;await window.OpenBook(book);var view=window.Reader;view.SetMode(0);view.Jump(0);
    await Wait(view,()=>Ready(view,book,0)&&Ready(view,book,1)&&Ready(view,book,2)&&Ready(view,book,3),"initial photo prefetch");
    Check(view.LoadPolicy.Expanded&&view.LoadPolicy.AverageBytes>1024*1024&&view.LoadPolicy.AveragePixels==24000000,"large folders automatically expand prefetch using bytes and pixels");var readyPixels=view.ImageCache.Get(book.Pages[0],view.PixelsForPage(0)).Image;Check(readyPixels.IsFrozen&&readyPixels.Format==PixelFormats.Pbgra32&&!(readyPixels is TransformedBitmap),"cached image already contains display-ready pixels rather than a deferred decoder or transform");
    int loaded=source.Loads(book.Pages[1]);var turn=Stopwatch.StartNew();view.Turn(1);Paint(view);turn.Stop();await Task.Delay(100);
    Check(Ready(view,book,1)&&source.Loads(book.Pages[1])==loaded,"next photo reuses the full display-resolution prefetch without a second decode");report.Add("MEASURE warmed turn + clipped 16x16 software paint (not whole-screen frame time): "+turn.Elapsed.TotalMilliseconds.ToString("F2")+" ms");
    view.Turn(-1);Paint(view);Check(Ready(view,book,0)&&source.Loads(book.Pages[0])==1,"previous photo remains immediately available from decoded cache");
    view.SetZoom(1.5);Paint(view);Check(view.ImageCache.Get(book.Pages[0],view.PixelsForPage(0))!=null,"zoom keeps the existing image visible while higher resolution loads");await Wait(view,()=>Ready(view,book,0),"higher resolution zoom");loaded=source.Loads(book.Pages[0]);view.SetZoom(1);Paint(view);await Task.Delay(100);Check(source.Loads(book.Pages[0])==loaded,"zooming back out reuses higher-resolution content");
    // An actual neighboring I/O operation is held in flight while the UI turns.
    view.ClearImages();source.Held=book.Pages[1].Path;source.Entered.Reset();source.Release.Reset();view.Jump(0);await Wait(view,()=>source.Entered.IsSet&&Ready(view,book,0),"held neighboring preload");loaded=source.Loads(book.Pages[1]);view.Turn(1);Paint(view);source.Release.Set();await Wait(view,()=>Ready(view,book,1),"in-flight neighbor after turn");Check(source.Loads(book.Pages[1])==loaded,"turning to an in-flight neighbor preserves its request and does not reopen the file");source.Held=null;
    // Drop far-away work, then reopen another source with exactly the same IDs.
    view.ClearImages();source.Held=book.Pages[2].Path;source.Entered.Reset();source.Release.Reset();view.Jump(1);await Wait(view,()=>source.Entered.IsSet,"held obsolete preload");view.Jump(7);Paint(view);await Wait(view,()=>view.ImageCache.CancelledLoads>0&&Ready(view,book,7),"rapid distant jump");Check(Ready(view,book,7)&&view.Progress.Page==7,"rapid distant jump cancels stale I/O and shows the requested photo");source.Release.Set();source.Held=null;
    string red=Path.Combine(directory,"red.png");var pixels=new byte[]{0,0,255,255};var redImage=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,pixels,4);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(redImage));using(var stream=File.Create(red))png.Save(stream);
    var smallSource=new TrackingSource{Delay=0};var smallPage=new Page{Path=red,Source=smallSource};smallPage.ReadInfo();var small=new Book{Id="small",Title="small",Pages=new List<Page>{smallPage}};view.Open(small,new Progress{Mode=0});Paint(view);Check(view.ImageCache.Get(smallPage,view.PixelsForPage(0))==null,"opening another folder cannot show images from the old cache");await Wait(view,()=>Ready(view,small,0),"small local image");Check(view.LoadPolicy.AverageBytes<1024*1024&&view.LoadPolicy.AveragePixels<8000000,"small-image folder profile remains lightweight");
    // Cheap images on a slow source should adapt from actual load time too.
    smallSource.Delay=160;view.Open(small,new Progress{Mode=0});await Wait(view,()=>Ready(view,small,0),"slow small image");Check(view.LoadPolicy.Expanded&&view.LoadPolicy.LoadMilliseconds>=150,"small files on a slow source expand prefetch based on measured load time");smallSource.Delay=0;
    // Include version in identity: same filename, new version, different image.
    var blue=new PngBitmapEncoder();blue.Frames.Add(BitmapFrame.Create(BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{255,0,0,255},4)));using(var stream=File.Create(red))blue.Save(stream);smallPage.Version="updated";view.InvalidateVisual();Paint(view);Check(view.ImageCache.Get(smallPage,view.PixelsForPage(0))==null,"a changed image version does not reuse an old cached image");await Wait(view,()=>Ready(view,small,0),"updated image version");var updated=new byte[4];view.ImageCache.Get(smallPage,view.PixelsForPage(0)).Image.CopyPixels(updated,4,0);Check(updated[0]==255&&updated[2]==0,"updated same-name file displays the new pixels");
    view.Open(book,new Progress{Mode=2,Cover=false,Wide=false});await Wait(view,()=>Ready(view,book,0)&&Ready(view,book,1)&&Ready(view,book,2)&&Ready(view,book,3),"RTL spread prefetch");loaded=source.Loads(book.Pages[2]);view.Turn(1);Paint(view);Check(view.Progress.Page==2&&Ready(view,book,2)&&Ready(view,book,3)&&source.Loads(book.Pages[2])==loaded,"RTL spreads prefetch both next pages at the actual display size");
    view.SetMode(0);view.Jump(0);view.SetZoom(5);await Wait(view,()=>Ready(view,book,0)&&Ready(view,book,1),"memory-bounded high-resolution prefetch");
    for(int i=1;i<=4;i++){view.Jump(i);await Wait(view,()=>Ready(view,book,i),"high-resolution page "+i);Check(view.ImageCache.Bytes<=ReaderImageCache.Budget&&view.ImageCache.Count<=16&&view.ImageCache.PendingCount<=8,"decoded cache and request window stay bounded at high zoom, page "+i);}
    Check(view.ImageCache.PeakWorkers<=2,"image I/O and decoding use at most two concurrent workers");report.Add("MEASURE high-zoom decoded cache: "+(view.ImageCache.Bytes/1024/1024)+" MiB; sampled process working set: "+(Process.GetCurrentProcess().WorkingSet64/1024/1024)+" MiB");view.SetZoom(1);await Wait(view,()=>view.ImageCache.PendingCount==0,"zoom-out prefetch settles");int settled=view.ImageCache.CompletedLoads;for(int i=0;i<10;i++){view.InvalidateVisual();Paint(view);await Task.Delay(40);}Check(view.ImageCache.CompletedLoads==settled&&view.ImageCache.Bytes<=ReaderImageCache.Budget,"zoom-out accounts for retained high-resolution images without prefetch eviction/reload loops");
    string rotated=Path.Combine(directory,"rotated-camera.jpg");var rotatedJpeg=new JpegBitmapEncoder();var exif=new BitmapMetadata("jpg");exif.SetQuery("/app1/ifd/{ushort=274}",(ushort)6);rotatedJpeg.Frames.Add(BitmapFrame.Create(new TransformedBitmap(redImage,new ScaleTransform(600,900)),null,exif,null));using(var stream=File.Create(rotated))rotatedJpeg.Save(stream);var rotatedPage=new Page{Path=rotated};rotatedPage.ReadInfo();var rotatedBook=new Book{Id=rotated,Pages=new List<Page>{rotatedPage}};view.Open(rotatedBook,new Progress{Mode=0});await Wait(view,()=>Ready(view,rotatedBook,0),"EXIF-oriented image cache");var rotatedPixels=view.ImageCache.Get(rotatedPage,view.PixelsForPage(0)).Image;Check(rotatedPage.Width==900&&rotatedPage.Height==600&&Math.Abs((double)rotatedPixels.PixelWidth/rotatedPixels.PixelHeight-1.5)<.01&&rotatedPixels.Format==PixelFormats.Pbgra32&&!(rotatedPixels is TransformedBitmap),"EXIF rotation is materialized before the image enters the display cache");
    string stripPath=Path.Combine(directory,"strip.png");var stripEncoder=new PngBitmapEncoder();stripEncoder.Frames.Add(BitmapFrame.Create(new TransformedBitmap(redImage,new ScaleTransform(2000,1))));using(var stream=File.Create(stripPath))stripEncoder.Save(stream);var stripSource=new TrackingSource{Delay=30};var strips=new Book{Id="strips",Pages=new List<Page>()};for(int i=0;i<40;i++){string path=Path.Combine(directory,"strip-"+i+".png");File.Copy(stripPath,path,true);var page=new Page{Path=path,Source=stripSource};page.ReadInfo();strips.Pages.Add(page);}view.Open(strips,new Progress{Mode=3});Paint(view);Check(view.ImageCache.PendingCount<=8,"webtoon with many visible short strips does not enqueue unbounded requests");await Wait(view,()=>Enumerable.Range(0,40).All(i=>Ready(view,strips,i)),"short webtoon strips");Check(view.ImageCache.Bytes<=ReaderImageCache.Budget&&view.ImageCache.PendingCount<=8,"visible short strips stay available without evicting and reloading each other");
    string bmpPath=Path.Combine(directory,"large-file-small-pixels.bmp");var bmp=new BmpBitmapEncoder();bmp.Frames.Add(BitmapFrame.Create(new TransformedBitmap(redImage,new ScaleTransform(1024,1024))));using(var stream=File.Create(bmpPath))bmp.Save(stream);var bmpPage=new Page{Path=bmpPath};bmpPage.ReadInfo();view.Open(new Book{Id=bmpPath,Pages=new List<Page>{bmpPage}},new Progress{Mode=0});Check(view.LoadPolicy.Expanded&&view.LoadPolicy.AverageBytes>=1024*1024&&view.LoadPolicy.AveragePixels<8000000&&view.LoadPolicy.LoadMilliseconds==0,"file size alone enables expanded prefetch before the first decode");
    string compact=Path.Combine(directory,"small-file-large-pixels.jpg");var compactJpeg=new JpegBitmapEncoder{QualityLevel=75};compactJpeg.Frames.Add(BitmapFrame.Create(new TransformedBitmap(redImage,new ScaleTransform(6000,4000))));using(var stream=File.Create(compact))compactJpeg.Save(stream);var compactPage=new Page{Path=compact};compactPage.ReadInfo();view.Open(new Book{Id=compact,Pages=new List<Page>{compactPage}},new Progress{Mode=0});Check(view.LoadPolicy.Expanded&&view.LoadPolicy.AverageBytes<1024*1024&&view.LoadPolicy.AveragePixels==24000000&&view.LoadPolicy.LoadMilliseconds==0,"high pixel count enables expanded prefetch even for a tiny compressed JPEG");
    view.CloseBook();await Task.Delay(300);Check(view.ImageCache.Bytes==0&&view.ImageCache.PendingCount==0,"closing reader releases decoded images and pending requests");
    window.Close();window=null;report.Add("ALL "+checks+" PHOTO CACHE TESTS PASSED");File.WriteAllLines(Path.Combine(directory,"photo-cache-results.txt"),report);return 0;
   }catch(Exception ex){report.Add(ex.ToString());File.WriteAllLines(Path.Combine(directory,"photo-cache-results.txt"),report);if(window!=null)window.Close();return 1;}
  }
 }
}
