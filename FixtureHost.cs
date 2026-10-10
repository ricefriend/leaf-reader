using System;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;

namespace LeafReader {
 static class FixtureHost {
  public static async Task Run(string directory,System.Windows.Application app){
   Directory.CreateDirectory(directory);Settings.StatePath=Path.Combine(directory,"state.json");string root=Path.Combine(directory,"library");string book=Path.Combine(root,"中文 # & 漫画 2");Directory.CreateDirectory(book);
   for(int i=1;i<=3;i++)using(var image=new Bitmap(1200,i==3?24000:1800))using(var g=Graphics.FromImage(image)){g.Clear(i==3?Color.DarkSlateBlue:Color.SeaGreen);using(var font=new Font("Arial",100)){for(int y=100;y<image.Height;y+=1000)g.DrawString("LEAF "+i+" / "+y,font,Brushes.White,80,y);}image.Save(Path.Combine(book,i+".jpg"),System.Drawing.Imaging.ImageFormat.Jpeg);}
   // Real, generated covers for responsive shelf and fast-scroll device tests.
   for(int i=1;i<=32;i++){string folder=Path.Combine(root,"阅读测试 "+i.ToString("D2"));Directory.CreateDirectory(folder);using(var image=new Bitmap(600,840))using(var g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(232-(i%5)*13,222-(i%4)*12,192+(i%3)*14));using(var ink=new SolidBrush(Color.FromArgb(40+(i%4)*20,72+(i%5)*10,60+(i%3)*12))){g.FillEllipse(ink,110,150,380,380);g.FillRectangle(ink,70,620,460,12);}using(var font=new Font("Arial",44,FontStyle.Bold)){g.DrawString("VOLUME",font,Brushes.White,110,235);g.DrawString(i.ToString("D2"),font,Brushes.White,240,340);}image.Save(Path.Combine(folder,"1.jpg"),System.Drawing.Imaging.ImageFormat.Jpeg);}}
   var config=new ShareConfig{port=8876};config.AddRoot(root);using(var server=new ShareServer(config)){server.Start();File.WriteAllText(Path.Combine(directory,"fixture.json"),Wire.Json.Serialize(new{port=config.port,code=server.PairCode,serverId=config.serverId}));string link="leafreader://pair?address="+Uri.EscapeDataString("http://10.0.2.2:"+config.port)+"&code="+server.PairCode;File.WriteAllText(Path.Combine(directory,"qr-payload.txt"),link);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(MainWindow.QrBitmap(link)));using(var file=File.Create(Path.Combine(directory,"connection-qr.png")))encoder.Save(file);while(!File.Exists(Path.Combine(directory,"stop")))await Task.Delay(500);}
   app.Shutdown();
  }
 }
}
