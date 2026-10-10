using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace LeafReader {
 public sealed partial class MainWindow {
  ShareServer sharing;
  async void SharingDialog(){
   if(sharing==null)sharing=new ShareServer(ShareConfig.Load());var d=Dialog(L10n.T("局域网共享 · 只读"),760,710);d.ResizeMode=ResizeMode.CanResize;var stack=new StackPanel{Margin=new Thickness(22)};
   var status=Text("",14);stack.Children.Add(status);stack.Children.Add(Text(L10n.T("共享默认关闭。仅授权以下目录；局域网 HTTP，跨网络使用 VPN 或 HTTPS。"),12,"Muted"));
   var paths=new ListBox{Height=120,Margin=new Thickness(0,12,0,12),Background=R("Panel"),Foreground=R("Fg")};foreach(var r in sharing.Config.roots)paths.Items.Add(r.path);stack.Children.Add(paths);
   var controls=new WrapPanel();controls.Children.Add(Btn(L10n.T("添加共享目录"),()=>{if(sharing.Running){MessageBox.Show(d,L10n.T("请先停止共享再更改目录。 "));return;}using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description=L10n.T("选择允许远程只读访问的图片目录"),ShowNewFolderButton=false})if(picker.ShowDialog()==System.Windows.Forms.DialogResult.OK)try{sharing.Config.AddRoot(picker.SelectedPath);paths.Items.Add(picker.SelectedPath);}catch(Exception ex){MessageBox.Show(d,ex.Message);}}));
   controls.Children.Add(Btn(L10n.T("移除所选"),()=>{if(sharing.Running){MessageBox.Show(d,L10n.T("请先停止共享再更改目录。"));return;}string path=paths.SelectedItem as string;if(path!=null){sharing.Config.roots.RemoveAll(r=>r.path==path);sharing.Config.Save();paths.Items.Remove(path);}}));stack.Children.Add(controls);
   var portRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,12)};portRow.Children.Add(Text(L10n.T("端口  ")));var port=new TextBox{Text=sharing.Config.port.ToString(),Width=100};portRow.Children.Add(port);stack.Children.Add(portRow);
   var address=new ComboBox{MinWidth=380,HorizontalAlignment=HorizontalAlignment.Left};stack.Children.Add(address);var code=Text("",14);code.Margin=new Thickness(0,12,0,12);stack.Children.Add(code);var qr=new Image{Width=220,Height=220,HorizontalAlignment=HorizontalAlignment.Left};stack.Children.Add(qr);var qrText=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,12)};stack.Children.Add(qrText);
   Action update=()=>{status.Text=sharing.Running?L10n.T("运行中 · ")+sharing.Config.roots.Count+L10n.T(" 个授权目录"):L10n.T("共享已停止");port.IsEnabled=!sharing.Running;address.ItemsSource=sharing.Addresses();if(address.SelectedIndex<0)address.SelectedIndex=0;code.Text=L10n.T("配对码：")+sharing.PairCode+L10n.T("   ·   10 分钟有效（重新生成会撤销旧授权）");string link="leafreader://pair?address="+Uri.EscapeDataString(Convert.ToString(address.SelectedItem))+"&code="+sharing.PairCode;qrText.Text=sharing.Running?link:L10n.T("启动后显示可扫码连接信息");qr.Visibility=sharing.Running?Visibility.Visible:Visibility.Collapsed;if(sharing.Running)qr.Source=QrBitmap(link);};
   address.SelectionChanged+=(s,e)=>{if(!sharing.Running)return;string link="leafreader://pair?address="+Uri.EscapeDataString(Convert.ToString(address.SelectedItem))+"&code="+sharing.PairCode;qrText.Text=link;qr.Source=QrBitmap(link);};
   var actions=new WrapPanel();actions.Children.Add(Btn(L10n.T("开启共享"),async()=>{int value;if(!int.TryParse(port.Text,out value)){MessageBox.Show(d,L10n.T("请输入有效端口"));return;}sharing.Config.port=value;status.Text=L10n.T("正在扫描授权目录…");try{await Task.Run(()=>sharing.Start());sharing.Config.Save();update();}catch(Exception ex){status.Text=L10n.T("启动失败：")+ex.Message+L10n.T("；若端口被占用请更换端口。");}}));
   actions.Children.Add(Btn(L10n.T("停止共享"),()=>{sharing.Dispose();update();}));actions.Children.Add(Btn(L10n.T("重置配对凭据"),()=>{sharing.ResetPairing();update();}));actions.Children.Add(Btn(L10n.T("刷新共享目录"),async()=>{try{await Task.Run(()=>sharing.Refresh());update();}catch(Exception ex){status.Text=ex.Message;}}));actions.Children.Add(Btn(L10n.T("关闭窗口"),()=>d.Close()));stack.Children.Add(actions);update();d.Content=new ScrollViewer{Content=stack};d.ShowDialog();await Task.CompletedTask;
  }
  internal static BitmapSource QrBitmap(string text){using(var generator=new QRCodeGenerator())using(var data=generator.CreateQrCode(text,QRCodeGenerator.ECCLevel.M))using(var qr=new PngByteQRCode(data))using(var bytes=new MemoryStream(qr.GetGraphic(5))){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=bytes;bitmap.EndInit();bitmap.Freeze();return bitmap;}}
  async Task ConnectServer(ServerProfile profile){
   Status(L10n.T("连接中 · ")+profile.Name);try{var remote=await Task.Run(()=>new RemoteApi(profile).Books());books.RemoveAll(b=>b.Id.StartsWith("remote:"+profile.ServerId+":"));books.AddRange(remote);books=books.OrderBy(b=>b.Title,NaturalOrder.Instance).ToList();RenderShelf();Status(L10n.T("已连接 · ")+profile.Name+" · "+remote.Count+L10n.T(" 本 · ")+profile.Address);}catch(Exception ex){Status(L10n.T("连接失败 · ")+profile.Name+"："+ex.Message);throw;}
  }
  void ServersDialog(){
   var d=Dialog(L10n.T("远程服务器"),710,550);var stack=new StackPanel{Margin=new Thickness(22)};stack.Children.Add(Text(L10n.T("保存服务器，在同一套书架中阅读远程图片。"),14));var list=new ListBox{Height=140,Margin=new Thickness(0,14,0,14),Background=R("Panel"),Foreground=R("Fg")};Action rebuild=()=>{list.Items.Clear();foreach(var p in State.Servers)list.Items.Add(p.Name+" · "+p.Address);};rebuild();stack.Children.Add(list);
   stack.Children.Add(Text(L10n.T("服务地址或配对链接")));var address=new TextBox{Text="http://",Margin=new Thickness(0,8,0,10)};stack.Children.Add(address);stack.Children.Add(Text(L10n.T("电脑上显示的配对码")));var code=new TextBox{Margin=new Thickness(0,8,0,14)};stack.Children.Add(code);var result=Text("",12,"Muted");result.TextWrapping=TextWrapping.Wrap;stack.Children.Add(result);
   var controls=new WrapPanel{Margin=new Thickness(0,14,0,0)};controls.Children.Add(Btn(L10n.T("配对并连接"),async()=>{try{string endpoint=address.Text.Trim(),pairCode=code.Text.Trim();if(endpoint.StartsWith("leafreader://pair?")){var query=System.Web.HttpUtility.ParseQueryString(new Uri(endpoint).Query);endpoint=query["address"];pairCode=query["code"];}result.Text=L10n.T("正在配对…");var profile=await Task.Run(()=>RemoteApi.Pair(endpoint,pairCode));State.Servers.RemoveAll(p=>p.ServerId==profile.ServerId);State.Servers.Add(profile);Save();rebuild();await ConnectServer(profile);result.Text=L10n.T("已连接 · ")+profile.Name;}catch(Exception ex){result.Text=ex.Message;}}));
   controls.Children.Add(Btn(L10n.T("重新连接"),async()=>{int index=list.SelectedIndex;if(index<0)return;try{await ConnectServer(State.Servers[index]);result.Text=L10n.T("已重新连接");}catch(Exception ex){result.Text=ex.Message;}}));
   controls.Children.Add(Btn(L10n.T("移除服务器"),()=>{int index=list.SelectedIndex;if(index<0)return;var p=State.Servers[index];State.Servers.RemoveAt(index);books.RemoveAll(b=>b.Id.StartsWith("remote:"+p.ServerId+":"));Save();rebuild();RenderShelf();}));controls.Children.Add(Btn(L10n.T("清理远程缓存"),()=>{try{DiskCache.Clear();cards.ClearCoverCache();RenderShelf();result.Text=L10n.T("远程缓存已清理");}catch(Exception ex){result.Text=ex.Message;}}));stack.Children.Add(controls);d.Content=stack;d.ShowDialog();
  }
 }
}
