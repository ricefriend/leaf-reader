using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LeafReader {
 // The canvas contains only cards around the viewport; the full library has
 // lightweight rectangles, so scrolling never accumulates thousands of images.
 public sealed class ShelfGrid : Grid {
  readonly ScrollViewer viewer;readonly Canvas canvas=new Canvas{VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left};
  List<Book> books=new List<Book>();Rect[] slots=new Rect[0];
  readonly Dictionary<int,FrameworkElement> visible=new Dictionary<int,FrameworkElement>();
  class Cover {public BitmapSource Image;public bool Failed;public long Used;}
  readonly Dictionary<string,Cover> cache=new Dictionary<string,Cover>();
  bool loading,refreshing;int generation;int gridButtons;long clock;double layoutWidth;
  const double CardWidth=164,CardHeight=284,CoverHeight=220,GapX=14,GapY=18;
  public Action<Book> BookOpened;public Action<Book> BookGridOpened;public Action<Book> BookDeleteRequested;public Action<Book> BookRevealRequested;public Func<Book,bool> CanDeleteBook;public Func<Book,string> Caption;
  public ScrollViewer Scroller {get{return viewer;}}
  internal int RealizedCount {get{return visible.Count;}}
  internal int BookCount {get{return books.Count;}}
  internal int VisibleGridButtons {get{return gridButtons;}}
  internal int[] RealizedIndices {get{return visible.Keys.OrderBy(i=>i).ToArray();}}
  internal bool VisibleCoversReady {get{return visible.Keys.All(i=>cache.ContainsKey(CoverKey(books[i])));}}
  internal Rect[] CardBounds {get{return slots;}}
  internal ContextMenu CardContextMenu(int index){FrameworkElement host;if(!visible.TryGetValue(index,out host))return null;var grid=host as Grid;return grid!=null&&grid.Children.Count>0?((FrameworkElement)grid.Children[0]).ContextMenu:null;}
  public ShelfGrid(){
   viewer=new ScrollViewer{Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Padding=new Thickness(0,0,8,10),VerticalContentAlignment=VerticalAlignment.Top,HorizontalContentAlignment=HorizontalAlignment.Left};
   Children.Add(viewer);viewer.ScrollChanged+=(s,e)=>{if(Math.Abs(viewer.ViewportWidth-layoutWidth)>1)LayoutCards(true);else RefreshVisible();};
   SizeChanged+=(s,e)=>LayoutCards(true);IsVisibleChanged+=(s,e)=>{if(IsVisible)RefreshVisible();};
  }
  static string CoverKey(Book book){return book.Id+"|"+(book.Pages.Count>0?book.Pages[0].Key:"");}
  public void SetBooks(List<Book> items,bool reset){
   bool same=books.Select(b=>b.Id).SequenceEqual(items.Select(b=>b.Id));
   generation++;books=items;visible.Clear();canvas.Children.Clear();
   if(reset||!same)viewer.ScrollToVerticalOffset(0);
   LayoutCards(same&&!reset);
  }
  public void ClearCoverCache(){generation++;cache.Clear();}
  int Anchor(){for(int i=0;i<slots.Length;i++)if(slots[i].Bottom>viewer.VerticalOffset&&slots[i].Top<viewer.VerticalOffset+Math.Max(1,viewer.ViewportHeight))return i;return -1;}
  void LayoutCards(bool preserve){
   if(refreshing)return;int anchor=preserve?Anchor():-1;double delta=anchor>=0?viewer.VerticalOffset-slots[anchor].Top:0;
   double width=Math.Max(180,viewer.ViewportWidth>0?viewer.ViewportWidth:ActualWidth);layoutWidth=viewer.ViewportWidth;
   int columns=Math.Max(1,(int)Math.Floor((width+GapX)/(CardWidth+GapX)));slots=new Rect[books.Count];
   for(int i=0;i<books.Count;i++)slots[i]=new Rect((i%columns)*(CardWidth+GapX),(i/columns)*(CardHeight+GapY),CardWidth,CardHeight);
   canvas.Height=books.Count==0?0:Math.Ceiling((double)books.Count/columns)*(CardHeight+GapY);canvas.Width=width;
   visible.Clear();canvas.Children.Clear();gridButtons=0;
   if(anchor>=0&&anchor<slots.Length)viewer.ScrollToVerticalOffset(Math.Max(0,slots[anchor].Top+delta));
   RefreshVisible();
  }
  void RefreshVisible(){
   if(refreshing||!IsVisible)return;refreshing=true;
   try{
    double top=Math.Max(0,viewer.VerticalOffset-400),bottom=viewer.VerticalOffset+Math.Max(600,viewer.ViewportHeight)+400;
    var needed=new HashSet<int>();for(int i=0;i<slots.Length;i++)if(slots[i].Bottom>=top&&slots[i].Top<=bottom)needed.Add(i);
    foreach(int i in visible.Keys.Where(i=>!needed.Contains(i)).ToArray()){canvas.Children.Remove(visible[i]);visible.Remove(i);}
    foreach(int i in needed.OrderBy(i=>i)){
     Cover cover;cache.TryGetValue(CoverKey(books[i]),out cover);if(cover!=null)cover.Used=++clock;
     if(visible.ContainsKey(i))continue;var button=MakeCard(i,cover);visible[i]=button;canvas.Children.Add(button);Canvas.SetLeft(button,slots[i].X);Canvas.SetTop(button,slots[i].Y);
    }
    var pinned=new HashSet<string>(needed.Select(i=>CoverKey(books[i])));
    while(cache.Count>Math.Max(72,pinned.Count)){var oldest=cache.Where(kv=>!pinned.Contains(kv.Key)).OrderBy(kv=>kv.Value.Used).FirstOrDefault();if(oldest.Key==null)break;cache.Remove(oldest.Key);}
   }finally{refreshing=false;}
   LoadNextCover();
  }
  static Brush Resource(FrameworkElement element,string key){return (Brush)element.FindResource(key);}
  TextBlock Label(string text,double size,string color){var label=new TextBlock{Text=text,FontSize=size,TextTrimming=TextTrimming.CharacterEllipsis};label.SetResourceReference(TextBlock.ForegroundProperty,color);return label;}
  Border Badge(string text,string foreground,string background){
   var label=Label(text,9.5,foreground);label.Margin=new Thickness(0);
   var border=new Border{Background=Resource(this,background),CornerRadius=new CornerRadius(5),Padding=new Thickness(6,2,6,2),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(8),Child=label};
   return border;
  }
  Border CoverShell(){
   var border=new Border{CornerRadius=new CornerRadius(10),BorderThickness=new Thickness(1),Background=Resource(this,"Card")};
   border.SetResourceReference(Border.BorderBrushProperty,"Line");return border;
  }
  static bool HasGridButton(Book book){return book.GroupId==null&&book.RemoteId==null&&book.Pages.Count>1&&book.Pages[0].Entry==null;}
  internal static bool ShowsGridButton(Book book){return HasGridButton(book);}
  FrameworkElement MakeCard(int index,Cover cover){
   var book=books[index];var box=slots[index];bool folder=book.GroupId!=null;
   var content=new Grid{Width=box.Width,Height=box.Height};
   var coverHost=new Grid{Height=CoverHeight,VerticalAlignment=VerticalAlignment.Top};
   var shell=CoverShell();
   if(cover!=null&&cover.Image!=null){
    var rectangle=new Rectangle{RadiusX=10,RadiusY=10,Stretch=Stretch.Fill,Fill=new ImageBrush(cover.Image){Stretch=Stretch.UniformToFill}};
    shell.Child=rectangle;
   }else{
    var placeholder=Label(cover!=null&&cover.Failed?L10n.T("封面无法读取"):L10n.T("正在加载…"),11,"Muted");
    placeholder.HorizontalAlignment=HorizontalAlignment.Center;placeholder.VerticalAlignment=VerticalAlignment.Center;shell.Child=placeholder;
   }
   coverHost.Children.Add(shell);
   if(folder){
    var count=book.GroupCount.ToString()+L10n.T(" 本");
    var pillLabel=Label(count,10,"Fg");pillLabel.FontWeight=FontWeights.SemiBold;
    var pill=new Border{Background=Resource(this,"Bg"),Opacity=.88,CornerRadius=new CornerRadius(6),Padding=new Thickness(8,3,8,3),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(8),Child=pillLabel};
    coverHost.Children.Add(Badge(L10n.T("目录"),"Accent","AccentSoft"));
    coverHost.Children.Add(pill);
   }else if(book.IsCurrentFolderBook){
    coverHost.Children.Add(Badge(L10n.T("本目录"),"AccentInk","Accent"));
   }
   content.Children.Add(coverHost);
   var info=new StackPanel{Margin=new Thickness(0,CoverHeight+10,0,0)};
   var title=Label(book.Title,13,"Fg");title.FontWeight=FontWeights.SemiBold;info.Children.Add(title);
   var meta=Label(Caption==null?book.PageCount+L10n.T(" 页"):Caption(book),10.5,"Muted");meta.Margin=new Thickness(0,3,0,0);info.Children.Add(meta);
   if(!folder&&book.ReadPercent>0){
    var track=new Border{Height=2,Background=Resource(this,"Line"),CornerRadius=new CornerRadius(1),Margin=new Thickness(0,7,0,0),HorizontalAlignment=HorizontalAlignment.Stretch};
    var fill=new Border{Height=2,Background=Resource(this,"Accent"),CornerRadius=new CornerRadius(1),HorizontalAlignment=HorizontalAlignment.Left,Width=Math.Max(2,(box.Width-2)*Math.Min(100,book.ReadPercent)/100)};
    var progress=new Grid();progress.Children.Add(track);progress.Children.Add(fill);info.Children.Add(progress);
   }
   content.Children.Add(info);
   var button=new Button{Content=content,Width=box.Width,Height=box.Height,Style=(Style)FindResource("CardButton"),ToolTip=book.Id,Margin=new Thickness(0),Padding=new Thickness(0)};
   button.RenderTransform=new TranslateTransform(0,0);
   button.MouseEnter+=(s,e)=>{((TranslateTransform)button.RenderTransform).Y=-2;button.SetResourceReference(Control.BorderBrushProperty,"AccentLine");};
   button.MouseLeave+=(s,e)=>{((TranslateTransform)button.RenderTransform).Y=0;button.BorderBrush=Brushes.Transparent;};
   button.Click+=(s,e)=>{if(BookOpened!=null)BookOpened(book);};
   var menu=new ContextMenu();
   var openItem=new MenuItem{Header=folder?L10n.T("打开文件夹"):L10n.T("开始阅读")};
   openItem.Click+=(s,e)=>{if(BookOpened!=null)BookOpened(book);};
   menu.Items.Add(openItem);
   if(HasGridButton(book)){
    var gridItem=new MenuItem{Header=L10n.T("平铺查看")};
    gridItem.Click+=(s,e)=>{if(BookGridOpened!=null)BookGridOpened(book);};
    menu.Items.Add(gridItem);
   }
   bool deletable=CanDeleteBook!=null?CanDeleteBook(book):(book.RemoteId==null);
   if(deletable){
    if(BookRevealRequested!=null){
     var revealItem=new MenuItem{Header=L10n.T("在资源管理器中打开")};
     revealItem.Click+=(s,e)=>BookRevealRequested(book);
     menu.Items.Add(revealItem);
    }
    if(BookDeleteRequested!=null){
     menu.Items.Add(new Separator());
     bool archive=!folder&&book.Pages.Count>0&&book.Pages[0].Entry!=null;
     var deleteItem=new MenuItem{Header=archive?L10n.T("删除压缩包…"):L10n.T("删除文件夹…")};
     deleteItem.Click+=(s,e)=>BookDeleteRequested(book);
     menu.Items.Add(deleteItem);
    }
   }
   button.ContextMenu=menu;
   var host=new Grid{Width=box.Width,Height=box.Height};host.Children.Add(button);
   if(HasGridButton(book)){
    var gridButton=new Button{Content="▦",Width=24,Height=24,FontSize=12,Padding=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(8),ToolTip=L10n.T("平铺查看"),Opacity=.9};
    gridButton.SetResourceReference(Control.BackgroundProperty,"Bg");gridButton.SetResourceReference(Control.ForegroundProperty,"Accent");gridButton.SetResourceReference(Control.BorderBrushProperty,"Line");gridButton.BorderThickness=new Thickness(1);
    gridButton.Click+=(s,e)=>{e.Handled=true;if(BookGridOpened!=null)BookGridOpened(book);};
    Panel.SetZIndex(gridButton,4);host.Children.Add(gridButton);gridButtons++;
   }
   return host;
  }
  async void LoadNextCover(){
   if(loading||!IsVisible)return;
   int index=visible.Keys.OrderBy(i=>Math.Abs(slots[i].Top-viewer.VerticalOffset)).FirstOrDefault(i=>!cache.ContainsKey(CoverKey(books[i])));
   if(!visible.ContainsKey(index)||cache.ContainsKey(CoverKey(books[index])))return;
   Book book=books[index];int gen=generation;loading=true;var cover=new Cover();
   try{cover.Image=await Task.Run(()=>{var page=book.Pages[0];page.ReadInfo();double ratio=(double)page.Height/Math.Max(1,page.Width);return page.Decode((int)Math.Max(24,Math.Min(400,Math.Sqrt(300000/Math.Max(.01,ratio)))));});}
   catch{cover.Failed=true;}
   finally{loading=false;}
   if(Dispatcher.HasShutdownStarted)return;
   if(gen==generation){cover.Used=++clock;cache[CoverKey(book)]=cover;
    FrameworkElement old;if(visible.TryGetValue(index,out old)){canvas.Children.Remove(old);visible.Remove(index);}RefreshVisible();
   }
   else RefreshVisible();
  }
 }
}
