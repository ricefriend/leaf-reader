using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LeafReader {
 public sealed class ReaderView : FrameworkElement {
  public Book Book; public Progress Progress;
  public double Zoom=1; public int Fit; public bool Light;
  public double Offset; public double Extent {get;private set;}
  public Action Changed,ToggleFullscreen;
  double[] tops=new double[0], heights=new double[0]; double imageWidth;
  Point dragStart,panStart,pan;double dragOffset;bool dragging,moved;
  int generation; long clock;
  CancellationTokenSource requests=new CancellationTokenSource();
  void CancelRequests(){requests.Cancel();requests=new CancellationTokenSource();generation++;pending.Clear();}
  class Cached {public BitmapSource Image;public string Error;public long Used;public long Bytes;}
  readonly Dictionary<string,Cached> cache=new Dictionary<string,Cached>();
  readonly HashSet<string> pending=new HashSet<string>();
  readonly SemaphoreSlim workers=new SemaphoreSlim(2);
  public ReaderView(){Focusable=true;ClipToBounds=true;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
   SizeChanged+=(s,e)=>{if(IsToon){int p=Progress.Page;double f=Progress.Fraction;LayoutToon();Offset=tops.Length>p?tops[p]+heights[p]*f:0;ClampOffset();}else ClampPan();InvalidateVisual();Notify();};
   MouseWheel+=Wheel;MouseLeftButtonDown+=Down;MouseMove+=Move;MouseLeftButtonUp+=Up;
   LostMouseCapture+=(s,e)=>{dragging=false;Cursor=Cursors.Arrow;};
  }
  public bool IsToon {get{return Book!=null&&Progress.Mode==3;}}
  public int VisibleCount {get{return Book==null?0:Paging.Count(Book,Progress.Page,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor);}}
  public void Open(Book b,Progress p){CancelRequests();Book=b;Progress=p;Zoom=1;Fit=0;pan=new Point();cache.Clear();pending.Clear();LayoutToon();if(IsToon)Offset=tops[p.Page]+heights[p.Page]*p.Fraction;else Offset=0;ClampOffset();InvalidateVisual();Notify();}
  public void ClearImages(){CancelRequests();cache.Clear();InvalidateVisual();}
  public void CloseBook(){CancelRequests();Book=null;Progress=null;cache.Clear();pending.Clear();tops=new double[0];heights=new double[0];InvalidateVisual();}
  public void RefreshLayout(){if(Book==null)return;Progress.PairAnchor=Progress.Page;pan=new Point();LayoutToon();if(IsToon)Offset=tops[Progress.Page]+heights[Progress.Page]*Progress.Fraction;ClampOffset();InvalidateVisual();Notify();}
  public void SetMode(int mode){if(Book==null)return;Progress.Mode=mode;Progress.PairAnchor=Progress.Page;Zoom=1;pan=new Point();Progress.Fraction=0;LayoutToon();Offset=IsToon?tops[Progress.Page]:0;InvalidateVisual();Notify();}
  public void Jump(int page,bool newAnchor=true){if(Book==null)return;CancelRequests();Progress.Page=Math.Max(0,Math.Min(Book.Pages.Count-1,page));if(newAnchor)Progress.PairAnchor=Progress.Page;Progress.Fraction=0;pan=new Point();if(IsToon){LayoutToon();Offset=tops[Progress.Page];ClampOffset();SyncPage();}InvalidateVisual();Notify();}
  public void Turn(int direction){
   if(Book==null)return;
   if(IsToon){Scroll(direction*Math.Max(100,ActualHeight*.86));return;}
   int p=Progress.Page,n=direction>0?Paging.Next(Book,p,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor):Paging.Previous(Book,p,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor);
   Jump(n,false);
  }
  public void Nudge(int delta){if(Book!=null&&(Progress.Mode==1||Progress.Mode==2))Jump(Progress.Page+delta);}
  public void Scroll(double delta){SetOffset(Offset+delta);}
  public void SetOffset(double value){if(!IsToon)return;int previous=Progress.Page;Offset=value;ClampOffset();SyncPage();if(previous!=Progress.Page)CancelRequests();InvalidateVisual();Notify();}
  void ClampOffset(){Offset=Math.Max(0,Math.Min(Math.Max(0,Extent-ActualHeight),Offset));}
  void SyncPage(){if(!IsToon||tops.Length==0)return;int p=FindPage(Offset+1);Progress.Page=p;Progress.Fraction=Math.Max(0,Math.Min(1,(Offset-tops[p])/heights[p]));}
  int FindPage(double y){int i=Array.BinarySearch(tops,y);return Math.Max(0,Math.Min(tops.Length-1,i>=0?i:~i-1));}
  void Notify(){if(Changed!=null)Changed();}
  public void SetZoom(double z){if(Book==null)return;Zoom=Math.Max(.2,Math.Min(5,z));if(IsToon){int p=Progress.Page;double f=Progress.Fraction;LayoutToon();Offset=tops[p]+heights[p]*f;ClampOffset();SyncPage();}else ClampPan();InvalidateVisual();Notify();}
  void LayoutToon(){
   if(Book==null)return;imageWidth=Math.Max(100,Math.Min(1000,ActualWidth-64))*Zoom;
   tops=new double[Book.Pages.Count];heights=new double[tops.Length];double y=24;
   for(int i=0;i<tops.Length;i++){tops[i]=y;heights[i]=imageWidth*Book.Pages[i].Height/Math.Max(1,Book.Pages[i].Width);y+=heights[i]+10;}Extent=y+24;
  }
  void Wheel(object sender,MouseWheelEventArgs e){if(Book==null)return;e.Handled=true;
   if((Keyboard.Modifiers&ModifierKeys.Control)!=0){SetZoom(Zoom*(e.Delta>0?1.15:1/1.15));return;}
   if(IsToon){Scroll(-e.Delta*1.1);return;}
   wheel+=e.Delta;if(Math.Abs(wheel)>=120){int direction=wheel<0?1:-1;wheel=0;Turn(direction);}
  }
  int wheel;
  void Down(object s,MouseButtonEventArgs e){if(Book==null)return;Focus();if(e.ClickCount==2){if(ToggleFullscreen!=null)ToggleFullscreen();return;}dragging=true;moved=false;dragStart=e.GetPosition(this);panStart=pan;dragOffset=Offset;CaptureMouse();Cursor=Cursors.Hand;}
  void Move(object s,MouseEventArgs e){if(!dragging)return;Point p=e.GetPosition(this);Vector d=p-dragStart;if(d.Length>5)moved=true;if(!moved)return;if(IsToon)SetOffset(dragOffset-d.Y);else{pan=new Point(panStart.X+d.X,panStart.Y+d.Y);ClampPan();InvalidateVisual();}}
  void Up(object s,MouseButtonEventArgs e){if(!dragging)return;dragging=false;ReleaseMouseCapture();Cursor=Cursors.Arrow;if(moved||IsToon)return;double x=e.GetPosition(this).X;if(x<ActualWidth*.28)Turn(Progress.Mode==2?1:-1);else if(x>ActualWidth*.72)Turn(Progress.Mode==2?-1:1);}
  double SpreadScale(out double width,out double height){int n=VisibleCount;var p=Book.Pages[Progress.Page];width=p.Width;height=p.Height;if(n==2){var q=Book.Pages[Progress.Page+1];height=Math.Max(p.Height,q.Height);width=p.Width*height/p.Height+q.Width*height/q.Height;}
   double scale=Fit==2?1:Fit==1?(ActualWidth-48)/width:Math.Min((ActualWidth-48)/width,(ActualHeight-36)/height);return Math.Max(.001,scale*Zoom);
  }
  void ClampPan(){if(Book==null||IsToon)return;double w,h;double scale=SpreadScale(out w,out h);double x=Math.Max(0,(w*scale-ActualWidth)/2+24),y=Math.Max(0,(h*scale-ActualHeight)/2+18);pan=new Point(Math.Max(-x,Math.Min(x,pan.X)),Math.Max(-y,Math.Min(y,pan.Y)));}
  protected override void OnRender(DrawingContext dc){
   dc.DrawRectangle(Light?new SolidColorBrush(Color.FromRgb(235,233,228)):new SolidColorBrush(Color.FromRgb(17,19,24)),null,new Rect(0,0,ActualWidth,ActualHeight));
   if(Book==null||ActualWidth<1||ActualHeight<1)return;
   if(IsToon){
    if(tops.Length!=Book.Pages.Count)LayoutToon();int start=FindPage(Offset);
    for(int i=start;i<tops.Length&&tops[i]<Offset+ActualHeight;i++)DrawPage(dc,i,new Rect((ActualWidth-imageWidth)/2,tops[i]-Offset,imageWidth,heights[i]));
    // Prefetch only the next page. The viewport never creates one UI element per image.
    if(start+1<Book.Pages.Count)Request(start+1,(int)imageWidth);
   }else{
    double w,h,scale=SpreadScale(out w,out h);double left=(ActualWidth-w*scale)/2+pan.X,top=(ActualHeight-h*scale)/2+pan.Y;
    int p=Progress.Page;if(VisibleCount==1)DrawPage(dc,p,new Rect(left,top,w*scale,h*scale));
    else{int first=Progress.Mode==2?p+1:p,second=Progress.Mode==2?p:p+1;double w1=Book.Pages[first].Width*h/Book.Pages[first].Height*scale;DrawPage(dc,first,new Rect(left,top,w1,h*scale));DrawPage(dc,second,new Rect(left+w1,top,w*scale-w1,h*scale));}
    if(p+VisibleCount<Book.Pages.Count)Request(p+VisibleCount,(int)Math.Max(300,ActualWidth/2));
   }
  }
  void DrawPage(DrawingContext dc,int index,Rect r){
   var item=Request(index,(int)Math.Max(64,Math.Min(8192,r.Width*1.25)));
   if(item!=null&&item.Image!=null){dc.DrawImage(item.Image,r);return;}
   dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(34,37,44)),null,r);
   string message=item==null?L10n.T("正在加载…"):(L10n.T("图片无法显示\n")+Book.Pages[index].Name+"\n"+item.Error);
   var t=new FormattedText(message,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),14,Brushes.Gray,VisualTreeHelper.GetDpi(this).PixelsPerDip);t.MaxTextWidth=Math.Max(1,Math.Min(440,r.Width-24));t.MaxTextHeight=120;
   dc.DrawText(t,new Point(r.Left+Math.Max(12,(r.Width-t.Width)/2),Math.Max(12,r.Top+(Math.Min(r.Height,ActualHeight)-t.Height)/2)));
  }
  Cached Request(int index,int pixels){
   int size=Math.Max(256,Math.Min(8192,((pixels+255)/256)*256));string key=Book.Pages[index].Key+"@"+size;Cached hit;
   if(cache.TryGetValue(key,out hit)){hit.Used=++clock;return hit;}
   if(pending.Contains(key))return null;
   if(pending.Count>=8)return null;
   pending.Add(key);int gen=generation;Page page=Book.Pages[index];var cancellation=requests.Token;
   Task.Run(async delegate{try{await workers.WaitAsync(cancellation);}catch(OperationCanceledException){return;}var entry=new Cached();try{if(gen!=generation)return;entry.Image=page.Decode(size,cancellation);entry.Bytes=(long)entry.Image.PixelWidth*entry.Image.PixelHeight*4;}catch(OperationCanceledException){return;}catch(Exception ex){entry.Error=ex.Message;}finally{workers.Release();}
    if(Dispatcher.HasShutdownStarted)return;await Dispatcher.InvokeAsync(new Action(delegate{if(gen!=generation)return;pending.Remove(key);entry.Used=++clock;cache[key]=entry;long total=cache.Values.Sum(c=>c.Bytes);while(cache.Count>16||total>192L*1024*1024){var oldest=cache.OrderBy(kv=>kv.Value.Used).First();total-=oldest.Value.Bytes;cache.Remove(oldest.Key);}InvalidateVisual();}));
   });return null;
  }
 }
}
