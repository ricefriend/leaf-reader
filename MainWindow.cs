using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LeafReader {
 public sealed partial class MainWindow:Window {
  public readonly Settings State;
  public readonly ReaderView Reader=new ReaderView();
  Grid root,body,shelf,readerHost;StackPanel shelfHead,readerHead,readerFoot;ShelfGrid cards;Grid shelfSurface;
  TextBlock message,summary,bookTitle,pageLabel,zoomLabel;
  TextBox search;ComboBox mode,fit,sort;CheckBox cover,wide;ScrollBar scroll;Slider pageSlider;Button bookmark,back,shiftBack,shiftNext;
  List<Book> books=new List<Book>();List<string> warnings=new List<string>();
  int operation;bool updating,busy,fullscreen;WindowState beforeFull;WindowStyle beforeStyle;ResizeMode beforeResize;
  DispatcherTimer saveTimer,idleTimer;bool testMode,uiHidden,progressDragging;string[] startup;Point? lastPointer;
  internal bool ReadingUiHidden {get{return uiHidden;}}
  internal Slider PageProgressControl {get{return pageSlider;}}
  internal ComboBox ShelfSortControl {get{return sort;}}
  public MainWindow(bool testing,string[] args){
   testMode=testing;startup=args;State=Settings.Load();L10n.English=(State.Language??System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)=="en";
   Title=L10n.T("栞 · Leaf Reader");FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
   Width=Math.Max(960,Math.Min(2200,State.Width));Height=Math.Max(640,Math.Min(1400,State.Height));MinWidth=960;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   SetResourceReference(BackgroundProperty,"Bg");SetResourceReference(ForegroundProperty,"Fg");
   Build();ApplyTheme();
   saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};saveTimer.Tick+=(s,e)=>{saveTimer.Stop();Save();};
   idleTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};idleTimer.Tick+=(s,e)=>HideIdleUi();
   PreviewMouseMove+=PointerMoved;PreviewMouseWheel+=(s,e)=>ReadingActivity(false);
   PreviewMouseDown+=(s,e)=>ReadingActivity(false);PreviewMouseUp+=(s,e)=>ReadingActivity(false);
   Activated+=(s,e)=>ReadingActivity(true);Deactivated+=(s,e)=>{progressDragging=false;idleTimer.Stop();SetReadingUiHidden(false);};
   Reader.Changed=ReadingChanged;Reader.ToggleFullscreen=ToggleFull;
   PreviewKeyDown+=Keys;AllowDrop=true;DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
   Drop+=async(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))await Import((string[])e.Data.GetData(DataFormats.FileDrop));};
   Closing+=(s,e)=>{idleTimer.Stop();saveTimer.Stop();if(sharing!=null)sharing.Dispose();Remember();if(!fullscreen&&WindowState==WindowState.Normal){State.Width=ActualWidth;State.Height=ActualHeight;}Save();};
   Loaded+=async(s,e)=>{if(testMode)return;if(Settings.LoadWarning!=null)MessageBox.Show(this,Settings.LoadWarning,L10n.T("阅读记录"));if(startup.Length>0)await Import(startup);else if(State.Roots.Count>0||State.Servers.Count>0)await Scan();};
  }
  Brush R(string key){return (Brush)FindResource(key);}
  TextBlock Text(string text,double size=13,string resource="Fg"){var t=new TextBlock{Text=text,FontSize=size,VerticalAlignment=VerticalAlignment.Center};t.SetResourceReference(TextBlock.ForegroundProperty,resource);return t;}
  Button Btn(string title,Action action,string tip=null){var b=new Button{Content=title,ToolTip=tip??title};b.Click+=(s,e)=>action();return b;}
  void Build(){
   root=new Grid();root.SetResourceReference(Panel.BackgroundProperty,"Bg");root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
   var header=new DockPanel{Margin=new Thickness(26,18,20,16)};
   var mark=new Border{Background=R("Accent"),CornerRadius=new CornerRadius(8),Width=36,Height=36,Margin=new Thickness(0,0,11,0),Child=new TextBlock{Text=L10n.T("栞"),FontSize=23,Foreground=new SolidColorBrush(Color.FromRgb(28,49,37)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};header.Children.Add(mark);
   var brand=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,30,0)};brand.Children.Add(Text("LEAF READER",15));brand.Children.Add(Text(L10n.T("留一页，给自己。"),10,"Muted"));header.Children.Add(brand);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Right);
   actions.Children.Add(Btn(L10n.T("导入文件夹"),ChooseFolder,L10n.T("Ctrl+O · 导入母文件夹，递归扫描子文件夹")));actions.Children.Add(Btn(L10n.T("打开压缩包"),ChooseArchive,L10n.T("直接阅读 ZIP / CBZ")));
   actions.Children.Add(Btn(L10n.T("共享"),SharingDialog,L10n.T("选择授权目录并开启局域网共享")));actions.Children.Add(Btn(L10n.T("服务器"),ServersDialog,L10n.T("配对或重新连接另一台电脑")));actions.Children.Add(Btn(L10n.Pick("设置","Settings"),SettingsDialog));header.Children.Add(actions);header.Children.Add(new Border());root.Children.Add(header);
   body=new Grid();Grid.SetRow(body,1);root.Children.Add(body);
   shelf=new Grid{Margin=new Thickness(30,0,23,0)};shelf.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});shelf.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});shelf.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.Children.Add(shelf);
   shelfHead=new StackPanel();var heading=new DockPanel{Margin=new Thickness(0,0,0,16)};
   var counts=new StackPanel();counts.Children.Add(Text(L10n.T("我的书架"),28));summary=Text(L10n.T("导入一个文件夹，开始阅读"),12,"Muted");summary.Margin=new Thickness(0,6,0,0);counts.Children.Add(summary);heading.Children.Add(counts);
   var controls=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom};controls.Children.Add(Btn(L10n.T("刷新"),async()=>await Scan(),L10n.T("F5 · 重新扫描导入位置")));controls.Children.Add(Btn(L10n.T("管理来源"),ManageSources,L10n.T("移除来源只更改书架，不删除图片")));heading.Children.Add(controls);shelfHead.Children.Add(heading);
   var filter=new DockPanel{Margin=new Thickness(0,0,0,17)};sort=new ComboBox{Width=188,ItemsSource=new[]{L10n.T("按名称排序"),L10n.T("最近阅读"),L10n.T("未读优先"),L10n.T("创建时间 · 新的在前"),L10n.T("创建时间 · 旧的在前")},SelectedIndex=Math.Max(0,Math.Min(4,State.ShelfSort)),ToolTip=L10n.T("文件夹按其创建时间；ZIP / CBZ 按压缩包文件的创建时间")};sort.SelectionChanged+=(s,e)=>{State.ShelfSort=sort.SelectedIndex;ScheduleSave();RenderShelf(true);};DockPanel.SetDock(sort,Dock.Right);filter.Children.Add(sort);
   var searchLabel=Text(L10n.T("搜索"),12,"Muted");searchLabel.Margin=new Thickness(0,0,12,0);filter.Children.Add(searchLabel);search=new TextBox{ToolTip=L10n.T("搜索书名或文件夹路径 · Ctrl+F"),MaxWidth=510,HorizontalAlignment=HorizontalAlignment.Left,MinWidth=280};search.TextChanged+=(s,e)=>{RenderShelf(true);};filter.Children.Add(search);shelfHead.Children.Add(filter);shelf.Children.Add(shelfHead);
   shelfSurface=new Grid();cards=new ShelfGrid();cards.BookOpened=async b=>await OpenBook(b);cards.Caption=b=>{Progress p;State.Books.TryGetValue(b.Id,out p);return b.PageCount+L10n.T(" 页")+(p!=null&&!string.IsNullOrEmpty(p.LastRead)?L10n.T("  ·  读至 ")+Math.Min(b.PageCount,p.Page+1):L10n.T("  ·  未读"));};shelfSurface.Children.Add(cards);Grid.SetRow(shelfSurface,1);shelf.Children.Add(shelfSurface);
   readerHost=new Grid{Visibility=Visibility.Collapsed};readerHost.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});readerHost.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});readerHost.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.Children.Add(readerHost);
   readerHead=new StackPanel{Margin=new Thickness(22,0,15,12)};var titlebar=new DockPanel();back=Btn(L10n.T("‹ 书架"),Back,L10n.T("Esc · 返回书架"));titlebar.Children.Add(back);bookTitle=Text("",18);bookTitle.TextTrimming=TextTrimming.CharacterEllipsis;bookTitle.Margin=new Thickness(12,0,0,0);titlebar.Children.Add(bookTitle);readerHead.Children.Add(titlebar);
   var options=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
   mode=new ComboBox{Width=156,ItemsSource=new[]{L10n.T("单页 · 标准看图"),L10n.T("双页 · 从左至右"),L10n.T("双页 · 从右至左"),L10n.T("Webtoon · 纵向")}};mode.SelectionChanged+=(s,e)=>{if(!updating&&Reader.Book!=null){Reader.SetMode(mode.SelectedIndex);Reader.Focus();}};options.Children.Add(mode);
   fit=new ComboBox{Width=112,ItemsSource=new[]{L10n.T("适应窗口"),L10n.T("适应宽度"),L10n.T("原始尺寸")},SelectedIndex=0};fit.SelectionChanged+=(s,e)=>{if(!updating){Reader.Fit=fit.SelectedIndex;Reader.Zoom=1;Reader.RefreshLayout();Reader.Focus();}};options.Children.Add(fit);
   cover=new CheckBox{Content=L10n.T("封面独页"),ToolTip=L10n.T("双页模式中让第一张图片单独显示")};wide=new CheckBox{Content=L10n.T("横图独页"),ToolTip=L10n.T("双页模式下，宽高比大于 1.18 的整张跨页图单独显示")};cover.Click+=(s,e)=>{if(Reader.Book!=null){Reader.Progress.Cover=cover.IsChecked==true;Reader.RefreshLayout();}};wide.Click+=(s,e)=>{if(Reader.Book!=null){Reader.Progress.Wide=wide.IsChecked==true;Reader.RefreshLayout();}};
   options.Children.Add(Btn("−",()=>Reader.SetZoom(Reader.Zoom/1.2),L10n.T("缩小 · -")));zoomLabel=Text("100%");zoomLabel.Width=48;options.Children.Add(zoomLabel);options.Children.Add(Btn("+",()=>Reader.SetZoom(Reader.Zoom*1.2),L10n.T("放大 · +")));options.Children.Add(Btn(L10n.T("重置"),()=>{Reader.Zoom=1;Reader.Fit=0;fit.SelectedIndex=0;Reader.RefreshLayout();},L10n.T("0 · 重置缩放和位置")));options.Children.Add(Btn(L10n.T("全屏"),ToggleFull,L10n.T("F11 / 双击画面 · 全屏")));readerHead.Children.Add(options);readerHost.Children.Add(readerHead);
   var viewGrid=new Grid();viewGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});viewGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});viewGrid.Children.Add(Reader);scroll=new ScrollBar{Orientation=Orientation.Vertical,Width=14,SmallChange=80,Visibility=Visibility.Collapsed};scroll.ValueChanged+=(s,e)=>{if(!updating)Reader.SetOffset(scroll.Value);};Grid.SetColumn(scroll,1);viewGrid.Children.Add(scroll);Grid.SetRow(viewGrid,1);readerHost.Children.Add(viewGrid);
   readerFoot=new StackPanel{Margin=new Thickness(22,10,15,12)};var nav=new DockPanel();
   var navleft=new StackPanel{Orientation=Orientation.Horizontal};navleft.Children.Add(Btn(L10n.T("上一页"),()=>Reader.Turn(-1),L10n.T("PageUp / 滚轮向上")));navleft.Children.Add(Btn(L10n.T("下一页"),()=>Reader.Turn(1),L10n.T("PageDown / 空格 / 滚轮向下")));shiftBack=Btn(L10n.T("配对 −1"),()=>Reader.Nudge(-1),L10n.T("[ 或 Shift+← · 起始页前移一张"));shiftNext=Btn(L10n.T("配对 +1"),()=>Reader.Nudge(1),L10n.T("] 或 Shift+→ · 起始页后移一张"));navleft.Children.Add(shiftBack);navleft.Children.Add(shiftNext);nav.Children.Add(navleft);
   var navright=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(navright,Dock.Right);bookmark=Btn(L10n.T("☆ 书签"),ToggleBookmark,L10n.T("B · 添加 / 取消当前页书签"));navright.Children.Add(bookmark);navright.Children.Add(Btn(L10n.T("书签列表"),Bookmarks));navright.Children.Add(Btn(L10n.T("跳页"),JumpDialog,L10n.T("Ctrl+G · 输入页码")));nav.Children.Add(navright);pageLabel=Text("");pageLabel.Margin=new Thickness(15,0,15,0);pageLabel.HorizontalAlignment=HorizontalAlignment.Center;nav.Children.Add(pageLabel);readerFoot.Children.Add(nav);
   pageSlider=new Slider{Minimum=1,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(3,11,8,0),ToolTip=L10n.T("拖动跳转页码")};pageSlider.ValueChanged+=(s,e)=>{if(!updating&&Reader.Book!=null)Reader.Jump((int)pageSlider.Value-1);};readerFoot.Children.Add(pageSlider);Grid.SetRow(readerFoot,2);readerHost.Children.Add(readerFoot);
   pageSlider.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler((s,e)=>{progressDragging=true;ReadingActivity(false);}));pageSlider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler((s,e)=>{progressDragging=false;ReadingActivity(false);}));
   message=Text(L10n.T("支持 JPG · PNG · BMP · GIF · TIFF · ZIP / CBZ    |    可直接拖入文件夹或图片"),11,"Muted");message.Margin=new Thickness(28,5,24,10);message.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetRow(message,2);root.Children.Add(message);RenderShelf();
  }
  void ApplyTheme(){
   string[] keys={"Bg","Panel","Hover","Fg","Muted","Accent"};string[] values=State.Light?new[]{"#F3F2EE","#E6E6E0","#D6DCD4","#252D29","#606B65","#559873"}:new[]{"#171A21","#20242D","#303743","#EDF0F5","#A4ACBA","#A8D9BC"};
   for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));Reader.Light=State.Light;Reader.InvalidateVisual();RenderShelf();
  }
  async void ChooseFolder(){if(busy)return;using(var d=new System.Windows.Forms.FolderBrowserDialog{Description=L10n.T("选择母文件夹：将扫描其中所有子文件夹"),ShowNewFolderButton=false}){if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK)await Import(new[]{d.SelectedPath});}}
  async void ChooseArchive(){if(busy)return;var d=new OpenFileDialog{Filter=L10n.T("漫画压缩包|*.cbz;*.zip"),Multiselect=true};if(d.ShowDialog(this)==true)await Import(d.FileNames);}
  async Task Import(string[] paths){
   if(busy){Status(L10n.T("正在处理，请完成后再导入。"));return;}string image=null;
   foreach(string input in paths){try{string path=Path.GetFullPath(input.Trim('"'));if(File.Exists(path)&&Library.IsImage(path)){image=path;path=Path.GetDirectoryName(path);}if(!Directory.Exists(path)&&!(File.Exists(path)&&Library.IsArchive(path)))continue;if(!State.Roots.Any(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase)))State.Roots.Add(path);}catch{}}
   Save();await Scan();if(image!=null){Book b=books.FirstOrDefault(x=>x.Pages.Any(p=>p.Path==image));if(b!=null){await OpenBook(b);Reader.Jump(b.Pages.FindIndex(p=>p.Path==image));}}
  }
  public async Task Scan(){
   if(busy)return;Remember();if(Reader.Book!=null)Back();busy=true;Status(L10n.T("正在扫描文件夹…"));int op=++operation;var roots=State.Roots.ToArray();
   try{var result=await Task.Run(()=>Library.Scan(roots));if(op!=operation)return;books=result.Books.Concat(books.Where(b=>b.RemoteId!=null)).ToList();warnings=result.Warnings;cards.ClearCoverCache();RenderShelf(true);foreach(var profile in State.Servers.ToArray())try{await ConnectServer(profile);}catch(Exception ex){warnings.Add(profile.Name+"："+ex.Message);}Status(L10n.T("扫描完成 · ")+books.Count+L10n.T(" 本 · ")+books.Sum(b=>b.Pages.Count)+L10n.T(" 张图片")+(warnings.Count>0?" · "+warnings.Count+L10n.T(" 个位置未能读取，见“管理来源”"):""));}
   catch(Exception e){Status(L10n.T("扫描失败：")+e.Message);}finally{busy=false;}
  }
  List<Book> FilterBooks(){IEnumerable<Book> list=books;string q=search==null?"":search.Text.Trim();if(q.Length>0)list=list.Where(b=>b.Title.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0||b.Id.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0);
   if(sort!=null&&sort.SelectedIndex==1)list=list.OrderByDescending(b=>State.Books.ContainsKey(b.Id)?State.Books[b.Id].LastRead:"");else if(sort!=null&&sort.SelectedIndex==2)list=list.OrderBy(b=>State.Books.ContainsKey(b.Id)&&!string.IsNullOrEmpty(State.Books[b.Id].LastRead));else if(sort!=null&&sort.SelectedIndex>=3)list=Library.ByCreated(list,sort.SelectedIndex==3);return list.ToList();}
  internal ShelfGrid ShelfControl {get{return cards;}}
  internal TextBox ShelfSearch {get{return search;}}
  void RenderShelf(bool reset=false){
   if(cards==null)return;var selected=FilterBooks();if(summary!=null)summary.Text=books.Count+L10n.T(" 本藏书   /   ")+State.Roots.Count+L10n.T(" 个来源   /   ")+selected.Count+L10n.T(" 本匹配");
   if(shelfSurface.Children.Count>1)shelfSurface.Children.RemoveAt(1);
   cards.SetBooks(selected,reset);cards.Visibility=selected.Count==0?Visibility.Collapsed:Visibility.Visible;
   if(selected.Count==0){var empty=new StackPanel{Margin=new Thickness(10,65,10,0)};empty.Children.Add(Text(books.Count==0?L10n.T("把故事，放进书架。"):L10n.T("没有找到匹配的漫画"),30));var desc=Text(books.Count==0?L10n.T("导入母文件夹，自动发现每个子文件夹里的图片。\n第一张图成为封面，下次打开接着读。"):L10n.T("试试其他书名或文件夹关键词。"),14,"Muted");desc.LineHeight=26;desc.Margin=new Thickness(0,16,0,24);empty.Children.Add(desc);if(books.Count==0){var add=Btn(L10n.T("＋ 导入文件夹"),ChooseFolder);add.HorizontalAlignment=HorizontalAlignment.Left;empty.Children.Add(add);empty.Children.Add(Text(L10n.T("也支持拖放文件夹、图片，以及 ZIP / CBZ。"),12,"Muted"));}shelfSurface.Children.Add(empty);}
  }
  public async Task OpenBook(Book book){
   if(busy)return;busy=true;Status(L10n.T("正在读取 “")+book.Title+L10n.T("” 的图片尺寸…"));int op=++operation;
   try{await Task.Run(()=>{if(book.RemoteId!=null){var remoteSource=(RemoteImageSource)book.Pages[0].Source;var profile=State.Servers.First(p=>p.ServerId==remoteSource.Id);new RemoteApi(profile).LoadPages(book);}foreach(var page in book.Pages)page.ReadInfo();});if(op!=operation)return;Remember();var progress=State.Get(book);shelf.Visibility=Visibility.Collapsed;readerHost.Visibility=Visibility.Visible;bookTitle.Text=book.Title+" · "+(book.SourceLabel??L10n.T("本地"));bookTitle.ToolTip=book.Id;Reader.Open(book,progress);Reader.Focus();ReadingActivity(true);Status(L10n.T("闲置 3 秒隐藏工具栏 · 鼠标移至上下边缘或按 Tab 显示 · F11 全屏"));if(book.Pages.Any(p=>p.Error!=null))Status(L10n.T("部分图片无法解码，可继续翻页；WebP 取决于 Windows 图像解码器。"));}catch(Exception e){Status(L10n.T("打开失败：")+e.Message);}finally{busy=false;}
  }
  public void SetTestBooks(List<Book> items){books=items;RenderShelf();}
  void Remember(){if(Reader.Book==null)return;Reader.Progress.LastRead=DateTime.UtcNow.ToString("o");Reader.Progress.PageId=Reader.Book.Pages[Reader.Progress.Page].Identity;Reader.Progress.PairAnchorId=Reader.Book.Pages[Math.Max(0,Math.Min(Reader.Book.Pages.Count-1,Reader.Progress.PairAnchor))].Identity;Reader.Progress.BookmarkIds=Reader.Progress.Bookmarks.Where(i=>i>=0&&i<Reader.Book.Pages.Count).Select(i=>Reader.Book.Pages[i].Identity).ToList();State.Books[Reader.Book.Id]=Reader.Progress;}
  void ReadingChanged(){
   if(Reader.Book==null)return;updating=true;var p=Reader.Progress;mode.SelectedIndex=p.Mode;fit.SelectedIndex=Reader.Fit;cover.IsChecked=p.Cover;wide.IsChecked=p.Wide;bool spread=p.Mode==1||p.Mode==2;cover.IsEnabled=wide.IsEnabled=shiftBack.IsEnabled=shiftNext.IsEnabled=spread;fit.IsEnabled=!Reader.IsToon;
   int n=Reader.VisibleCount;pageLabel.Text=(p.Page+1)+(n==2?"–"+(p.Page+2):"")+" / "+Reader.Book.Pages.Count;zoomLabel.Text=Math.Round(Reader.Zoom*100)+"%";pageSlider.IsDirectionReversed=p.Mode==2;pageSlider.ToolTip=p.Mode==2?L10n.T("右端为第 1 页，向左推进 · 拖动跳页"):L10n.T("左端为第 1 页，向右推进 · 拖动跳页");pageSlider.Maximum=Reader.Book.Pages.Count;pageSlider.Value=p.Page+1;bookmark.Content=p.Bookmarks.Contains(p.Page)?L10n.T("★ 已标记"):L10n.T("☆ 书签");
   scroll.Visibility=Reader.IsToon?Visibility.Visible:Visibility.Collapsed;scroll.Maximum=Math.Max(0,Reader.Extent-Reader.ActualHeight);scroll.ViewportSize=Reader.ActualHeight;scroll.LargeChange=Math.Max(80,Reader.ActualHeight*.85);scroll.Value=Reader.Offset;updating=false;Remember();ScheduleSave();
  }
  void ScheduleSave(){if(saveTimer==null)return;saveTimer.Stop();saveTimer.Start();}
  void Save(){string error=State.Save();if(error!=null)Status(error);}
  void Status(string text){message.Text=text;message.ToolTip=text;}
  void Back(){if(busy)return;if(fullscreen)ToggleFull();idleTimer.Stop();SetReadingUiHidden(false);Remember();Save();Reader.CloseBook();readerHost.Visibility=Visibility.Collapsed;shelf.Visibility=Visibility.Visible;RenderShelf();Status(L10n.T("书架已更新 · 阅读进度自动保存"));}
  void ToggleFull(){if(Reader.Book==null)return;fullscreen=!fullscreen;if(fullscreen){beforeFull=WindowState;beforeStyle=WindowStyle;beforeResize=ResizeMode;WindowState=WindowState.Normal;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowState=WindowState.Maximized;}else{WindowState=WindowState.Normal;WindowStyle=beforeStyle;ResizeMode=beforeResize;WindowState=beforeFull;}SetReadingUiHidden(fullscreen);ReadingActivity(false);Reader.Focus();}
  void SetReadingUiHidden(bool hidden){
   hidden=hidden&&Reader.Book!=null;if(uiHidden==hidden)return;uiHidden=hidden;
   var visibility=hidden?Visibility.Collapsed:Visibility.Visible;
   readerHead.Visibility=readerFoot.Visibility=root.Children[0].Visibility=message.Visibility=visibility;
   scroll.Opacity=hidden?0:1;scroll.IsHitTestVisible=!hidden;
   if(hidden)Reader.Focus();
  }
  internal void ReadingActivity(bool reveal){
   if(Reader.Book==null||idleTimer==null)return;if(reveal)SetReadingUiHidden(false);
   idleTimer.Stop();if(!uiHidden)idleTimer.Start();
  }
  bool UsingReadingControls(){return progressDragging||mode.IsDropDownOpen||fit.IsDropDownOpen||Mouse.Captured!=null||readerHead.IsMouseOver||readerFoot.IsMouseOver||root.Children[0].IsMouseOver||scroll.IsMouseOver;}
  void HideIdleUi(){
   if(State.AutoHide==false){idleTimer.Stop();SetReadingUiHidden(false);return;}
   if(Reader.Book==null){idleTimer.Stop();return;}
   if((!IsActive&&!testMode)||busy||OwnedWindows.Count>0||UsingReadingControls())return;
   idleTimer.Stop();SetReadingUiHidden(true);
  }
  internal void ReadingPointerAt(Point point){
   if(Reader.Book==null)return;
   bool edge=point.Y<=32||point.Y>=root.ActualHeight-32;
   ReadingActivity(edge);
  }
  void PointerMoved(object sender,MouseEventArgs e){
   var point=e.GetPosition(root);if(lastPointer.HasValue&&(point-lastPointer.Value).Length<2)return;lastPointer=point;
   if(Mouse.Captured!=null){ReadingActivity(false);return;}ReadingPointerAt(point);
  }
  void ToggleBookmark(){if(Reader.Book==null)return;int page=Reader.Progress.Page;var marks=Reader.Progress.Bookmarks;if(marks.Contains(page))marks.Remove(page);else{marks.Add(page);marks.Sort();}ReadingChanged();}
  Window Dialog(string title,int width,int height){return new Window{Owner=this,Title=title,Width=width,Height=height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=R("Bg"),Foreground=R("Fg"),FontFamily=FontFamily,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false};}
  void JumpDialog(){if(Reader.Book==null)return;var d=Dialog(L10n.T("跳转页码"),340,190);var stack=new StackPanel{Margin=new Thickness(22)};stack.Children.Add(Text(L10n.T("输入页码（1–")+Reader.Book.Pages.Count+"）"));var box=new TextBox{Text=(Reader.Progress.Page+1).ToString(),Margin=new Thickness(0,12,0,14)};stack.Children.Add(box);var go=Btn(L10n.T("跳转"),()=>{int p;if(int.TryParse(box.Text,out p)&&p>=1&&p<=Reader.Book.Pages.Count){Reader.Jump(p-1);d.Close();}else box.SelectAll();});go.IsDefault=true;stack.Children.Add(go);d.Content=stack;d.Loaded+=(s,e)=>{box.Focus();box.SelectAll();};d.ShowDialog();Reader.Focus();}
  void Bookmarks(){if(Reader.Book==null)return;var d=Dialog(L10n.T("书签 · ")+Reader.Book.Title,420,440);var stack=new StackPanel{Margin=new Thickness(20)};if(Reader.Progress.Bookmarks.Count==0)stack.Children.Add(Text(L10n.T("还没有书签。按 B 标记当前页。"),14,"Muted"));foreach(int p in Reader.Progress.Bookmarks.Where(p=>p<Reader.Book.Pages.Count).ToArray()){int target=p;var btn=Btn(L10n.T("第 ")+(p+1)+L10n.T(" 页   ·   ")+Reader.Book.Pages[p].Name,()=>{Reader.Jump(target);d.Close();});btn.Margin=new Thickness(0,0,0,8);stack.Children.Add(btn);}d.Content=new ScrollViewer{Content=stack};d.ShowDialog();Reader.Focus();}
  void ManageSources(){if(busy)return;var d=Dialog(L10n.T("管理书架来源"),700,460);var panel=new DockPanel{Margin=new Thickness(20)};var hint=Text(L10n.T("移除来源仅从书架移除，不会删除原文件。"),12,"Muted");DockPanel.SetDock(hint,Dock.Top);panel.Children.Add(hint);var list=new ListBox{Margin=new Thickness(0,15,0,15),Background=R("Panel"),Foreground=R("Fg"),BorderThickness=new Thickness(0)};foreach(string r in State.Roots)list.Items.Add(r);
   var buttons=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(buttons,Dock.Bottom);buttons.Children.Add(Btn(L10n.T("移除所选"),()=>{var item=list.SelectedItem as string;if(item!=null){State.Roots.Remove(item);list.Items.Remove(item);Save();}}));buttons.Children.Add(Btn(L10n.T("扫描详情"),()=>MessageBox.Show(d,warnings.Count==0?L10n.T("没有扫描错误。"):string.Join("\n",warnings.Take(40)),L10n.T("扫描详情"))));buttons.Children.Add(Btn(L10n.T("完成"),()=>d.Close()));panel.Children.Add(buttons);panel.Children.Add(list);d.Content=panel;d.ShowDialog();ScanAfterSources();}
  async void ScanAfterSources(){await Scan();}
  void Help(){var d=Dialog(L10n.T("操作说明 · Leaf Reader"),720,630);var t=Text(L10n.T("阅读界面\n闲置 3 秒自动隐藏工具栏；鼠标移至上下边缘或按 Tab 恢复。\n右至左模式的进度条从右向左推进；其他模式从左向右。\n书架可按创建时间升降序排序，并记住选择。\n\n四种阅读模式\n单页：每次显示一张。双页：按所选方向排列与翻页。\nWebtoon：图片连续上下排列，滚轮平滑滚动。\n\n双页配对\n[ / ] 或 Shift+← / →：起始页前移 / 后移一张。\n例：11–12 页按 ] 变为 12–13 页，下一次翻页为 14–15 页。\n“封面独页”让第 1 张单独显示；“横图独页”识别整张跨页图。\n\n翻页与查看\n← / →：跟随阅读方向翻页（右至左模式中 ← 是下一页）。\n↑ / ↓、PageUp / PageDown、滚轮：上一页 / 下一页。\nWebtoon 中 ↑ / ↓ 小幅滚动，PageUp / PageDown 翻一屏。\n空格 / Shift+空格：下一页 / 上一页；Home / End：首尾页。\nCtrl+滚轮、+ / -：缩放；拖动：移动图片；0：重置。\n点击画面左 / 右侧：按阅读方向翻页。\nF11 或双击画面：全屏；Esc：退出全屏，再次按返回书架。\nB：书签；Ctrl+G：跳页；1 / 2 / 3 / 4：切换阅读模式。\n\n书架\nCtrl+O：导入文件夹；Ctrl+F：搜索；F5：重新扫描。\n文件夹递归扫描；每个直接含图片的文件夹独立成册。\n文件名采用自然排序（2 在 10 前）；ZIP / CBZ 可直接阅读。\n图片拖入后打开其所在文件夹。进度、模式、书签自动保存。\nGIF / TIFF 显示第一帧；WebP 依赖系统解码器。暂不支持 PDF / RAR。"),13);t.LineHeight=23;t.TextWrapping=TextWrapping.Wrap;t.Margin=new Thickness(24);d.Content=new ScrollViewer{Content=t,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};d.ShowDialog();}
  void Keys(object s,KeyEventArgs e){
   ReadingActivity(false);
   if(e.Key==Key.Tab&&Reader.Book!=null&&uiHidden){ReadingActivity(true);e.Handled=true;return;}
   bool ctrl=(Keyboard.Modifiers&ModifierKeys.Control)!=0,shift=(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
   if(e.Key==Key.F1){Help();e.Handled=true;return;}if(e.Key==Key.F11){ToggleFull();e.Handled=true;return;}
   if(e.Key==Key.Escape){if(fullscreen)ToggleFull();else if(Reader.Book!=null)Back();else{search.Clear();Keyboard.ClearFocus();}e.Handled=true;return;}
   if(ctrl&&e.Key==Key.O){ChooseFolder();e.Handled=true;return;}
   if(ctrl&&e.Key==Key.F&&Reader.Book==null){search.Focus();search.SelectAll();e.Handled=true;return;}
   if(e.OriginalSource is TextBox||e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase)return;
   if(Reader.Book==null){if(e.Key==Key.F5){ScanAfterSources();e.Handled=true;}return;}
   if(e.OriginalSource is ComboBox || mode.IsDropDownOpen || fit.IsDropDownOpen)return;
   if(ctrl&&e.Key==Key.G){JumpDialog();e.Handled=true;return;}
   if(ctrl)return;
   switch(e.Key){
    case Key.Left:if(shift)Reader.Nudge(-1);else Reader.Turn(Reader.Progress.Mode==2?1:-1);break;
    case Key.Right:if(shift)Reader.Nudge(1);else Reader.Turn(Reader.Progress.Mode==2?-1:1);break;
    case Key.Up:if(Reader.IsToon)Reader.Scroll(-80);else Reader.Turn(-1);break;
    case Key.Down:if(Reader.IsToon)Reader.Scroll(80);else Reader.Turn(1);break;
    case Key.PageUp:Reader.Turn(-1);break;case Key.PageDown:Reader.Turn(1);break;
    case Key.Space:Reader.Turn(shift?-1:1);break;case Key.Home:Reader.Jump(0);break;case Key.End:Reader.Jump(Reader.Book.Pages.Count-1);break;
    case Key.OemOpenBrackets:Reader.Nudge(-1);break;case Key.OemCloseBrackets:Reader.Nudge(1);break;
    case Key.Add:case Key.OemPlus:Reader.SetZoom(Reader.Zoom*1.2);break;case Key.Subtract:case Key.OemMinus:Reader.SetZoom(Reader.Zoom/1.2);break;
    case Key.D0:case Key.NumPad0:Reader.Zoom=1;Reader.Fit=0;fit.SelectedIndex=0;Reader.RefreshLayout();break;
    case Key.D1:Reader.SetMode(0);break;case Key.D2:Reader.SetMode(1);break;case Key.D3:Reader.SetMode(2);break;case Key.D4:Reader.SetMode(3);break;
    case Key.B:ToggleBookmark();break;default:return;
   }e.Handled=true;
  }
 }
}
