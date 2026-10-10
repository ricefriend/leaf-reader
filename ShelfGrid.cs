using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LeafReader {
 // The canvas contains only cards around the viewport; the full library has
 // lightweight rectangles, so scrolling never accumulates thousands of images.
 public sealed class ShelfGrid : Grid {
  readonly ScrollViewer viewer;readonly Canvas canvas=new Canvas();
  List<Book> books=new List<Book>();Rect[] slots=new Rect[0];
  readonly Dictionary<int,Button> visible=new Dictionary<int,Button>();
  class Cover {public BitmapSource Image;public bool Failed;public long Used;}
  readonly Dictionary<string,Cover> cache=new Dictionary<string,Cover>();
  bool loading,refreshing;int generation;long clock;double layoutWidth;
  public Action<Book> BookOpened;public Func<Book,string> Caption;
  public ScrollViewer Scroller {get{return viewer;}}
  internal int RealizedCount {get{return visible.Count;}}
  internal int BookCount {get{return books.Count;}}
  internal int[] RealizedIndices {get{return visible.Keys.OrderBy(i=>i).ToArray();}}
  internal bool VisibleCoversReady {get{return visible.Keys.All(i=>cache.ContainsKey(CoverKey(books[i])));}}
  internal Rect[] CardBounds {get{return slots;}}
  public ShelfGrid(){
   viewer=new ScrollViewer{Content=canvas,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Padding=new Thickness(0,0,8,10)};
   Children.Add(viewer);viewer.ScrollChanged+=(s,e)=>{if(Math.Abs(viewer.ViewportWidth-layoutWidth)>1)LayoutCards(true);else RefreshVisible();};
   SizeChanged+=(s,e)=>LayoutCards(true);IsVisibleChanged+=(s,e)=>{if(IsVisible)RefreshVisible();};
  }
  static string CoverKey(Book book){return book.Id+"|"+book.Pages[0].Key;}
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
   double width=Math.Max(180,viewer.ViewportWidth>0?viewer.ViewportWidth:ActualWidth-26);layoutWidth=viewer.ViewportWidth;
   const double cardWidth=180,cardHeight=296,gap=18;
   int columns=Math.Max(1,(int)Math.Floor((width+gap)/(cardWidth+gap)));slots=new Rect[books.Count];
   for(int i=0;i<books.Count;i++){
    slots[i]=new Rect((i%columns)*(cardWidth+gap),(i/columns)*(cardHeight+gap),cardWidth,cardHeight);
   }
   canvas.Height=books.Count==0?0:Math.Ceiling((double)books.Count/columns)*(cardHeight+gap);canvas.Width=width;
   // Covers have fixed dimensions. Only the column count changes on resize.
   visible.Clear();canvas.Children.Clear();
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
    // Keep images near the viewport and a bounded recent cache.
    var pinned=new HashSet<string>(needed.Select(i=>CoverKey(books[i])));
    while(cache.Count>Math.Max(72,pinned.Count)){var oldest=cache.Where(kv=>!pinned.Contains(kv.Key)).OrderBy(kv=>kv.Value.Used).FirstOrDefault();if(oldest.Key==null)break;cache.Remove(oldest.Key);}
   }finally{refreshing=false;}
   LoadNextCover();
  }
  TextBlock Label(string text,int size,string color){var label=new TextBlock{Text=text,FontSize=size,TextTrimming=TextTrimming.CharacterEllipsis};label.SetResourceReference(TextBlock.ForegroundProperty,color);return label;}
  Button MakeCard(int index,Cover cover){
   var book=books[index];var box=slots[index];var stack=new StackPanel();
   var coverBox=new Grid{Height=box.Height-76,ClipToBounds=true};coverBox.SetResourceReference(Panel.BackgroundProperty,"Panel");
   if(cover!=null&&cover.Image!=null)coverBox.Children.Add(new Image{Source=cover.Image,Stretch=Stretch.UniformToFill});
   else{var placeholder=Label(cover!=null&&cover.Failed?L10n.T("封面无法读取"):L10n.T("正在加载…"),12,"Muted");placeholder.HorizontalAlignment=HorizontalAlignment.Center;placeholder.VerticalAlignment=VerticalAlignment.Center;coverBox.Children.Add(placeholder);}
   stack.Children.Add(coverBox);var title=Label((book.GroupId!=null?"▸ ":"")+book.Title,14,"Fg");title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(0,10,0,5);stack.Children.Add(title);stack.Children.Add(Label(Caption==null?book.Pages.Count+L10n.T(" 页"):Caption(book),11,"Muted"));
   var button=new Button{Content=stack,Width=box.Width,Height=box.Height,Padding=new Thickness(8),Margin=new Thickness(0),Background=Brushes.Transparent,HorizontalContentAlignment=HorizontalAlignment.Stretch,ToolTip=(book.RemoteId==null?book.Id:book.SourceLabel+" · "+book.Folder)+L10n.T("\n创建时间：")+book.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")};
   button.Click+=(s,e)=>{if(BookOpened!=null)BookOpened(book);};return button;
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
    Button old;if(visible.TryGetValue(index,out old)){canvas.Children.Remove(old);visible.Remove(index);}RefreshVisible();
   }
   else RefreshVisible();
  }
 }
}
