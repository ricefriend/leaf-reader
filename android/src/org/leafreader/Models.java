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
 static class Book {String id,name,source,rootId,rootName,groupId,relativePath="";int groupCount;long created;Server server;int count;boolean groupRead;List<Page> pages=new ArrayList<>();}
 static class ShelfNode {
  String key,name,sourceKey,relativePath="";boolean source;ShelfNode parent;
  List<Book> books=new ArrayList<>();List<ShelfNode> children=new ArrayList<>();
  int count;long created;Book cover;
 }
 static String sourceKey(Book b){return b.server==null?"local:"+b.rootId:"remote:"+b.server.id+":"+b.rootId;}
 static String normRelative(String value){if(value==null)return "";return value.replace('\\','/').replaceAll("^/+","").replaceAll("/+$","");}
 static String nodeKey(String source,String relative){
  boolean local=source!=null&&source.startsWith("local:");
  return source+"|"+(local?relative.toUpperCase(Locale.ROOT):relative);
 }
 static String key(String source,String relative){return nodeKey(source,relative);}
 static String sourceName(Book b){String root=b.rootName==null?"":b.rootName;return b.server==null?root:b.server.name+" / "+root;}
 static List<ShelfNode> tree(List<Book> books){
  Map<String,ShelfNode> roots=new LinkedHashMap<>();
  for(Book b:books){
   String source=sourceKey(b);ShelfNode root=roots.get(source);
   if(root==null){root=new ShelfNode();root.key=source+"|";root.name=sourceName(b);if(root.name.isEmpty())root.name=source;root.sourceKey=source;root.source=true;roots.put(source,root);}
   String relative=normRelative(b.relativePath);ShelfNode node=root;
   if(!relative.isEmpty()){
    String accumulated="";for(String part:relative.split("/")){if(part.isEmpty())continue;accumulated=accumulated.isEmpty()?part:accumulated+"/"+part;String key=nodeKey(source,accumulated);ShelfNode child=null;for(ShelfNode candidate:node.children)if(candidate.key.equalsIgnoreCase(key)){child=candidate;break;}if(child==null){child=new ShelfNode();child.key=key;child.name=part;child.sourceKey=source;child.relativePath=accumulated;child.parent=node;node.children.add(child);}node=child;}
   }
   node.books.add(b);
  }
  List<ShelfNode> result=new ArrayList<>(roots.values());for(ShelfNode root:result)compute(root);
  result.sort((a,b)->{int c=NATURAL.compare(a.name,b.name);return c!=0?c:a.key.compareTo(b.key);});
  return result;
 }
 static void compute(ShelfNode node){
  node.books.sort((a,b)->{int c=NATURAL.compare(a.name,b.name);return c!=0?c:a.id.compareTo(b.id);});
  node.children.sort((a,b)->{int c=NATURAL.compare(a.name,b.name);return c!=0?c:a.key.compareTo(b.key);});
  int count=node.books.size();long created=0;Book cover=node.books.isEmpty()?null:node.books.get(0);
  for(Book book:node.books)created=Math.max(created,book.created);
  for(ShelfNode child:node.children){compute(child);count+=child.count;created=Math.max(created,child.created);if(cover==null||(child.cover!=null&&compareBooks(child.cover,cover)<0))cover=child.cover;}
  node.count=count;node.created=created;node.cover=cover;
 }
 static int compareBooks(Book a,Book b){int c=NATURAL.compare(a.name,b.name);return c!=0?c:a.id.compareTo(b.id);}
 static ShelfNode find(List<ShelfNode> roots,String key){
  if(key==null||key.isEmpty())return null;
  for(ShelfNode root:roots){ShelfNode found=find(root,key);if(found!=null)return found;}return null;
 }
 static ShelfNode find(ShelfNode node,String key){
  if(node.key.equalsIgnoreCase(key))return node;
  for(ShelfNode child:node.children){ShelfNode found=find(child,key);if(found!=null)return found;}return null;
 }
 static List<ShelfNode> path(List<ShelfNode> roots,String key){
  List<ShelfNode> path=new ArrayList<>();ShelfNode node=find(roots,key);
  while(node!=null){path.add(0,node);node=node.parent;}return path;
 }
 static List<Book> descendants(ShelfNode node){
  List<Book> result=new ArrayList<>();collect(node,result);return result;
 }
 static void collect(ShelfNode node,List<Book> result){
  result.addAll(node.books);for(ShelfNode child:node.children)collect(child,result);
 }
 static Book pseudo(ShelfNode node){
  Book book=new Book();book.id="group:"+node.key;book.groupId=node.key;book.groupCount=node.count;book.name=node.name;book.source=node.sourceKey;book.created=node.created;
  if(node.cover!=null){book.pages=node.cover.pages;book.server=node.cover.server;}return book;
 }
 static List<Book> groups(List<Book> books){List<Book> result=new ArrayList<>();for(ShelfNode root:tree(books))result.add(pseudo(root));return result;}
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
