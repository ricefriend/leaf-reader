package org.leafreader;

import android.content.Context;
import android.net.Uri;
import android.graphics.*;
import android.media.ExifInterface;
import android.util.LruCache;
import java.io.*;
import java.net.HttpURLConnection;
import java.util.*;
import java.util.concurrent.*;
import java.util.zip.*;
import static org.leafreader.Models.*;

final class Images {
 interface Source {InputStream open(Page page,String variant,int width,Api.Request request)throws Exception;}
 final Context context;final File directory;final ExecutorService executor=Executors.newFixedThreadPool(2);
 final LruCache<String,Bitmap> memory=new LruCache<String,Bitmap>(48*1024*1024){protected int sizeOf(String k,Bitmap b){return b.getAllocationByteCount();}};
 final Object diskGate=new Object();
 Images(Context c){context=c;directory=new File(c.getCacheDir(),"images");directory.mkdirs();trim();}
 Source source(Page p){return p.server==null?this::localOpen:this::remoteOpen;}
 InputStream remoteOpen(Page p,String variant,int width,Api.Request request)throws Exception{HttpURLConnection c=Api.connect(p.server,Api.imagePath(p,variant,width),null,request);return new FilterInputStream(c.getInputStream()){public void close()throws IOException{try{super.close();}finally{c.disconnect();request.connection=null;}}};}
 InputStream localOpen(Page p,String variant,int width,Api.Request request)throws Exception{
  request.check();if(p.entry==null){InputStream input=context.getContentResolver().openInputStream(Uri.parse(p.uri));if(input==null)throw new IOException(L10n.t("文件授权失效，请重新选择目录"));return input;}
  Page archive=new Page();archive.id=p.archive;archive.uri=p.uri;archive.version=p.version;File file=original(archive,request);final ZipFile zip=new ZipFile(file);ZipEntry entry=zip.getEntry(p.entry);if(entry==null){zip.close();throw new IOException(L10n.t("压缩包中的图片已删除"));}if(entry.getSize()>200L*1024*1024){zip.close();throw new IOException(L10n.t("压缩包单张图片超过 200 MB"));}return new FilterInputStream(zip.getInputStream(entry)){public void close()throws IOException{try{super.close();}finally{zip.close();}}};
 }
 File file(Page p,String variant,int width,Api.Request request)throws Exception{
  File target=new File(directory,Api.hash(p.key()+"|"+variant+"|"+width)+".img");synchronized(diskGate){if(target.exists()){target.setLastModified(System.currentTimeMillis());return target;}}
  File temp=File.createTempFile("fetch-",".tmp",directory);
  try{try(InputStream in=source(p).open(p,variant,width,request);OutputStream out=new FileOutputStream(temp)){Api.copy(in,out,variant.equals("original")?200L*1024*1024:32L*1024*1024,request);}request.check();synchronized(diskGate){if(target.exists())temp.delete();else if(!temp.renameTo(target))throw new IOException(L10n.t("缓存写入失败"));trim();}return target;}finally{temp.delete();}
 }
 File original(Page p,Api.Request request)throws Exception{return file(p,"original",0,request);}
 void trim(){synchronized(diskGate){File[] files=directory.listFiles((d,n)->n.endsWith(".img"));if(files==null)return;Arrays.sort(files,(a,b)->Long.compare(b.lastModified(),a.lastModified()));long total=0;for(File f:files){total+=f.length();if(total>256L*1024*1024)f.delete();}}}
 void clear(){memory.evictAll();synchronized(diskGate){File[] files=directory.listFiles((d,n)->n.endsWith(".img"));if(files!=null)for(File f:files)f.delete();}}
 void info(Page p,Api.Request request)throws Exception{
  if(p.metadata)return;BitmapFactory.Options options=new BitmapFactory.Options();options.inJustDecodeBounds=true;try(InputStream in=localOpen(p,"original",0,request)){BitmapFactory.decodeStream(in,null,options);}if(options.outWidth<1)throw new IOException(L10n.t("不支持的图片格式：")+p.name+L10n.t("（安卓本地 TIFF 不受支持）"));int orientation=orientation(p,request);p.width=orientation>=5&&orientation<=8?options.outHeight:options.outWidth;p.height=orientation>=5&&orientation<=8?options.outWidth:options.outHeight;p.metadata=true;
 }
 int orientation(Page p,Api.Request request){try(InputStream in=localOpen(p,"original",0,request)){return new ExifInterface(in).getAttributeInt(ExifInterface.TAG_ORIENTATION,1);}catch(Exception e){return 1;}}
 Bitmap preview(Page p,int width,Api.Request request)throws Exception{
  width=Math.min(2048,Math.max(128,width));String variant=width<=512?"thumbnail":"preview";String key=p.key()+"|"+variant+"|"+width;Bitmap hit=memory.get(key);if(hit!=null)return hit;request.check();BitmapFactory.Options opts=new BitmapFactory.Options();opts.inJustDecodeBounds=true;File file=p.server==null?null:file(p,variant,width,request);
  try(InputStream in=file==null?localOpen(p,variant,width,request):new FileInputStream(file)){BitmapFactory.decodeStream(in,null,opts);}if(opts.outWidth<1||opts.outHeight<1)throw new IOException(L10n.t("图片无法解码：")+p.name);
  int rw=opts.outWidth,rh=opts.outHeight,orientation=p.server==null?orientation(p,request):1;int sample=1;int budget=width<=512?300000:2000000;while((long)(rw/sample)*(rh/sample)>budget||rw/sample>width*2)sample*=2;
  opts=new BitmapFactory.Options();opts.inSampleSize=sample;opts.inPreferredConfig=Bitmap.Config.ARGB_8888;Bitmap decoded;try(InputStream in=file==null?localOpen(p,variant,width,request):new FileInputStream(file)){decoded=BitmapFactory.decodeStream(in,null,opts);}if(decoded==null)throw new IOException(L10n.t("图片无法解码：")+p.name);request.check();if(orientation!=1){Matrix m=orientationMatrix(orientation,decoded.getWidth(),decoded.getHeight());Bitmap oriented=Bitmap.createBitmap(decoded,0,0,decoded.getWidth(),decoded.getHeight(),m,true);if(oriented!=decoded)decoded.recycle();decoded=oriented;}request.check();memory.put(key,decoded);return decoded;
 }
 static Matrix orientationMatrix(int o,int w,int h){float[] v;switch(o){case 2:v=new float[]{-1,0,w,0,1,0,0,0,1};break;case 3:v=new float[]{-1,0,w,0,-1,h,0,0,1};break;case 4:v=new float[]{1,0,0,0,-1,h,0,0,1};break;case 5:v=new float[]{0,1,0,1,0,0,0,0,1};break;case 6:v=new float[]{0,-1,h,1,0,0,0,0,1};break;case 7:v=new float[]{0,-1,h,-1,0,w,0,0,1};break;case 8:v=new float[]{0,1,0,-1,0,w,0,0,1};break;default:v=new float[]{1,0,0,0,1,0,0,0,1};}Matrix m=new Matrix();m.setValues(v);return m;}
 static class Tile {Bitmap bitmap;Rect raw;int rawWidth,rawHeight,orientation;RectF destination;}
 Tile tile(Page p,RectF destination,int viewWidth,int viewHeight,Api.Request request)throws Exception{
  File file=original(p,request);request.check();BitmapRegionDecoder decoder=BitmapRegionDecoder.newInstance(file.toString(),false);
  try{int rw=decoder.getWidth(),rh=decoder.getHeight();int o=1;try{o=new ExifInterface(file.toString()).getAttributeInt(ExifInterface.TAG_ORIENTATION,1);}catch(Exception ignored){}boolean swap=o>=5&&o<=8;float ow=swap?rh:rw,oh=swap?rw:rh;Matrix transform=orientationMatrix(o,rw,rh);transform.postScale(destination.width()/ow,destination.height()/oh);transform.postTranslate(destination.left,destination.top);Matrix inverse=new Matrix();if(!transform.invert(inverse))return null;RectF raw=new RectF(0,0,viewWidth,viewHeight);inverse.mapRect(raw);Rect region=new Rect(Math.max(0,(int)Math.floor(raw.left)),Math.max(0,(int)Math.floor(raw.top)),Math.min(rw,(int)Math.ceil(raw.right)),Math.min(rh,(int)Math.ceil(raw.bottom)));if(region.width()<1||region.height()<1)return null;int sample=1;while((long)(region.width()/sample)*(region.height()/sample)>1500000||region.width()/sample>viewWidth*2||region.height()/sample>viewHeight*2)sample*=2;BitmapFactory.Options options=new BitmapFactory.Options();options.inSampleSize=sample;options.inPreferredConfig=Bitmap.Config.ARGB_8888;Bitmap bitmap=decoder.decodeRegion(region,options);request.check();Tile tile=new Tile();tile.bitmap=bitmap;tile.raw=region;tile.rawWidth=rw;tile.rawHeight=rh;tile.orientation=o;tile.destination=new RectF(destination);return tile;}finally{decoder.recycle();}
 }
 void close(){executor.shutdownNow();memory.evictAll();}
}
