package org.leafreader;

import java.util.*;
import org.json.*;

final class Models {
 static final Comparator<String> NATURAL=(a,b)->{int i=0,j=0;while(i<a.length()&&j<b.length()){
  char x=a.charAt(i),y=b.charAt(j);if(Character.isDigit(x)&&Character.isDigit(y)){int ai=i,bj=j;while(i<a.length()&&Character.isDigit(a.charAt(i)))i++;while(j<b.length()&&Character.isDigit(b.charAt(j)))j++;String aa=a.substring(ai,i).replaceFirst("^0+",""),bb=b.substring(bj,j).replaceFirst("^0+","");int c=Integer.compare(aa.length(),bb.length());if(c!=0)return c;c=aa.compareTo(bb);if(c!=0)return c;}else{int c=Character.compare(Character.toUpperCase(x),Character.toUpperCase(y));if(c!=0)return c;i++;j++;}}
  int c=Integer.compare(a.length()-i,b.length()-j);return c!=0?c:a.compareTo(b);};
 static class Server {String id,address,name,token;JSONObject json()throws JSONException{return new JSONObject().put("id",id).put("address",address).put("name",name).put("token",token);}}
 static class Page {String id,name,version,uri,entry,archive;int width=800,height=1200;Server server;boolean metadata;
  String key(){return (server==null?"local":server.id)+"|"+id+"|"+version;}
 }
 static class Book {String id,name,source,rootId;long created;Server server;int count;List<Page> pages=new ArrayList<>();}
 static class Progress {int page,anchor,mode=3;float fraction;String pageId="",anchorId="";Set<String> bookmarks=new HashSet<>();
  JSONObject json()throws JSONException{ return new JSONObject().put("page",page).put("pageId",pageId).put("mode",mode).put("anchor",anchor).put("anchorId",anchorId).put("fraction",fraction).put("bookmarks",new JSONArray(bookmarks));}
  static Progress parse(String text){Progress p=new Progress();try{JSONObject o=new JSONObject(text);p.page=o.optInt("page");p.pageId=o.optString("pageId");p.mode=o.optInt("mode",3);p.anchor=o.optInt("anchor");p.anchorId=o.optString("anchorId");p.fraction=(float)o.optDouble("fraction");JSONArray b=o.optJSONArray("bookmarks");if(b!=null)for(int i=0;i<b.length();i++)p.bookmarks.add(b.getString(i));}catch(Exception ignored){}return p;}
  void restore(Book b){for(int i=0;i<b.pages.size();i++){if(b.pages.get(i).id.equals(pageId))page=i;if(b.pages.get(i).id.equals(anchorId))anchor=i;}page=Math.max(0,Math.min(b.pages.size()-1,page));anchor=Math.max(0,Math.min(b.pages.size()-1,anchor));mode=Math.max(0,Math.min(3,mode));fraction=Math.max(0,Math.min(1,fraction));}
 }
 static int count(Book b,int p,int mode,int anchor,boolean cover,boolean wide){if(mode==0||mode==3||p==b.pages.size()-1||cover&&p==0||p<anchor&&p+2>anchor)return 1;if(wide&&(isWide(b.pages.get(p))||isWide(b.pages.get(p+1))))return 1;return 2;}
 static boolean isWide(Page p){return p.width>p.height*1.18;}
 static int next(Book b,int p,int mode,int anchor,boolean cover,boolean wide){int n=p+count(b,p,mode,anchor,cover,wide);return n>=b.pages.size()?p:n;}
 static int previous(Book b,int p,int mode,int anchor,boolean cover,boolean wide){if(p<=0)return 0;if(mode==0||mode==3)return p-1;int current=0,previous=0;while(current<p){previous=current;current+=count(b,current,mode,anchor,cover,wide);}return previous;}
 static boolean image(String name){return name.toLowerCase(Locale.ROOT).matches(".*\\.(jpg|jpeg|png|bmp|gif|tif|tiff|webp)$");}
 static boolean zip(String name){return name.toLowerCase(Locale.ROOT).matches(".*\\.(zip|cbz)$");}
}
