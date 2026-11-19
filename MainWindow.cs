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
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LeafReader {
 public sealed partial class MainWindow:Window {
  public readonly Settings State;
  public readonly ReaderView Reader=new ReaderView();
  Grid root,body,shelf,readerHost;StackPanel shelfHead,readerHead,readerFoot;ShelfGrid cards;Grid shelfSurface;ShelfSidebar sidebar;Border sidebarEdge,sidebarHandle;FrameworkElement headerBar;
  TextBlock message,summary,bookTitle,pageLabel,zoomLabel;
  string shelfGroup;Button shelfUp,refreshButton;ToggleButton flatToggle,groupToggle;StackPanel breadcrumb;TextBlock heroEyebrow,heroTitle;Button continueButton;Book continueBook;
  bool sidebarOpen;
  DispatcherTimer sidebarHide;
  TextBox search;ComboBox mode,fit,sort;CheckBox cover,wide,penetrate;ScrollBar scroll;Slider pageSlider;Button bookmark,back,shiftBack,shiftNext,prevBook,nextBook;
  List<Book> books=new List<Book>();List<Book> readingSequence=new List<Book>();List<string> warnings=new List<string>();List<string> localWarnings=new List<string>();List<ShelfNode> tree=new List<ShelfNode>();ShelfView view;bool treeDirty=true;
  enum BookOpenPosition {Saved,Start,End}
  int operation;bool updating,busy,fullscreen;WindowState beforeFull;WindowStyle beforeStyle;ResizeMode beforeResize;
  DispatcherTimer saveTimer,idleTimer,watchTimer;bool testMode,uiHidden,progressDragging,pendingLocalRefresh,pendingFullScan,watcherDegraded,watchersEnabledInTest;string[] startup;Point? lastPointer;
  readonly List<FileSystemWatcher> watchers=new List<FileSystemWatcher>();
  DateTime lastLocalScanUtc=DateTime.MinValue;
  internal bool ReadingUiHidden {get{return uiHidden;}}
  internal bool ReadingChromeVisible {get{return readerHead!=null&&readerHead.Visibility==Visibility.Visible;}}
  internal bool ReadingProgressDragging {get{return progressDragging;}}
  internal bool ReadingIdleTimerActive {get{return idleTimer!=null&&idleTimer.IsEnabled;}}
  internal void HideReadingChromeForTest(){if(Reader.Book!=null)SetReadingUiHidden(true);}
  internal Slider PageProgressControl {get{return pageSlider;}}
  internal ComboBox ShelfSortControl {get{return sort;}}
  internal Button ShelfRefreshControl {get{return refreshButton;}}
  internal void EnableWatchersForTest(){watchersEnabledInTest=true;SyncWatchers();}
  public MainWindow(bool testing,string[] args){
   testMode=testing;startup=args;State=Settings.Load();L10n.English=(State.Language??System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)=="en";
   Title=L10n.T("栞 · Leaf Reader");FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
   Width=Math.Max(960,Math.Min(2200,State.Width));Height=Math.Max(640,Math.Min(1400,State.Height));MinWidth=960;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   SetResourceReference(BackgroundProperty,"Bg");SetResourceReference(ForegroundProperty,"Fg");
   Build();ApplyTheme();SizeChanged+=(s,e)=>UpdateResponsive();
   saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};saveTimer.Tick+=(s,e)=>{saveTimer.Stop();Save();};
   idleTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};idleTimer.Tick+=(s,e)=>HideIdleUi();
   sidebarHide=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(420)};sidebarHide.Tick+=(s,e)=>{sidebarHide.Stop();HideSidebar(false);};
   watchTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};watchTimer.Tick+=async(s,e)=>{watchTimer.Stop();await HandleWatchTick();};
   PreviewMouseMove+=PointerMoved;PreviewMouseWheel+=(s,e)=>ReadingActivity(false);
   PreviewMouseDown+=(s,e)=>ReadingActivity(false);PreviewMouseUp+=(s,e)=>ReadingActivity(false);
   Activated+=async(s,e)=>{ReadingActivity(true);if(!testMode&&Reader.Book==null&&!busy&&OwnedWindows.Count==0&&(pendingLocalRefresh||watcherDegraded)){pendingLocalRefresh=false;await RefreshLocal();}};
   Deactivated+=(s,e)=>{progressDragging=false;idleTimer.Stop();SetReadingUiHidden(false);};
   Reader.Changed=ReadingChanged;Reader.ToggleFullscreen=ToggleFull;Reader.BoundaryTurn=async direction=>{if(State.FolderPenetration!=false)await OpenAdjacentBook(direction);};
   PreviewKeyDown+=Keys;AllowDrop=true;DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
   Drop+=async(s,e)=>{if(e.Data.GetDataPresent(DataFormats.FileDrop))await Import((string[])e.Data.GetData(DataFormats.FileDrop));};
   Closing+=(s,e)=>{idleTimer.Stop();saveTimer.Stop();if(watchTimer!=null)watchTimer.Stop();ClearWatchers();if(sharing!=null)sharing.Dispose();Remember();if(!fullscreen&&WindowState==WindowState.Normal){State.Width=ActualWidth;State.Height=ActualHeight;}Save();};
   Loaded+=async(s,e)=>{UpdateResponsive();if(testMode)return;if(Settings.LoadWarning!=null)MessageBox.Show(this,Settings.LoadWarning,L10n.T("阅读记录"));if(startup.Length>0)await Import(startup);else if(State.Roots.Count>0||State.Servers.Count>0)await Scan();else SyncWatchers();};
  }
  Brush R(string key){return (Brush)FindResource(key);}
  TextBlock Text(string text,double size=13,string resource="Fg"){var t=new TextBlock{Text=text,FontSize=size,VerticalAlignment=VerticalAlignment.Center};t.SetResourceReference(TextBlock.ForegroundProperty,resource);return t;}
  Button Btn(string title,Action action,string tip=null){var b=new Button{Content=title,ToolTip=tip??title};b.Click+=(s,e)=>action();return b;}
  Button PrimaryBtn(string title,Action action,string tip=null){var b=Btn(title,action,tip);b.Style=(Style)FindResource("PrimaryButton");b.Margin=new Thickness(0,0,8,0);return b;}
  void Build(){
   root=new Grid();root.SetResourceReference(Panel.BackgroundProperty,"Bg");root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
   var header=new DockPanel{Margin=new Thickness(26,16,22,14)};
   var mark=new Border{Background=R("Accent"),CornerRadius=new CornerRadius(10),Width=34,Height=34,Margin=new Thickness(0,0,10,0),Child=new TextBlock{Text=L10n.T("栞"),FontSize=22,Foreground=new SolidColorBrush(Color.FromRgb(16,32,24)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};header.Children.Add(mark);
   var brand=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,24,0)};brand.Children.Add(Text("LEAF READER",14));brand.Children.Add(Text(L10n.T("留一页，给自己。"),9.5,"Muted"));header.Children.Add(brand);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(actions,Dock.Right);
   actions.Children.Add(PrimaryBtn(L10n.T("导入文件夹"),ChooseFolder,L10n.T("Ctrl+O · 导入母文件夹，递归扫描子文件夹")));actions.Children.Add(Btn(L10n.T("压缩包"),ChooseArchive,L10n.T("直接阅读 ZIP / CBZ")));
   actions.Children.Add(Btn(L10n.T("共享"),SharingDialog,L10n.T("选择授权目录并开启局域网共享")));actions.Children.Add(Btn(L10n.T("服务器"),ServersDialog,L10n.T("配对或重新连接另一台电脑")));actions.Children.Add(Btn(L10n.Pick("设置","Settings"),SettingsDialog));header.Children.Add(actions);
   search=new TextBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=12,Padding=new Thickness(2,0,0,0),VerticalContentAlignment=VerticalAlignment.Center,ToolTip=L10n.T("搜索书名或文件夹路径 · Ctrl+F")};
   var watermark=new TextBlock{Text=L10n.T("搜索书名或文件夹"),FontSize=12,IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};watermark.SetResourceReference(TextBlock.ForegroundProperty,"Muted");
   var searchGrid=new Grid();searchGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});searchGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});searchGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var glass=Text(L10n.T("⌕"),13,"Muted");glass.HorizontalAlignment=HorizontalAlignment.Center;searchGrid.Children.Add(glass);
   var searchHost=new Grid();searchHost.Children.Add(watermark);searchHost.Children.Add(search);Grid.SetColumn(searchHost,1);searchGrid.Children.Add(searchHost);
   var hint=Text("Ctrl F",9.5,"Muted");hint.Margin=new Thickness(8,0,10,0);Grid.SetColumn(hint,2);searchGrid.Children.Add(hint);
   var searchShell=new Border{Height=38,MinWidth=220,MaxWidth=360,Margin=new Thickness(0,0,16,0),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right,Padding=new Thickness(0),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10)};searchShell.SetResourceReference(Border.BackgroundProperty,"Card");searchShell.SetResourceReference(Border.BorderBrushProperty,"Line");searchShell.Child=searchGrid;header.Children.Add(searchShell);
   search.TextChanged+=(s,e)=>{watermark.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;RenderShelf(true);};
   root.Children.Add(header);headerBar=header;
   body=new Grid();Grid.SetRow(body,1);root.Children.Add(body);
   shelf=new Grid{Margin=new Thickness(34,0,22,0)};shelf.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});shelf.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});shelf.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.Children.Add(shelf);
   readerHost=new Grid{Visibility=Visibility.Collapsed};readerHost.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});readerHost.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});readerHost.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.Children.Add(readerHost);
   sidebar=new ShelfSidebar{HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Stretch,Visibility=Visibility.Collapsed,Opacity=0};sidebar.RenderTransform=new TranslateTransform(-248,0);sidebar.Effect=new DropShadowEffect{BlurRadius=26,ShadowDepth=0,Opacity=.42,Color=Colors.Black};sidebar.MouseEnter+=(s,e)=>{if(sidebarHide!=null)sidebarHide.Stop();};sidebar.MouseLeave+=(s,e)=>DelaySidebarHide();Panel.SetZIndex(sidebar,30);body.Children.Add(sidebar);
   sidebarEdge=new Border{Width=10,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Stretch,Background=Brushes.Transparent,ToolTip=L10n.T("目录树 · 移到左侧展开")};sidebarEdge.MouseEnter+=(s,e)=>ShowSidebar();sidebarEdge.MouseLeave+=(s,e)=>DelaySidebarHide();Panel.SetZIndex(sidebarEdge,29);body.Children.Add(sidebarEdge);
   sidebarHandle=new Border{Width=28,Height=96,CornerRadius=new CornerRadius(0,8,8,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Cursor=Cursors.Hand,ToolTip=L10n.T("目录树 · 移到左侧展开"),BorderThickness=new Thickness(0,1,1,0)};
   sidebarHandle.SetResourceReference(Border.BackgroundProperty,"Accent");sidebarHandle.SetResourceReference(Border.BorderBrushProperty,"AccentLine");
   var handleStack=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
   var handleIcon=Text("≡",17,"AccentInk");handleIcon.HorizontalAlignment=HorizontalAlignment.Center;handleStack.Children.Add(handleIcon);
   var handleLabel=Text(L10n.T("目录"),9.5,"AccentInk");handleLabel.HorizontalAlignment=HorizontalAlignment.Center;handleLabel.Margin=new Thickness(0,4,0,0);handleLabel.LayoutTransform=new RotateTransform(90);handleStack.Children.Add(handleLabel);
   sidebarHandle.Child=handleStack;sidebarHandle.MouseEnter+=(s,e)=>ShowSidebar();sidebarHandle.MouseLeftButtonUp+=(s,e)=>{ShowSidebar();e.Handled=true;};Panel.SetZIndex(sidebarHandle,28);body.Children.Add(sidebarHandle);
   sidebar.Selected=key=>{shelfGroup=key;RenderShelf(true);};sidebar.ManageSources=ManageSources;sidebar.Refresh=ScanAfterSources;sidebar.DeleteNodeRequested=async node=>await DeleteNodeTarget(node,true);treeDirty=true;
   shelfHead=new StackPanel();
   var toolbar=new Grid{Margin=new Thickness(0,0,0,10)};
   toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});toolbar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   shelfUp=Btn("‹",()=>GoUpShelf(),L10n.T("返回上一级"));shelfUp.Width=30;shelfUp.Height=30;shelfUp.Padding=new Thickness(0);shelfUp.Margin=new Thickness(0,0,8,0);shelfUp.FontSize=16;shelfUp.Visibility=Visibility.Collapsed;toolbar.Children.Add(shelfUp);
   breadcrumb=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
   var crumbScroll=new ScrollViewer{Content=breadcrumb,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(crumbScroll,1);toolbar.Children.Add(crumbScroll);
   var rightTools=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
   refreshButton=Btn(L10n.T("刷新"),ScanAfterSources,L10n.T("F5 · 重新扫描导入位置"));refreshButton.Margin=new Thickness(0,0,8,0);rightTools.Children.Add(refreshButton);
   sort=new ComboBox{Width=168,ItemsSource=new[]{L10n.T("按名称排序"),L10n.T("最近阅读"),L10n.T("未读优先"),L10n.T("创建时间 · 新的在前"),L10n.T("创建时间 · 旧的在前")},SelectedIndex=Math.Max(0,Math.Min(4,State.ShelfSort)),ToolTip=L10n.T("文件夹按其创建时间；ZIP / CBZ 按压缩包文件的创建时间")};sort.SelectionChanged+=(s,e)=>{State.ShelfSort=sort.SelectedIndex;ScheduleSave();RenderShelf(true);};rightTools.Children.Add(sort);
   var segment=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(2),Margin=new Thickness(6,0,0,0)};segment.SetResourceReference(Border.BackgroundProperty,"Card");segment.SetResourceReference(Border.BorderBrushProperty,"Line");
   var segmentRow=new StackPanel{Orientation=Orientation.Horizontal};
   flatToggle=new ToggleButton{Content=L10n.T("平铺"),IsChecked=!State.ShelfGrouped,Style=(Style)FindResource("SegmentButton")};groupToggle=new ToggleButton{Content=L10n.T("分组"),IsChecked=State.ShelfGrouped,Style=(Style)FindResource("SegmentButton")};
   flatToggle.Click+=(s,e)=>{if(!updating)SetShelfGrouped(false);};groupToggle.Click+=(s,e)=>{if(!updating)SetShelfGrouped(true);};segmentRow.Children.Add(flatToggle);segmentRow.Children.Add(groupToggle);segment.Child=segmentRow;rightTools.Children.Add(segment);
   Grid.SetColumn(rightTools,2);toolbar.Children.Add(rightTools);shelfHead.Children.Add(toolbar);
   var hero=new Grid{Margin=new Thickness(0,2,0,16)};hero.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});hero.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var heroStack=new StackPanel();heroEyebrow=Text("",10,"Muted");heroEyebrow.Margin=new Thickness(0,0,0,6);heroStack.Children.Add(heroEyebrow);
   heroTitle=Text(L10n.T("我的书架"),26);heroTitle.FontWeight=FontWeights.SemiBold;heroTitle.FontFamily=new FontFamily("Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI");heroStack.Children.Add(heroTitle);
   summary=Text(L10n.T("导入一个文件夹，开始阅读"),11.5,"Muted");summary.Margin=new Thickness(0,7,0,0);heroStack.Children.Add(summary);hero.Children.Add(heroStack);
   continueButton=new Button{Content=L10n.T("继续阅读")+"  →",Style=(Style)FindResource("PrimaryButton"),Visibility=Visibility.Collapsed,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,2)};continueButton.Click+=async(s,e)=>{if(continueBook!=null)await OpenBook(continueBook);};Grid.SetColumn(continueButton,1);hero.Children.Add(continueButton);
   shelfHead.Children.Add(hero);shelf.Children.Add(shelfHead);
   shelfSurface=new Grid();cards=new ShelfGrid();cards.BookOpened=async b=>{if(b.GroupId!=null){shelfGroup=b.GroupId;RenderShelf(true);}else await OpenBook(b);};cards.BookGridOpened=b=>ShowPageGrid(b);cards.CanDeleteBook=b=>CanDeleteShelfItem(b);cards.BookRevealRequested=b=>RevealBookTarget(b);cards.BookDeleteRequested=async b=>await DeleteBookTarget(b,true);cards.Caption=b=>{if(b.GroupId!=null)return b.GroupCount+L10n.T(" 本 · 点击进入");if(search!=null&&!string.IsNullOrEmpty(search.Text.Trim()))return (b.SourceName??"")+(string.IsNullOrEmpty(b.RelativePath)?"":" / "+b.RelativePath);Progress p;State.Books.TryGetValue(b.Id,out p);return b.PageCount+L10n.T(" 页")+(p!=null&&!string.IsNullOrEmpty(p.LastRead)?L10n.T("  ·  读至 ")+Math.Min(b.PageCount,p.Page+1):L10n.T("  ·  未读"));};shelfSurface.Children.Add(cards);Grid.SetRow(shelfSurface,1);shelf.Children.Add(shelfSurface);
   readerHead=new StackPanel{Margin=new Thickness(22,0,15,12)};var titlebar=new DockPanel();back=Btn(L10n.T("‹ 书架"),Back,L10n.T("Esc · 返回书架"));titlebar.Children.Add(back);bookTitle=Text("",18);bookTitle.TextTrimming=TextTrimming.CharacterEllipsis;bookTitle.Margin=new Thickness(12,0,0,0);titlebar.Children.Add(bookTitle);readerHead.Children.Add(titlebar);
   var options=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
   mode=new ComboBox{Width=156,ItemsSource=new[]{L10n.T("单页 · 标准看图"),L10n.T("双页 · 从左至右"),L10n.T("双页 · 从右至左"),L10n.T("Webtoon · 纵向")}};mode.SelectionChanged+=(s,e)=>{if(!updating&&Reader.Book!=null){Reader.SetMode(mode.SelectedIndex);Reader.Focus();}};options.Children.Add(mode);
   fit=new ComboBox{Width=112,ItemsSource=new[]{L10n.T("适应窗口"),L10n.T("适应宽度"),L10n.T("原始尺寸")},SelectedIndex=0};fit.SelectionChanged+=(s,e)=>{if(!updating){Reader.Fit=fit.SelectedIndex;Reader.Zoom=1;Reader.RefreshLayout();Reader.Focus();}};options.Children.Add(fit);
   cover=new CheckBox{Content=L10n.T("封面独页"),ToolTip=L10n.T("双页模式中让第一张图片单独显示")};wide=new CheckBox{Content=L10n.T("横图独页"),ToolTip=L10n.T("双页模式下，宽高比大于 1.18 的整张跨页图单独显示")};cover.Click+=(s,e)=>{if(Reader.Book!=null){Reader.Progress.Cover=cover.IsChecked==true;Reader.RefreshLayout();}};wide.Click+=(s,e)=>{if(Reader.Book!=null){Reader.Progress.Wide=wide.IsChecked==true;Reader.RefreshLayout();}};
   options.Children.Add(cover);options.Children.Add(wide);
   penetrate=new CheckBox{Content=L10n.T("文件夹穿透"),IsChecked=State.FolderPenetration!=false,ToolTip=L10n.T("看完当前文件夹后自动穿透到下一本，无需返回书架")};penetrate.Click+=(s,e)=>{State.FolderPenetration=penetrate.IsChecked==true;Save();Reader.Focus();};options.Children.Add(penetrate);
   options.Children.Add(Btn("−",()=>Reader.SetZoom(Reader.Zoom/1.2),L10n.T("缩小 · -")));zoomLabel=Text("100%");zoomLabel.Width=48;options.Children.Add(zoomLabel);options.Children.Add(Btn("+",()=>Reader.SetZoom(Reader.Zoom*1.2),L10n.T("放大 · +")));options.Children.Add(Btn(L10n.T("重置"),()=>{Reader.Zoom=1;Reader.Fit=0;fit.SelectedIndex=0;Reader.RefreshLayout();},L10n.T("0 · 重置缩放和位置")));options.Children.Add(Btn(L10n.T("全屏"),ToggleFull,L10n.T("F11 / 双击画面 · 全屏")));readerHead.Children.Add(options);readerHost.Children.Add(readerHead);
   var viewGrid=new Grid();viewGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});viewGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});viewGrid.Children.Add(Reader);scroll=new ScrollBar{Orientation=Orientation.Vertical,Width=14,SmallChange=80,Visibility=Visibility.Collapsed};scroll.ValueChanged+=(s,e)=>{if(!updating)Reader.SetOffset(scroll.Value);};Grid.SetColumn(scroll,1);viewGrid.Children.Add(scroll);Grid.SetRow(viewGrid,1);readerHost.Children.Add(viewGrid);
   readerFoot=new StackPanel{Margin=new Thickness(22,10,15,12)};var nav=new DockPanel();
   var navleft=new StackPanel{Orientation=Orientation.Horizontal};navleft.Children.Add(Btn(L10n.T("上一页"),()=>Reader.Turn(-1),L10n.T("PageUp / 滚轮向上")));navleft.Children.Add(Btn(L10n.T("下一页"),()=>Reader.Turn(1),L10n.T("PageDown / 空格 / 滚轮向下")));shiftBack=Btn(L10n.T("配对 −1"),()=>Reader.Nudge(-1),L10n.T("[ 或 Shift+← · 起始页前移一张"));shiftNext=Btn(L10n.T("配对 +1"),()=>Reader.Nudge(1),L10n.T("] 或 Shift+→ · 起始页后移一张"));navleft.Children.Add(shiftBack);navleft.Children.Add(shiftNext);prevBook=Btn(L10n.T("上一本"),async()=>await OpenAdjacentBook(-1),L10n.T("切换到上一本"));nextBook=Btn(L10n.T("下一本"),async()=>await OpenAdjacentBook(1),L10n.T("切换到下一本"));navleft.Children.Add(prevBook);navleft.Children.Add(nextBook);nav.Children.Add(navleft);
   var navright=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(navright,Dock.Right);bookmark=Btn(L10n.T("☆ 书签"),ToggleBookmark,L10n.T("B · 添加 / 取消当前页书签"));navright.Children.Add(bookmark);navright.Children.Add(Btn(L10n.T("书签列表"),Bookmarks));navright.Children.Add(Btn(L10n.T("跳页"),JumpDialog,L10n.T("Ctrl+G · 输入页码")));nav.Children.Add(navright);pageLabel=Text("");pageLabel.Margin=new Thickness(15,0,15,0);pageLabel.HorizontalAlignment=HorizontalAlignment.Center;nav.Children.Add(pageLabel);readerFoot.Children.Add(nav);
   pageSlider=new Slider{Minimum=1,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(3,11,8,0),ToolTip=L10n.T("拖动跳转页码")};pageSlider.ValueChanged+=(s,e)=>{if(!updating&&Reader.Book!=null)Reader.Jump((int)pageSlider.Value-1);};readerFoot.Children.Add(pageSlider);Grid.SetRow(readerFoot,2);readerHost.Children.Add(readerFoot);
   pageSlider.AddHandler(Thumb.DragStartedEvent,new DragStartedEventHandler((s,e)=>{progressDragging=true;ReadingActivity(false);}));pageSlider.AddHandler(Thumb.DragCompletedEvent,new DragCompletedEventHandler((s,e)=>{progressDragging=false;ReadingActivity(false);}));
   message=Text(L10n.T("支持 JPG · PNG · BMP · GIF · TIFF · ZIP / CBZ    |    可直接拖入文件夹或图片"),11,"Muted");message.Margin=new Thickness(28,5,24,10);message.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetRow(message,2);root.Children.Add(message);UpdateSegment();RenderShelf();
  }
  void ApplyTheme(){
   string[] keys={"Bg","Side","Panel","Card","Hover","Line","Fg","Muted","Accent","AccentSoft","AccentLine","AccentInk"};
   string[] values=State.Light?new[]{"#F3F2ED","#EAE7DF","#FBFAF6","#FBFAF6","#E3E0D6","#D9D5C9","#1E2A24","#61706A","#3F7D5C","#DCEBE2","#A9CDB8","#FFFFFF"}:new[]{"#0E1116","#11161D","#151B23","#151B23","#1E2732","#232C37","#E9EEF5","#93A1B5","#A8D9BC","#23342A","#3D5C49","#102018"};
   for(int i=0;i<keys.Length;i++)Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(values[i]));Reader.Light=State.Light;Reader.InvalidateVisual();UpdateSegment();RenderShelf();
  }
  internal void SetLight(bool light){State.Light=light;ApplyTheme();Save();}
  void UpdateSegment(){
   if(flatToggle==null)return;Brush soft=(Brush)FindResource("AccentSoft"),accent=(Brush)FindResource("Accent"),muted=(Brush)FindResource("Muted");
   if(State.ShelfGrouped){groupToggle.Background=soft;groupToggle.Foreground=accent;flatToggle.Background=Brushes.Transparent;flatToggle.Foreground=muted;}
   else{flatToggle.Background=soft;flatToggle.Foreground=accent;groupToggle.Background=Brushes.Transparent;groupToggle.Foreground=muted;}
  }
  string InitialBrowseFolder(){
   for(int i=State.Roots.Count-1;i>=0;i--){
    try{string parent=Path.GetDirectoryName(Path.GetFullPath(State.Roots[i]));if(!string.IsNullOrEmpty(parent)&&Directory.Exists(parent))return parent;}catch{}
   }
   return null;
  }
  async void ChooseFolder(){if(busy)return;string selected=FolderPicker.Pick(this,L10n.T("选择母文件夹：将扫描其中所有子文件夹"),InitialBrowseFolder());if(!string.IsNullOrEmpty(selected))await Import(new[]{selected});}
  async void ChooseArchive(){if(busy)return;var d=new OpenFileDialog{Filter=L10n.T("漫画压缩包|*.cbz;*.zip"),Multiselect=true};if(d.ShowDialog(this)==true)await Import(d.FileNames);}
  async Task Import(string[] paths){
   if(busy){Status(L10n.T("正在处理，请完成后再导入。"));return;}string image=null;
   foreach(string input in paths){try{string path=Path.GetFullPath(input.Trim('"'));if(File.Exists(path)&&Library.IsImage(path)){image=path;path=Path.GetDirectoryName(path);}if(!Directory.Exists(path)&&!(File.Exists(path)&&Library.IsArchive(path)))continue;if(!State.Roots.Any(p=>string.Equals(p,path,StringComparison.OrdinalIgnoreCase)))State.Roots.Add(path);}catch{}}
   Save();await Scan();if(image!=null){Book b=books.FirstOrDefault(x=>x.Pages.Any(p=>p.Path==image));if(b!=null){await OpenBook(b);Reader.Jump(b.Pages.FindIndex(p=>p.Path==image));}}
  }
  void ClearWatchers(){
   foreach(var watcher in watchers){try{watcher.EnableRaisingEvents=false;watcher.Dispose();}catch{}}
   watchers.Clear();
  }
  static bool IsIgnoredWatchPath(string path){
   if(string.IsNullOrEmpty(path))return true;
   string name=Path.GetFileName(path);
   if(string.Equals(name,"Thumbs.db",StringComparison.OrdinalIgnoreCase)||string.Equals(name,"desktop.ini",StringComparison.OrdinalIgnoreCase)||string.Equals(name,".DS_Store",StringComparison.OrdinalIgnoreCase))return true;
   string ext=Path.GetExtension(path);
   return string.Equals(ext,".tmp",StringComparison.OrdinalIgnoreCase)||string.Equals(ext,".part",StringComparison.OrdinalIgnoreCase)||string.Equals(ext,".crdownload",StringComparison.OrdinalIgnoreCase);
  }
  static bool ShouldHandleWatchPath(string path,WatcherChangeTypes changeType){
   if(IsIgnoredWatchPath(path))return false;
   if(changeType==WatcherChangeTypes.Deleted||changeType==WatcherChangeTypes.Renamed)return true;
   return Directory.Exists(path)||Library.IsImage(path)||Library.IsArchive(path);
  }
  void ScheduleWatchRefreshFromThread(){
   if(Dispatcher.HasShutdownStarted)return;
   Dispatcher.BeginInvoke((Action)(()=>{if(!Dispatcher.HasShutdownStarted&&watchTimer!=null){watchTimer.Stop();watchTimer.Start();}}));
  }
  void AttachWatcher(string directory,string filter,bool includeSubdirectories){
   var watcher=new FileSystemWatcher(directory){
    IncludeSubdirectories=includeSubdirectories,
    NotifyFilter=NotifyFilters.DirectoryName|NotifyFilters.FileName|NotifyFilters.LastWrite|NotifyFilters.CreationTime,
    InternalBufferSize=32768
   };
   if(!string.IsNullOrEmpty(filter))watcher.Filter=filter;
   watcher.Created+=(s,e)=>{if(ShouldHandleWatchPath(e.FullPath,e.ChangeType))ScheduleWatchRefreshFromThread();};
   watcher.Deleted+=(s,e)=>{if(ShouldHandleWatchPath(e.FullPath,e.ChangeType))ScheduleWatchRefreshFromThread();};
   watcher.Changed+=(s,e)=>{if(ShouldHandleWatchPath(e.FullPath,e.ChangeType))ScheduleWatchRefreshFromThread();};
   watcher.Renamed+=(s,e)=>{if(!IsIgnoredWatchPath(e.FullPath)||!IsIgnoredWatchPath(e.OldFullPath))ScheduleWatchRefreshFromThread();};
   watcher.Error+=(s,e)=>{watcherDegraded=true;ScheduleWatchRefreshFromThread();};
   watcher.EnableRaisingEvents=true;
   watchers.Add(watcher);
  }
  void SyncWatchers(){
   ClearWatchers();watcherDegraded=false;
   if(testMode&&!watchersEnabledInTest)return;
   var watchedDirectories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var watchedFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(string raw in State.Roots){
    try{
     string path=Path.GetFullPath(raw);
     if(Directory.Exists(path)){
      if(watchedDirectories.Add(path))AttachWatcher(path,null,true);
     }else if(File.Exists(path)&&Library.IsArchive(path)){
      if(watchedFiles.Add(path)){
       string parent=Path.GetDirectoryName(path);
       if(!string.IsNullOrEmpty(parent)&&Directory.Exists(parent))AttachWatcher(parent,Path.GetFileName(path),false);
       else watcherDegraded=true;
      }
     }else{
      watcherDegraded=true;
     }
    }catch{watcherDegraded=true;}
   }
  }
  static string LocalBookSignature(Book b){
   var sb=new System.Text.StringBuilder();
   sb.Append(b.Id).Append('|').Append(b.Title).Append('|').Append(b.CreatedUtc.Ticks).Append('|').Append(b.SourceKey).Append('|').Append(b.RelativePath).Append('|').Append(b.Pages.Count);
   for(int i=0;i<b.Pages.Count;i++){var p=b.Pages[i];sb.Append('|').Append(p.Path).Append(':').Append(p.Entry);}
   return sb.ToString();
  }
  static bool SameLocalBooks(IList<Book> current,IList<Book> next){
   if(current.Count!=next.Count)return false;
   for(int i=0;i<current.Count;i++)if(!string.Equals(LocalBookSignature(current[i]),LocalBookSignature(next[i]),StringComparison.Ordinal))return false;
   return true;
  }
  async Task HandleWatchTick(){
   if(Reader.Book!=null||busy||OwnedWindows.Count>0){pendingLocalRefresh=true;return;}
   await RefreshLocal();
  }
  async Task DrainPendingRefresh(){
   if(busy||Reader.Book!=null||OwnedWindows.Count>0)return;
   if(pendingFullScan){pendingFullScan=false;pendingLocalRefresh=false;await Scan();return;}
   if(pendingLocalRefresh){pendingLocalRefresh=false;await RefreshLocal();}
  }
  async Task RefreshLocal(){
   if(busy){pendingLocalRefresh=true;return;}
   busy=true;int op=++operation;var roots=State.Roots.ToArray();if(watcherDegraded)SyncWatchers();
   try{
    var result=await Task.Run(()=>Library.Scan(roots));lastLocalScanUtc=DateTime.UtcNow;if(op!=operation)return;
    var currentLocal=books.Where(b=>b.RemoteId==null).ToList();
    if(SameLocalBooks(currentLocal,result.Books)&&localWarnings.SequenceEqual(result.Warnings))return;
    books=result.Books.Concat(books.Where(b=>b.RemoteId!=null)).ToList();warnings=result.Warnings;localWarnings=new List<string>(result.Warnings);
    treeDirty=true;cards.ClearCoverCache();RenderShelf(false);
    Status(L10n.T("扫描完成 · ")+books.Count+L10n.T(" 本 · ")+books.Sum(b=>b.Pages.Count)+L10n.T(" 张图片")+(warnings.Count>0?" · "+warnings.Count+L10n.T(" 个位置未能读取，见“管理来源”"):""));
   }catch(Exception e){Status(L10n.T("扫描失败：")+e.Message);}finally{busy=false;}
   await DrainPendingRefresh();
  }
  public async Task Scan(){
   if(busy){pendingFullScan=true;return;}Remember();pendingFullScan=false;pendingLocalRefresh=false;if(Reader.Book!=null)Back();busy=true;Status(L10n.T("正在扫描文件夹…"));int op=++operation;var roots=State.Roots.ToArray();SyncWatchers();
   try{var result=await Task.Run(()=>Library.Scan(roots));lastLocalScanUtc=DateTime.UtcNow;if(op!=operation)return;books=result.Books.Concat(books.Where(b=>b.RemoteId!=null)).ToList();warnings=result.Warnings;localWarnings=new List<string>(result.Warnings);treeDirty=true;cards.ClearCoverCache();RenderShelf(true);foreach(var profile in State.Servers.ToArray())try{await ConnectServer(profile);}catch(Exception ex){warnings.Add(profile.Name+"："+ex.Message);}Status(L10n.T("扫描完成 · ")+books.Count+L10n.T(" 本 · ")+books.Sum(b=>b.Pages.Count)+L10n.T(" 张图片")+(warnings.Count>0?" · "+warnings.Count+L10n.T(" 个位置未能读取，见“管理来源”"):""));}
   catch(Exception e){Status(L10n.T("扫描失败：")+e.Message);}finally{busy=false;}
   await DrainPendingRefresh();
  }
  List<Book> FilterBooks(){return view==null?new List<Book>():view.Items;}
  internal Button ShelfUpControl {get{return shelfUp;}}
  internal List<Book> ShelfItems {get{return FilterBooks();}}
  internal int ShelfItemCount {get{return view==null?0:view.Items.Count;}}
  internal ShelfGrid ShelfControl {get{return cards;}}
  internal TextBox ShelfSearch {get{return search;}}
  internal string ShelfKey {get{return shelfGroup;}}
  internal bool ShelfCanGoUp {get{return shelfGroup!=null;}}
  internal bool ShelfContinueVisible {get{return continueButton!=null&&continueButton.Visibility==Visibility.Visible;}}
  internal string ShelfContinueId {get{return continueBook==null?null:continueBook.Id;}}
  internal int SidebarRowCount {get{return sidebar==null?0:sidebar.RowCount;}}
  internal string[] ShelfBreadcrumb {get{var list=new List<string>();list.Add(L10n.T("书架"));if(view!=null)foreach(ShelfNode node in view.Path)list.Add(node.Name);return list.ToArray();}}
  internal void SelectShelfNode(string key){if(treeDirty)RebuildTree();shelfGroup=Nearest(key);RenderShelf(true);}
  internal void GoUpShelf(){if(treeDirty)RebuildTree();List<ShelfNode> path=ShelfTree.Path(tree,shelfGroup);shelfGroup=path.Count>1?path[path.Count-2].Key:null;RenderShelf(true);}
  internal void SetShelfGrouped(bool grouped){State.ShelfGrouped=grouped;if(flatToggle!=null){flatToggle.IsChecked=!grouped;groupToggle.IsChecked=grouped;}UpdateSegment();ScheduleSave();RenderShelf(true);}
  internal bool ShelfGrouped {get{return State.ShelfGrouped;}}
  string Nearest(string key){while(!string.IsNullOrEmpty(key)){if(ShelfTree.Find(tree,key)!=null)return key;int cut=key.LastIndexOf('|');if(cut<=0)break;key=key.Substring(0,cut);}return null;}
  void RebuildTree(){tree=ShelfTree.Build(books);treeDirty=false;if(sidebar!=null)sidebar.SetTree(tree);}
  internal bool SidebarVisible {get{return SidebarAvailable;}}
  internal bool SidebarOpen {get{return sidebarOpen;}}
  internal double SidebarOffset {get{return sidebar==null?0:((TranslateTransform)sidebar.RenderTransform).X;}}
  bool SidebarAvailable {get{return sidebarEdge!=null&&ActualWidth>=1120&&(readerHost==null||readerHost.Visibility!=Visibility.Visible);}}
  internal void RevealSidebar(){ShowSidebar();}
  internal void CollapseSidebar(){HideSidebar(true);}
  void DelaySidebarHide(){if(sidebarHide!=null){sidebarHide.Stop();sidebarHide.Start();}}
  void UpdateResponsive(){
   if(sidebar==null)return;bool available=SidebarAvailable;
   if(sidebarEdge!=null)sidebarEdge.Visibility=available?Visibility.Visible:Visibility.Collapsed;
   if(!available){if(sidebarHandle!=null)sidebarHandle.Visibility=Visibility.Collapsed;HideSidebar(true);}
   else if(sidebarHandle!=null)sidebarHandle.Visibility=sidebarOpen?Visibility.Collapsed:Visibility.Visible;
  }
  void ShowSidebar(){
   if(!SidebarAvailable||sidebarOpen)return;sidebarOpen=true;if(sidebarHide!=null)sidebarHide.Stop();
   sidebar.Visibility=Visibility.Visible;sidebar.IsHitTestVisible=true;if(sidebarHandle!=null)sidebarHandle.Visibility=Visibility.Collapsed;
   var move=(TranslateTransform)sidebar.RenderTransform;sidebar.BeginAnimation(OpacityProperty,null);move.BeginAnimation(TranslateTransform.XProperty,null);double startOpacity=sidebar.Opacity,startX=move.X;
   var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
   var fade=new DoubleAnimation(startOpacity,1,TimeSpan.FromMilliseconds(150)){EasingFunction=ease};
   var slide=new DoubleAnimation(startX,0,TimeSpan.FromMilliseconds(190)){EasingFunction=ease};
   slide.Completed+=(s,e)=>{if(sidebarOpen){move.BeginAnimation(TranslateTransform.XProperty,null);move.X=0;sidebar.BeginAnimation(OpacityProperty,null);sidebar.Opacity=1;}};
   sidebar.BeginAnimation(OpacityProperty,fade);move.BeginAnimation(TranslateTransform.XProperty,slide);
  }
  void HideSidebar(bool immediate){
   sidebarOpen=false;if(sidebarHide!=null)sidebarHide.Stop();if(sidebar==null)return;
   var move=(TranslateTransform)sidebar.RenderTransform;
   if(immediate){
    move.BeginAnimation(TranslateTransform.XProperty,null);sidebar.BeginAnimation(OpacityProperty,null);move.X=-248;sidebar.Opacity=0;sidebar.Visibility=Visibility.Collapsed;sidebar.IsHitTestVisible=false;
    if(sidebarHandle!=null&&SidebarAvailable)sidebarHandle.Visibility=Visibility.Visible;return;
   }
   var ease=new CubicEase{EasingMode=EasingMode.EaseIn};
   sidebar.BeginAnimation(OpacityProperty,null);move.BeginAnimation(TranslateTransform.XProperty,null);double startOpacity=sidebar.Opacity,startX=move.X;
   var fade=new DoubleAnimation(startOpacity,0,TimeSpan.FromMilliseconds(150)){EasingFunction=ease};
   var slide=new DoubleAnimation(startX,-248,TimeSpan.FromMilliseconds(190)){EasingFunction=ease};
   slide.Completed+=(s,e)=>{
    if(!sidebarOpen){move.BeginAnimation(TranslateTransform.XProperty,null);move.X=-248;sidebar.BeginAnimation(OpacityProperty,null);sidebar.Opacity=0;sidebar.Visibility=Visibility.Collapsed;sidebar.IsHitTestVisible=false;if(sidebarHandle!=null&&SidebarAvailable)sidebarHandle.Visibility=Visibility.Visible;}
   };
   sidebar.BeginAnimation(OpacityProperty,fade);move.BeginAnimation(TranslateTransform.XProperty,slide);
  }
  void RenderShelf(bool reset=false){
   if(cards==null)return;if(treeDirty)RebuildTree();
   if(shelfGroup!=null&&ShelfTree.Find(tree,shelfGroup)==null)shelfGroup=Nearest(shelfGroup);
   view=ShelfView.Build(tree,shelfGroup,State.ShelfGrouped,search==null?"":search.Text,State.ShelfSort,State.Books);
   var selected=view.Items;if(sidebar!=null)sidebar.SetActive(shelfGroup);
   UpdateBreadcrumb();UpdateHero();
   shelfUp.Visibility=shelfGroup!=null?Visibility.Visible:Visibility.Collapsed;shelfUp.ToolTip=view.Node==null?null:view.Node.Name;
   if(shelfSurface.Children.Count>1)shelfSurface.Children.RemoveAt(1);
   cards.SetBooks(selected,reset);cards.Visibility=selected.Count==0?Visibility.Collapsed:Visibility.Visible;
   if(selected.Count==0){var empty=new StackPanel{Margin=new Thickness(10,65,10,0)};empty.Children.Add(Text(books.Count==0?L10n.T("把故事，放进书架。"):L10n.T("没有找到匹配的漫画"),30));var desc=Text(books.Count==0?L10n.T("导入母文件夹，自动发现每个子文件夹里的图片。\n第一张图成为封面，下次打开接着读。"):L10n.T("试试其他书名或文件夹关键词。"),14,"Muted");desc.LineHeight=26;desc.Margin=new Thickness(0,16,0,24);empty.Children.Add(desc);if(books.Count==0){var add=Btn(L10n.T("＋ 导入文件夹"),ChooseFolder);add.Style=(Style)FindResource("PrimaryButton");add.HorizontalAlignment=HorizontalAlignment.Left;empty.Children.Add(add);empty.Children.Add(Text(L10n.T("也支持拖放文件夹、图片，以及 ZIP / CBZ。"),12,"Muted"));}else if(search!=null&&!string.IsNullOrEmpty(search.Text)){var clear=Btn(L10n.T("清空搜索"),()=>search.Clear());clear.HorizontalAlignment=HorizontalAlignment.Left;empty.Children.Add(clear);}shelfSurface.Children.Add(empty);}
  }
  void UpdateBreadcrumb(){
   breadcrumb.Children.Clear();bool root=view==null||view.Path.Count==0;
   breadcrumb.Children.Add(Crumb(L10n.T("书架"),null,root));
   if(view!=null)for(int i=0;i<view.Path.Count;i++){
    breadcrumb.Children.Add(Separator());
    ShelfNode node=view.Path[i];breadcrumb.Children.Add(Crumb(node.Name,node.Key,i==view.Path.Count-1));
   }
  }
  TextBlock Separator(){var text=Text("›",11,"Muted");text.Margin=new Thickness(3,0,3,0);return text;}
  FrameworkElement Crumb(string name,string key,bool current){
   if(current){var label=Text(name,12);label.FontWeight=FontWeights.SemiBold;label.Margin=new Thickness(5,0,5,0);return label;}
   var button=new Button{Content=name,FontSize=11.5,Padding=new Thickness(6,3,6,3),Margin=new Thickness(0)};button.SetResourceReference(Control.ForegroundProperty,"Muted");
   button.Click+=(s,e)=>SelectShelfNode(key);return button;
  }
  void UpdateHero(){
   if(heroTitle==null)return;string query=search==null?"":search.Text.Trim();
   if(query.Length>0){heroEyebrow.Text=L10n.T("搜索");heroTitle.Text=L10n.T("搜索结果");}
   else if(view==null||view.Node==null){heroEyebrow.Text=State.ShelfGrouped?L10n.T("按来源分组"):L10n.T("全部书籍");heroTitle.Text=L10n.T("我的书架");}
   else if(view.Node.IsSource){heroEyebrow.Text=L10n.T("来源");heroTitle.Text=view.Node.Name;}
   else{heroEyebrow.Text=L10n.T("目录");heroTitle.Text=view.Node.Name;}
   summary.Text=SummaryText();continueBook=view==null?null:view.ContinueBook;continueButton.Visibility=query.Length==0&&continueBook!=null?Visibility.Visible:Visibility.Collapsed;
  }
  string SummaryText(){
   if(view==null)return L10n.T("导入一个文件夹，开始阅读");
   if(search!=null&&!string.IsNullOrEmpty(search.Text.Trim()))return view.Items.Count+L10n.T(" 个结果");
   if(shelfGroup==null)return books.Count+L10n.T(" 本藏书   /   ")+tree.Count+L10n.T(" 个来源")+(State.ShelfGrouped?"   /   "+view.Items.Count+L10n.T(" 个来源"):"");
   return view.TotalBooks+L10n.T(" 本   /   ")+view.TotalPages+L10n.T(" 页")+(view.TotalFolders>0?"   /   "+view.TotalFolders+L10n.T(" 个目录"):"");
  }
  void ShowPageGrid(Book book){
   if(book==null||book.Pages.Count<2)return;int chosen=-1;
   var dialog=new PageGrid(book,index=>chosen=index);dialog.Owner=this;dialog.ShowDialog();
   if(chosen>=0)OpenBookAt(book,chosen);
  }
  static bool PathIsSameOrUnder(string candidate,string target){
   if(string.IsNullOrEmpty(candidate)||string.IsNullOrEmpty(target))return false;
   try{
    string a=Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    string b=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    return string.Equals(a,b,StringComparison.OrdinalIgnoreCase)||a.StartsWith(b+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
   }catch{return false;}
  }
  string ResolveNodePath(ShelfNode node){
   if(node==null||string.IsNullOrEmpty(node.SourceKey)||!node.SourceKey.StartsWith("local:",StringComparison.OrdinalIgnoreCase))return null;
   string matchingRoot=State.Roots.Select(r=>{try{return Path.GetFullPath(r).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}catch{return null;}})
    .FirstOrDefault(r=>r!=null&&string.Equals("local:"+r.ToUpperInvariant(),node.SourceKey,StringComparison.OrdinalIgnoreCase));
   if(matchingRoot==null){
    var desc=ShelfTree.Descendants(node).FirstOrDefault(b=>b.RemoteId==null);
    if(desc!=null){
     string basePath=desc.Pages.Count>0&&desc.Pages[0].Entry!=null?desc.Folder:desc.Id;
     if(!string.IsNullOrEmpty(basePath)){
      if(string.IsNullOrEmpty(desc.RelativePath))return basePath;
      int up=desc.RelativePath.Split('/').Length;
      var dir=new DirectoryInfo(basePath);
      for(int i=0;i<up&&dir!=null;i++)dir=dir.Parent;
      if(dir!=null)matchingRoot=dir.FullName;
     }
    }
   }
   if(string.IsNullOrEmpty(matchingRoot))return null;
   return string.IsNullOrEmpty(node.RelativePath)?matchingRoot:Path.Combine(matchingRoot,node.RelativePath.Replace('/',Path.DirectorySeparatorChar));
  }
  bool CanDeleteShelfItem(Book book){
   if(book==null)return false;
   return book.GroupId!=null?book.GroupId.StartsWith("local:",StringComparison.OrdinalIgnoreCase):book.RemoteId==null;
  }
  bool ResolveDeleteTarget(Book book,out string path,out bool isArchive,out string label,out int count){
   path=null;isArchive=false;label="";count=1;
   if(book==null)return false;
   if(book.GroupId!=null){
    if(treeDirty)RebuildTree();
    ShelfNode node=ShelfTree.Find(tree,book.GroupId);
    path=ResolveNodePath(node);label=book.Title;count=node!=null?node.Count:book.GroupCount;
    return !string.IsNullOrEmpty(path)&&Directory.Exists(path);
   }
   if(book.RemoteId!=null)return false;
   isArchive=book.Pages.Count>0&&book.Pages[0].Entry!=null;
   path=book.Id;label=book.Title;
   return !string.IsNullOrEmpty(path)&&(isArchive?File.Exists(path):Directory.Exists(path));
  }
  void RevealBookTarget(Book book){
   string path;bool isArchive;string label;int count;
   if(!ResolveDeleteTarget(book,out path,out isArchive,out label,out count))return;
   try{
    if(isArchive)System.Diagnostics.Process.Start("explorer.exe","/select,\""+path+"\"");
    else System.Diagnostics.Process.Start("explorer.exe","\""+path+"\"");
   }catch{}
  }
  void DeletePathFromDisk(string path,bool isArchive){
   if(testMode){
    if(isArchive)File.Delete(path);
    else Directory.Delete(path,true);
    return;
   }
   if(isArchive)Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
   else Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
  }
  internal async Task<bool> DeleteBookTarget(Book book,bool confirm){
   string path;bool isArchive;string label;int count;
   if(!ResolveDeleteTarget(book,out path,out isArchive,out label,out count))return false;
   return await DeleteTargetCore(path,isArchive,label,count,confirm);
  }
  internal async Task<bool> DeleteNodeTarget(ShelfNode node,bool confirm){
   string path=ResolveNodePath(node);
   if(string.IsNullOrEmpty(path)||!Directory.Exists(path))return false;
   return await DeleteTargetCore(path,false,node.Name,node.Count,confirm);
  }
  async Task<bool> DeleteTargetCore(string path,bool isArchive,string label,int count,bool confirm){
   if(busy)return false;
   if(confirm){
    string prompt=(isArchive?L10n.T("确定要删除压缩包“"):L10n.T("确定要删除文件夹“"))+label+(count>1?L10n.T("（内含 ")+count+L10n.T(" 本）"):"")+L10n.T("”吗？\n\n路径：")+path+L10n.T("\n\n将移入系统回收站。");
    if(MessageBox.Show(this,prompt,L10n.T("确认删除"),MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return false;
   }
   busy=true;
   try{
    await Task.Run(()=>DeletePathFromDisk(path,isArchive));
    bool rootsChanged=State.Roots.RemoveAll(r=>PathIsSameOrUnder(r,path))>0;
    if(rootsChanged)Save();
    SyncWatchers();
    books.RemoveAll(b=>b.RemoteId==null&&(PathIsSameOrUnder(b.Id,path)||PathIsSameOrUnder(b.Folder,path)));
    treeDirty=true;cards.ClearCoverCache();RenderShelf(false);
    Status(L10n.T("已删除：")+label);
    return true;
   }catch(Exception ex){
    Status(L10n.T("删除失败：")+ex.Message);
    return false;
   }finally{busy=false;}
  }
  void CaptureReadingSequence(Book book){
   if(treeDirty)RebuildTree();
   bool flatOrSearch=!State.ShelfGrouped||(search!=null&&!string.IsNullOrEmpty(search.Text.Trim()));
   List<Book> seq;
   if(flatOrSearch&&view!=null){
    seq=view.Items.Where(b=>b.GroupId==null).ToList();
   }else if(State.ShelfGrouped){
    ShelfNode scope=null;
    if(!string.IsNullOrEmpty(shelfGroup)){
     var path=ShelfTree.Path(tree,shelfGroup);
     if(path.Count>0)scope=path[0];
    }
    if(scope==null&&book!=null&&!string.IsNullOrEmpty(book.SourceKey))scope=tree.FirstOrDefault(r=>string.Equals(r.SourceKey,book.SourceKey,StringComparison.OrdinalIgnoreCase));
    seq=ShelfView.OrderedBooks(tree,scope,State.ShelfSort,State.Books);
   }else{
    seq=ShelfView.OrderedBooks(tree,null,State.ShelfSort,State.Books);
   }
   if(book!=null&&!seq.Any(b=>b.Id==book.Id))seq.Insert(0,book);
   readingSequence=seq;
  }
  void UpdateReadingBookNav(){
   if(prevBook==null||nextBook==null)return;
   if(Reader.Book==null){prevBook.IsEnabled=nextBook.IsEnabled=false;return;}
   int idx=readingSequence.FindIndex(b=>b.Id==Reader.Book.Id);
   bool hasPrev=idx>0,hasNext=idx>=0&&idx+1<readingSequence.Count;
   prevBook.IsEnabled=hasPrev;nextBook.IsEnabled=hasNext;
   prevBook.ToolTip=hasPrev?L10n.T("上一本")+" · "+readingSequence[idx-1].Title:L10n.T("已经是第一本");
   nextBook.ToolTip=hasNext?L10n.T("下一本")+" · "+readingSequence[idx+1].Title:L10n.T("已经是最后一本");
  }
  internal async Task OpenAdjacentBook(int direction){
   if(busy||Reader.Book==null)return;
   if(pendingLocalRefresh&&State.Roots.Count>0){
    pendingLocalRefresh=false;
    try{
     var roots=State.Roots.ToArray();
     var res=await Task.Run(()=>Library.Scan(roots));
     books=res.Books.Concat(books.Where(b=>b.RemoteId!=null)).ToList();
     warnings=res.Warnings;localWarnings=new List<string>(res.Warnings);
     treeDirty=true;RebuildTree();
     string currentId=Reader.Book.Id;
     CaptureReadingSequence(books.FirstOrDefault(b=>b.Id==currentId)??Reader.Book);
    }catch{}
   }
   int idx=readingSequence.FindIndex(b=>b.Id==Reader.Book.Id);
   int targetIdx=idx+direction;
   if(idx<0||targetIdx<0||targetIdx>=readingSequence.Count){
    Status(direction>0?L10n.T("已经是最后一本"):L10n.T("已经是第一本"));
    return;
   }
   Book target=readingSequence[targetIdx];
   await OpenBook(target,direction>0?BookOpenPosition.Start:BookOpenPosition.End,false);
   if(Reader.Book==target)Status((direction>0?L10n.T("已穿透至下一本："):L10n.T("已穿透至上一本："))+target.Title);
  }
  async void OpenBookAt(Book book,int page){await OpenBook(book);if(Reader.Book==book)Reader.Jump(Math.Max(0,Math.Min(book.Pages.Count-1,page)));}
  public Task OpenBook(Book book){return OpenBook(book,BookOpenPosition.Saved,true);}
  async Task OpenBook(Book book,BookOpenPosition openPos,bool captureSequence){
   if(busy)return;if(captureSequence)CaptureReadingSequence(book);busy=true;Status(L10n.T("正在读取 “")+book.Title+L10n.T("” 的图片尺寸…"));int op=++operation;
   try{await Task.Run(()=>{if(book.RemoteId!=null){var remoteSource=(RemoteImageSource)book.Pages[0].Source;var profile=State.Servers.First(p=>p.ServerId==remoteSource.Id);new RemoteApi(profile).LoadPages(book);}foreach(var page in book.Pages)page.ReadInfo();});if(op!=operation)return;Remember();var progress=State.Get(book);
    if(openPos==BookOpenPosition.Start){progress.Page=0;progress.PairAnchor=0;progress.Fraction=0;}
    else if(openPos==BookOpenPosition.End){
     if(progress.Mode==3){progress.Page=Math.Max(0,book.Pages.Count-1);progress.Fraction=1;}
     else{int last=0;while(true){int next=Paging.Next(book,last,progress.Mode,progress.Cover,progress.Wide,progress.PairAnchor);if(next==last)break;last=next;}progress.Page=last;progress.Fraction=0;}
    }
    shelf.Visibility=Visibility.Collapsed;readerHost.Visibility=Visibility.Visible;UpdateResponsive();bookTitle.Text=book.Title+" · "+(book.SourceLabel??L10n.T("本地"));bookTitle.ToolTip=book.Id;Reader.Open(book,progress);if(openPos==BookOpenPosition.End&&Reader.IsToon)Reader.SetOffset(double.MaxValue);Reader.Focus();UpdateReadingBookNav();ReadingActivity(true);Status(L10n.T("闲置 3 秒隐藏工具栏 · 鼠标移至上下边缘或按 Tab 显示 · F11 全屏"));if(book.Pages.Any(p=>p.Error!=null))Status(L10n.T("部分图片无法解码，可继续翻页；WebP 取决于 Windows 图像解码器。"));}catch(Exception e){Status(L10n.T("打开失败：")+e.Message);}finally{busy=false;}
   await DrainPendingRefresh();
  }
  public void SetTestBooks(List<Book> items){books=items;treeDirty=true;RenderShelf();}
  void Remember(){if(Reader.Book==null)return;Reader.Progress.LastRead=DateTime.UtcNow.ToString("o");Reader.Progress.PageId=Reader.Book.Pages[Reader.Progress.Page].Identity;Reader.Progress.PairAnchorId=Reader.Book.Pages[Math.Max(0,Math.Min(Reader.Book.Pages.Count-1,Reader.Progress.PairAnchor))].Identity;Reader.Progress.BookmarkIds=Reader.Progress.Bookmarks.Where(i=>i>=0&&i<Reader.Book.Pages.Count).Select(i=>Reader.Book.Pages[i].Identity).ToList();State.Books[Reader.Book.Id]=Reader.Progress;}
  void ReadingChanged(){
   if(Reader.Book==null)return;updating=true;var p=Reader.Progress;mode.SelectedIndex=p.Mode;fit.SelectedIndex=Reader.Fit;cover.IsChecked=p.Cover;wide.IsChecked=p.Wide;if(penetrate!=null)penetrate.IsChecked=State.FolderPenetration!=false;bool spread=p.Mode==1||p.Mode==2;cover.IsEnabled=wide.IsEnabled=shiftBack.IsEnabled=shiftNext.IsEnabled=spread;fit.IsEnabled=!Reader.IsToon;UpdateReadingBookNav();
   int n=Reader.VisibleCount;pageLabel.Text=(p.Page+1)+(n==2?"–"+(p.Page+2):"")+" / "+Reader.Book.Pages.Count;zoomLabel.Text=Math.Round(Reader.Zoom*100)+"%";pageSlider.IsDirectionReversed=p.Mode==2;pageSlider.ToolTip=p.Mode==2?L10n.T("右端为第 1 页，向左推进 · 拖动跳页"):L10n.T("左端为第 1 页，向右推进 · 拖动跳页");pageSlider.Maximum=Reader.Book.Pages.Count;pageSlider.Value=p.Page+1;bookmark.Content=p.Bookmarks.Contains(p.Page)?L10n.T("★ 已标记"):L10n.T("☆ 书签");
   scroll.Visibility=Reader.IsToon&&!uiHidden?Visibility.Visible:Visibility.Collapsed;scroll.Maximum=Math.Max(0,Reader.Extent-Reader.ActualHeight);scroll.ViewportSize=Reader.ActualHeight;scroll.LargeChange=Math.Max(80,Reader.ActualHeight*.85);scroll.Value=Reader.Offset;updating=false;Remember();ScheduleSave();
  }
  void ScheduleSave(){if(saveTimer==null)return;saveTimer.Stop();saveTimer.Start();}
  void Save(){string error=State.Save();if(error!=null)Status(error);}
  void Status(string text){message.Text=text;message.ToolTip=text;}
  void Back(){if(busy)return;if(fullscreen)ToggleFull();idleTimer.Stop();SetReadingUiHidden(false);Remember();Save();Reader.CloseBook();readerHost.Visibility=Visibility.Collapsed;shelf.Visibility=Visibility.Visible;UpdateResponsive();RenderShelf();Status(L10n.T("书架已更新 · 阅读进度自动保存"));if(pendingLocalRefresh||pendingFullScan){var _=DrainPendingRefresh();}}
  void ToggleFull(){if(Reader.Book==null)return;fullscreen=!fullscreen;if(fullscreen){beforeFull=WindowState;beforeStyle=WindowStyle;beforeResize=ResizeMode;WindowState=WindowState.Normal;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowState=WindowState.Maximized;}else{WindowState=WindowState.Normal;WindowStyle=beforeStyle;ResizeMode=beforeResize;WindowState=beforeFull;}SetReadingUiHidden(fullscreen);ReadingActivity(false);Reader.Focus();}
  void AnimateChrome(FrameworkElement element,bool show,double offset,int milliseconds){
   if(element==null)return;
   var move=element.RenderTransform as TranslateTransform;
   if(move==null){move=new TranslateTransform(0,0);element.RenderTransform=move;}
   element.BeginAnimation(UIElement.OpacityProperty,null);move.BeginAnimation(TranslateTransform.YProperty,null);
   if(show){
    element.Visibility=Visibility.Visible;element.Opacity=0;move.Y=offset;
    var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
    var fade=new DoubleAnimation(1,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=ease};
    var slide=new DoubleAnimation(0,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=ease};
    slide.Completed+=(s,e)=>{if(!uiHidden){element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;move.BeginAnimation(TranslateTransform.YProperty,null);move.Y=0;}};
    element.BeginAnimation(UIElement.OpacityProperty,fade);move.BeginAnimation(TranslateTransform.YProperty,slide);
   }else{
    element.Opacity=1;move.Y=0;
    var ease=new CubicEase{EasingMode=EasingMode.EaseIn};
    var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=ease};
    var slide=new DoubleAnimation(offset,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=ease};
    slide.Completed+=(s,e)=>{if(uiHidden){element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=0;move.BeginAnimation(TranslateTransform.YProperty,null);move.Y=0;element.Visibility=Visibility.Collapsed;}};
    element.BeginAnimation(UIElement.OpacityProperty,fade);move.BeginAnimation(TranslateTransform.YProperty,slide);
   }
  }
  void SetReadingUiHidden(bool hidden){
   hidden=hidden&&Reader.Book!=null;if(uiHidden==hidden)return;uiHidden=hidden;
   if(hidden){
    readerHead.IsHitTestVisible=false;readerFoot.IsHitTestVisible=false;scroll.IsHitTestVisible=false;
    AnimateChrome(headerBar,false,-8,170);AnimateChrome(readerHead,false,-8,170);AnimateChrome(readerFoot,false,8,190);AnimateChrome(message,false,8,150);AnimateChrome(scroll,false,0,150);
    Reader.Focus();
   }else{
    readerHead.IsHitTestVisible=true;readerFoot.IsHitTestVisible=true;scroll.IsHitTestVisible=true;
    AnimateChrome(headerBar,true,-8,190);AnimateChrome(readerHead,true,-8,190);AnimateChrome(readerFoot,true,8,200);AnimateChrome(message,true,8,170);
    if(Reader.IsToon)AnimateChrome(scroll,true,0,170);else scroll.Visibility=Visibility.Collapsed;
   }
  }
  internal void ReadingActivity(bool reveal){
   if(Reader.Book==null||idleTimer==null)return;if(reveal)SetReadingUiHidden(false);
   idleTimer.Stop();if(!uiHidden)idleTimer.Start();
  }
  bool UsingReadingControls(){return progressDragging||mode.IsDropDownOpen||fit.IsDropDownOpen||Mouse.Captured!=null||readerHead.IsMouseOver||readerFoot.IsMouseOver||headerBar.IsMouseOver||scroll.IsMouseOver;}
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
   if(e.Key==Key.Escape){if(fullscreen)ToggleFull();else if(Reader.Book!=null)Back();else if(shelfGroup!=null)GoUpShelf();else{search.Clear();Keyboard.ClearFocus();}e.Handled=true;return;}
   if(e.Key==Key.Back&&Reader.Book==null&&shelfGroup!=null){GoUpShelf();e.Handled=true;return;}
   if(ctrl&&e.Key==Key.O){ChooseFolder();e.Handled=true;return;}
   if(ctrl&&e.Key==Key.F&&Reader.Book==null){search.Focus();search.SelectAll();e.Handled=true;return;}
   if(Reader.Book==null&&e.Key==Key.F5){ScanAfterSources();e.Handled=true;return;}
   if(e.OriginalSource is TextBox||e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase)return;
   if(Reader.Book==null)return;
   if(e.OriginalSource is ComboBox || mode.IsDropDownOpen || fit.IsDropDownOpen)return;
   if(ctrl&&e.Key==Key.G){JumpDialog();e.Handled=true;return;}
   if(ctrl)return;
   switch(e.Key){
    case Key.Left:if(shift)Reader.Nudge(-1);else Reader.Turn(Reader.Progress.Mode==2?1:-1);break;
    case Key.Right:if(shift)Reader.Nudge(1);else Reader.Turn(Reader.Progress.Mode==2?-1:1);break;
    case Key.Up:Reader.ScrollStep(-80);break;
    case Key.Down:Reader.ScrollStep(80);break;
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
