using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LeafReader {
 public sealed class PageGrid : Window {
  readonly Book book;
  readonly CancellationTokenSource cancel=new CancellationTokenSource();
  readonly SemaphoreSlim gate=new SemaphoreSlim(3);
  public PageGrid(Book value,Action<int> chosen){
   book=value;Title=L10n.T("平铺")+" · "+book.Title;Width=1120;Height=800;MinWidth=720;MinHeight=520;
   WindowStartupLocation=WindowStartupLocation.CenterOwner;ResizeMode=ResizeMode.CanResize;
   SetResourceReference(BackgroundProperty,"Bg");SetResourceReference(ForegroundProperty,"Fg");
   FontFamily=new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");FontSize=12;
   var root=new Grid();root.SetResourceReference(Panel.BackgroundProperty,"Bg");root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   var head=new DockPanel{Margin=new Thickness(24,18,24,12)};
   var close=new Button{Content=L10n.T("关闭"),VerticalAlignment=VerticalAlignment.Top};close.Click+=(s,e)=>Close();DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
   var titles=new StackPanel();var title=Text(L10n.T("平铺"),22);title.FontWeight=FontWeights.SemiBold;titles.Children.Add(title);
   var subtitle=Text(book.Pages.Count+L10n.T(" 页")+" · "+book.Title,11.5,"Muted");subtitle.Margin=new Thickness(0,5,0,0);titles.Children.Add(subtitle);head.Children.Add(titles);
   root.Children.Add(head);
   var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(16,0,16,16)};
   var panel=new WrapPanel();for(int i=0;i<book.Pages.Count;i++)panel.Children.Add(MakeItem(i,chosen));
   scroll.Content=panel;Grid.SetRow(scroll,1);root.Children.Add(scroll);Content=root;
   Closed+=(s,e)=>cancel.Cancel();
   PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape)Close();};
  }
  TextBlock Text(string value,double size,string color="Fg"){
   var text=new TextBlock{Text=value,FontSize=size,VerticalAlignment=VerticalAlignment.Center};text.SetResourceReference(TextBlock.ForegroundProperty,color);return text;
  }
  FrameworkElement MakeItem(int index,Action<int> chosen){
   var page=book.Pages[index];
   var stack=new StackPanel{Width=156};
   var cover=new Grid{Width=144,Height=168,HorizontalAlignment=HorizontalAlignment.Center};
   var shape=new Rectangle{RadiusX=8,RadiusY=8,Stretch=Stretch.Fill};shape.SetResourceReference(Shape.FillProperty,"Card");
   var placeholder=Text(L10n.T("正在加载…"),10.5,"Muted");placeholder.HorizontalAlignment=HorizontalAlignment.Center;placeholder.VerticalAlignment=VerticalAlignment.Center;
   cover.Children.Add(shape);cover.Children.Add(placeholder);stack.Children.Add(cover);
   var label=Text((index+1)+" / "+book.Pages.Count,10.5,"Muted");label.HorizontalAlignment=HorizontalAlignment.Center;label.Margin=new Thickness(0,6,0,0);stack.Children.Add(label);
   var item=new Button{Content=stack,Width=156,Height=200,Margin=new Thickness(8),Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(1),Style=(Style)FindResource("CardButton"),ToolTip=page.Name};
   item.Click+=(s,e)=>{if(chosen!=null)chosen(index);Close();};
   LoadThumbnail(page,shape,placeholder);return item;
  }
  async void LoadThumbnail(Page page,Rectangle shape,TextBlock placeholder){
   bool acquired=false;
   try{
    await gate.WaitAsync(cancel.Token);acquired=true;
    var bitmap=await Task.Run(()=>{cancel.Token.ThrowIfCancellationRequested();page.ReadInfo();return page.Decode(240,cancel.Token);},cancel.Token);
    if(cancel.IsCancellationRequested)return;
    shape.Fill=new ImageBrush(bitmap){Stretch=Stretch.UniformToFill};placeholder.Visibility=Visibility.Collapsed;
   }catch(OperationCanceledException){}catch{placeholder.Text=L10n.T("无法读取");}
   finally{if(acquired)gate.Release();}
  }
 }
}
