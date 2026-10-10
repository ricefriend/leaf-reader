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
  readonly ReaderLoadPolicy loadPolicy=new ReaderLoadPolicy();
  readonly ReaderImageCache images;int travel=1;
  internal ReaderImageCache ImageCache {get{return images;}}
  internal ReaderLoadPolicy LoadPolicy {get{return loadPolicy;}}
  public ReaderView(){images=new ReaderImageCache(Dispatcher,()=>InvalidateVisual(),loadPolicy);Focusable=true;ClipToBounds=true;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
   SizeChanged+=(s,e)=>{if(IsToon){int p=Progress.Page;double f=Progress.Fraction;LayoutToon();Offset=tops.Length>p?tops[p]+heights[p]*f:0;ClampOffset();}else ClampPan();InvalidateVisual();Notify();};
   MouseWheel+=Wheel;MouseLeftButtonDown+=Down;MouseMove+=Move;MouseLeftButtonUp+=Up;
   LostMouseCapture+=(s,e)=>{dragging=false;Cursor=Cursors.Arrow;};
  }
  public bool IsToon {get{return Book!=null&&Progress.Mode==3;}}
  public int VisibleCount {get{return Book==null?0:Paging.Count(Book,Progress.Page,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor);}}
  public void Open(Book b,Progress p){images.Reset();loadPolicy.Open(b);travel=1;Book=b;Progress=p;Zoom=1;Fit=0;pan=new Point();LayoutToon();if(IsToon)Offset=tops[p.Page]+heights[p.Page]*p.Fraction;else Offset=0;ClampOffset();InvalidateVisual();Notify();}
  public void ClearImages(){images.Reset();InvalidateVisual();}
  public void CloseBook(){images.Reset();Book=null;Progress=null;tops=new double[0];heights=new double[0];InvalidateVisual();}
  public void RefreshLayout(){if(Book==null)return;Progress.PairAnchor=Progress.Page;pan=new Point();LayoutToon();if(IsToon)Offset=tops[Progress.Page]+heights[Progress.Page]*Progress.Fraction;ClampOffset();InvalidateVisual();Notify();}
  public void SetMode(int mode){if(Book==null)return;Progress.Mode=mode;Progress.PairAnchor=Progress.Page;Zoom=1;pan=new Point();Progress.Fraction=0;LayoutToon();Offset=IsToon?tops[Progress.Page]:0;InvalidateVisual();Notify();}
  public void Jump(int page,bool newAnchor=true){if(Book==null)return;if(page!=Progress.Page)travel=page>Progress.Page?1:-1;Progress.Page=Math.Max(0,Math.Min(Book.Pages.Count-1,page));if(newAnchor)Progress.PairAnchor=Progress.Page;Progress.Fraction=0;pan=new Point();if(IsToon){LayoutToon();Offset=tops[Progress.Page];ClampOffset();SyncPage();}InvalidateVisual();Notify();}
  public void Turn(int direction){
   if(Book==null)return;
   if(IsToon){Scroll(direction*Math.Max(100,ActualHeight*.86));return;}
   int p=Progress.Page,n=direction>0?Paging.Next(Book,p,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor):Paging.Previous(Book,p,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor);
   Jump(n,false);
  }
  public void Nudge(int delta){if(Book!=null&&(Progress.Mode==1||Progress.Mode==2))Jump(Progress.Page+delta);}
  public void Scroll(double delta){SetOffset(Offset+delta);}
  public void SetOffset(double value){if(!IsToon)return;int previous=Progress.Page;Offset=value;ClampOffset();SyncPage();if(previous!=Progress.Page)travel=Progress.Page>previous?1:-1;InvalidateVisual();Notify();}
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
  double SpreadScale(out double width,out double height){return SpreadScale(Progress.Page,out width,out height);}
  double SpreadScale(int index,out double width,out double height){int n=Paging.Count(Book,index,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor);var p=Book.Pages[index];width=p.Width;height=p.Height;if(n==2){var q=Book.Pages[index+1];height=Math.Max(p.Height,q.Height);width=p.Width*height/p.Height+q.Width*height/q.Height;}
   double scale=Fit==2?1:Fit==1?(ActualWidth-48)/width:Math.Min((ActualWidth-48)/width,(ActualHeight-36)/height);return Math.Max(.001,scale*Zoom);
  }
  void ClampPan(){if(Book==null||IsToon)return;double w,h;double scale=SpreadScale(out w,out h);double x=Math.Max(0,(w*scale-ActualWidth)/2+24),y=Math.Max(0,(h*scale-ActualHeight)/2+18);pan=new Point(Math.Max(-x,Math.Min(x,pan.X)),Math.Max(-y,Math.Min(y,pan.Y)));}
  int DisplayPixels(double width){return (int)Math.Max(64,Math.Min(8192,Math.Ceiling(width*1.25*VisualTreeHelper.GetDpi(this).DpiScaleX-.000001)));}
  void AddSpread(List<ReaderImageCache.Wish> wishes,int page,bool visible){
   double w,h,scale=SpreadScale(page,out w,out h);int count=Paging.Count(Book,page,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor);
   for(int i=0;i<count;i++){var item=Book.Pages[page+i];wishes.Add(new ReaderImageCache.Wish(item,DisplayPixels(item.Width*h/item.Height*scale),visible));}
  }
  int Neighbor(int page,int direction){return direction>0?Paging.Next(Book,page,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor):Paging.Previous(Book,page,Progress.Mode,Progress.Cover,Progress.Wide,Progress.PairAnchor);}
  internal int PixelsForPage(int page){if(IsToon)return ReaderImageCache.Resolution(Book.Pages[page],DisplayPixels(imageWidth));double w,h,scale=SpreadScale(page,out w,out h);return ReaderImageCache.Resolution(Book.Pages[page],DisplayPixels(Book.Pages[page].Width*h/Book.Pages[page].Height*scale));}
  protected override void OnRender(DrawingContext dc){
   dc.DrawRectangle(Light?new SolidColorBrush(Color.FromRgb(235,233,228)):new SolidColorBrush(Color.FromRgb(17,19,24)),null,new Rect(0,0,ActualWidth,ActualHeight));
   if(Book==null||ActualWidth<1||ActualHeight<1)return;
   var wishes=new List<ReaderImageCache.Wish>();
   if(IsToon){
    if(tops.Length!=Book.Pages.Count)LayoutToon();int start=FindPage(Offset),end=start;
    while(end<tops.Length&&tops[end]<Offset+ActualHeight){wishes.Add(new ReaderImageCache.Wish(Book.Pages[end],DisplayPixels(imageWidth),true));end++;}
    for(int step=1;step<=loadPolicy.Ahead;step++){int index=travel>0?end+step-1:start-step;if(index>=0&&index<Book.Pages.Count)wishes.Add(new ReaderImageCache.Wish(Book.Pages[index],DisplayPixels(imageWidth),false));}
    int back=travel>0?start-1:end;if(back>=0&&back<Book.Pages.Count)wishes.Add(new ReaderImageCache.Wish(Book.Pages[back],DisplayPixels(imageWidth),false));
    images.Plan(wishes);
    for(int i=start;i<end;i++)DrawPage(dc,i,new Rect((ActualWidth-imageWidth)/2,tops[i]-Offset,imageWidth,heights[i]));
   }else{
    int p=Progress.Page;AddSpread(wishes,p,true);int next=p;
    for(int step=0;step<loadPolicy.Ahead;step++){int candidate=Neighbor(next,travel);if(candidate==next)break;next=candidate;AddSpread(wishes,next,false);}
    int previous=Neighbor(p,-travel);if(previous!=p)AddSpread(wishes,previous,false);
    images.Plan(wishes);
    double w,h,scale=SpreadScale(out w,out h);double left=(ActualWidth-w*scale)/2+pan.X,top=(ActualHeight-h*scale)/2+pan.Y;
    if(VisibleCount==1)DrawPage(dc,p,new Rect(left,top,w*scale,h*scale));
    else{int first=Progress.Mode==2?p+1:p,second=Progress.Mode==2?p:p+1;double w1=Book.Pages[first].Width*h/Book.Pages[first].Height*scale;DrawPage(dc,first,new Rect(left,top,w1,h*scale));DrawPage(dc,second,new Rect(left+w1,top,w*scale-w1,h*scale));}
   }
  }
  void DrawPage(DrawingContext dc,int index,Rect r){
   var item=images.Get(Book.Pages[index],DisplayPixels(r.Width));
   if(item!=null&&item.Image!=null){dc.DrawImage(item.Image,r);return;}
   dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(34,37,44)),null,r);
   string message=item==null?L10n.T("正在加载…"):(L10n.T("图片无法显示\n")+Book.Pages[index].Name+"\n"+item.Error);
   var t=new FormattedText(message,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),14,Brushes.Gray,VisualTreeHelper.GetDpi(this).PixelsPerDip);t.MaxTextWidth=Math.Max(1,Math.Min(440,r.Width-24));t.MaxTextHeight=120;
   dc.DrawText(t,new Point(r.Left+Math.Max(12,(r.Width-t.Width)/2),Math.Max(12,r.Top+(Math.Min(r.Height,ActualHeight)-t.Height)/2)));
  }
 }
}
