package org.leafreader;

import android.net.Uri;
import org.json.*;
import java.io.*;
import java.net.*;
import java.security.*;
import java.util.*;
import static org.leafreader.Models.*;

final class Api {
 static class Failure extends IOException {final String code;final int status;Failure(int s,String c,String m){super(m);code=c;status=s;}}
 static final class Request {volatile boolean cancelled;volatile HttpURLConnection connection;
  void check()throws IOException{if(cancelled||Thread.currentThread().isInterrupted())throw new InterruptedIOException(L10n.t("请求已取消"));}
  void bind(HttpURLConnection c)throws IOException{synchronized(this){check();connection=c;}}
  void release(HttpURLConnection c){synchronized(this){if(connection==c)connection=null;}}
  void cancel(){HttpURLConnection c;synchronized(this){cancelled=true;c=connection;}if(c!=null)c.disconnect();}
 }
 static String hash(String value){try{byte[] bytes=MessageDigest.getInstance("SHA-256").digest(value.getBytes("UTF-8"));StringBuilder out=new StringBuilder();for(byte b:bytes)out.append(String.format(Locale.ROOT,"%02x",b&255));return out.toString();}catch(Exception e){throw new IllegalStateException(e);}}
 static boolean privateIp(InetAddress ip){if(ip.isLoopbackAddress()||ip.isSiteLocalAddress()||ip.isLinkLocalAddress())return true;byte[] b=ip.getAddress();return b.length==4&&(b[0]&255)==100&&(b[1]&255)>=64&&(b[1]&255)<=127||b.length==16&&(b[0]&0xfe)==0xfc;}
 static String address(String value)throws IOException{try{URI u=new URI(value.trim());if(!("http".equals(u.getScheme())||"https".equals(u.getScheme()))||u.getHost()==null||u.getUserInfo()!=null||u.getQuery()!=null||u.getFragment()!=null||!(u.getPath().isEmpty()||u.getPath().equals("/")))throw new IOException(L10n.t("请输入完整服务地址，例如 http://192.168.1.10:8765"));if("http".equals(u.getScheme()))for(InetAddress ip:InetAddress.getAllByName(u.getHost()))if(!privateIp(ip))throw new IOException(L10n.t("HTTP 仅允许局域网或私有 VPN 地址；跨网络使用 HTTPS"));return u.getScheme()+"://"+u.getRawAuthority();}catch(URISyntaxException ex){throw new IOException(L10n.t("服务地址格式错误"),ex);}}
 static HttpURLConnection connect(Server s,String path,String body,Request request)throws Exception{return connect(s,path,body,request,15000);}
 static HttpURLConnection connect(Server s,String path,String body,Request request,int readTimeout)throws Exception{
  request.check();HttpURLConnection c=(HttpURLConnection)new URL(address(s.address)+path).openConnection(Proxy.NO_PROXY);try{request.bind(c);}catch(IOException ex){c.disconnect();throw ex;}try{c.setConnectTimeout(12000);c.setReadTimeout(readTimeout);c.setInstanceFollowRedirects(false);c.setRequestProperty("Connection","close");if(s.token!=null&&!s.token.isEmpty())c.setRequestProperty("Authorization","Bearer "+s.token);
  if(body!=null){c.setRequestMethod("POST");c.setDoOutput(true);c.setRequestProperty("Content-Type","application/json");byte[] bytes=body.getBytes("UTF-8");c.setFixedLengthStreamingMode(bytes.length);try(OutputStream out=c.getOutputStream()){out.write(bytes);}}
  int status=c.getResponseCode();if(status!=200){String text="";InputStream input=c.getErrorStream();if(input!=null)try(InputStream in=input;ByteArrayOutputStream out=new ByteArrayOutputStream()){copy(in,out,32768,request);text=out.toString("UTF-8");}c.disconnect();try{JSONObject error=new JSONObject(text).getJSONObject("error");throw new Failure(status,error.getString("code"),L10n.t(error.getString("message")));}catch(JSONException ex){throw new Failure(status,"HTTP_ERROR",L10n.t("连接失败，HTTP ")+status+L10n.t("；请检查服务地址或重新配对"));}}
  request.check();return c;}catch(Exception ex){c.disconnect();request.release(c);throw ex;}
 }
 static void copy(InputStream input,OutputStream output,long limit,Request request)throws IOException{byte[] buffer=new byte[65536];long total=0;int n;while((n=input.read(buffer))>=0){request.check();if(n==0)continue;total+=n;if(total>limit)throw new IOException(L10n.t("单张文件超出缓存上限，请使用兼容预览"));output.write(buffer,0,n);}}
 static JSONObject json(Server s,String path,String body,Request request)throws Exception{return json(s,path,body,request,15000);}
 static JSONObject json(Server s,String path,String body,Request request,int timeout)throws Exception{HttpURLConnection c=connect(s,path,body,request,timeout);try(InputStream in=c.getInputStream();ByteArrayOutputStream out=new ByteArrayOutputStream()){copy(in,out,16*1024*1024,request);return new JSONObject(out.toString("UTF-8"));}finally{c.disconnect();request.release(c);}}
 static Server pair(String address,String code,Request request)throws Exception{Server s=new Server();s.address=address(address);JSONObject info=json(s,"/v1/info",null,request);if(info.getInt("protocol")!=1)throw new IOException(L10n.t("服务器协议版本不兼容"));JSONObject result=json(s,"/v1/pair",new JSONObject().put("code",code).put("client","Android Leaf Reader").toString(),request);s.id=result.getString("serverId");s.name=info.getString("name");s.token=result.getString("token");return s;}
 static Page page(Server s,JSONObject o)throws JSONException{Page p=new Page();p.server=s;p.id=o.getString("id");p.name=o.getString("name");p.version=o.getString("version");p.width=Math.max(1,o.getInt("width"));p.height=Math.max(1,o.getInt("height"));p.metadata=true;return p;}
 static List<Book> books(Server server,Request request)throws Exception{JSONObject info=json(server,"/v1/info",null,request);if(!server.id.equals(info.getString("serverId")))throw new IOException(L10n.t("服务器身份改变，请重新配对"));JSONArray array=json(server,"/v1/books",null,request).getJSONArray("books");List<Book> books=new ArrayList<>();for(int i=0;i<array.length();i++){JSONObject o=array.getJSONObject(i);Book b=new Book();b.id="remote:"+server.id+":"+o.getString("id");b.name=o.getString("name");b.rootId=o.getString("rootId");b.rootName=o.optString("rootName");b.source=L10n.t("远程 · ")+server.name+" / "+o.optString("rootName");b.server=server;b.count=o.getInt("pageCount");b.created=(Long.parseLong(o.getString("createdTicks"))-621355968000000000L)/10000;b.pages.add(page(server,o.getJSONObject("cover")));books.add(b);}books.sort((a,b)->NATURAL.compare(a.name,b.name));return books;}
 static void loadPages(Book b,Request request)throws Exception{if(b.server==null)return;String id=b.id.substring(b.id.lastIndexOf(':')+1);JSONArray files=null;for(int attempt=0;attempt<3;attempt++){try{files=json(b.server,"/v1/books/"+Uri.encode(id)+"/pages",null,request,60000).getJSONArray("files");break;}catch(Exception ex){request.check();boolean retry=ex instanceof Failure?"SERVER_BUSY".equals(((Failure)ex).code):ex instanceof SocketException||ex instanceof EOFException;if(!retry||attempt==2)throw ex;Thread.sleep(250L*(attempt+1));request.check();}}List<Page> pages=new ArrayList<>();for(int i=0;i<files.length();i++)pages.add(page(b.server,files.getJSONObject(i)));if(pages.isEmpty())throw new IOException(L10n.t("目录已没有图片，请刷新书架"));request.check();b.pages=pages;}
 static String imagePath(Page p,String variant,int width){return "/v1/files/"+Uri.encode(p.id)+"/"+variant+"?w="+width+"&version="+Uri.encode(p.version);}
}
