package org.leafreader;

import java.util.*;
import static org.leafreader.Models.*;

public final class ModelTests {
 static int checks;
 static void check(boolean value,String message){if(!value)throw new AssertionError(message);checks++;System.out.println("PASS "+message);}
 static Book book(int count){Book b=new Book();b.id="test";for(int i=0;i<count;i++){Page p=new Page();p.id="file-"+i;p.width=600;p.height=900;b.pages.add(p);}return b;}
 public static void main(String[] args)throws Exception{
  List<String> names=new ArrayList<>(Arrays.asList("10.png","2.png","1.png","999999999999999999999.png","0003.png","中文 10.png","中文 2.png"));names.sort(NATURAL);check(names.indexOf("2.png")<names.indexOf("10.png")&&names.indexOf("中文 2.png")<names.indexOf("中文 10.png"),"natural sorting including Unicode and long numbers");
  boolean all=true;int scenarios=0;for(int length=1;length<=35;length++)for(int anchor=0;anchor<length;anchor++){Book b=book(length);for(int i=4;i<length;i+=7)b.pages.get(i).width=1800;List<Integer> starts=new ArrayList<>(),visited=new ArrayList<>();int p=0;while(true){starts.add(p);int n=Models.count(b,p,1,anchor,true,true);for(int j=0;j<n;j++)visited.add(p+j);int next=Models.next(b,p,1,anchor,true,true);if(next==p)break;p=next;}all&=visited.size()==length;for(int i=0;i<length;i++)all&=visited.get(i)==i;for(int i=1;i<starts.size();i++)all&=Models.previous(b,starts.get(i),2,anchor,true,true)==starts.get(i-1);scenarios++;}check(all&&scenarios==630,"Android paging parity with 630 Windows regression scenarios");
  Book b=book(20);int p=11;check(Models.next(b,p,1,p,true,true)==13&&Models.previous(b,13,2,p,true,true)==11,"11-12 shifted to 12-13 then 14-15 and reverse");
  Progress progress=new Progress();progress.pageId="file-12";progress.anchorId="file-10";progress.page=0;progress.anchor=0;progress.mode=3;progress.fraction=.4f;progress.bookmarks.add("file-15");Collections.swap(b.pages,0,12);Progress restored=Progress.parse(progress.json().toString());restored.restore(b);check(restored.page==0&&restored.anchor==10&&restored.fraction==.4f&&restored.bookmarks.contains("file-15"),"stable progress and bookmark identifiers survive list changes");
  Page a=new Page();a.id="same";a.version="v1";Server one=new Server();one.id="one";a.server=one;Page c=new Page();c.id="same";c.version="v1";Server two=new Server();two.id="two";c.server=two;check(!a.key().equals(c.key()),"source identity isolates equal filenames");c.server=one;c.version="v2";check(!a.key().equals(c.key()),"file version invalidates cached image key");
  Book g1=book(1);g1.name="10";g1.rootId="uri-one";g1.rootName="中文 # &";Book g2=book(1);g2.name="2";g2.rootId="uri-one";g2.rootName=g1.rootName;Book g3=book(1);g3.name="3";g3.rootId="uri-two";g3.rootName=g1.rootName;
  List<Book> grouped=Models.groups(Arrays.asList(g1,g2,g3));check(grouped.size()==2&&grouped.stream().mapToInt(g->g.groupCount).sum()==3,"SAF source groups preserve books and distinguish same folder names");check(grouped.stream().filter(g->g.groupCount==2).findFirst().get().pages==g2.pages,"source cover uses naturally first child without copying images");g1.server=one;g2.server=two;g2.rootId=g1.rootId;check(Models.groups(Arrays.asList(g1,g2)).size()==2,"remote server identity separates equal authorized root ids");check(!Models.sourceKey(g1).equals(Models.sourceKey(g3)),"local and remote group identities are separate");
  check(Models.image("中文 # & 2.WebP")&&Models.zip("漫画.CBZ")&&!Models.image("secret.txt"),"format classification");System.out.println("ALL "+checks+" MODEL TESTS PASSED");
 }
}
