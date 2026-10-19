using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace LeafReader {
 public sealed class ShelfNode {
  public string Key,Name,SourceKey,RelativePath;
  public bool IsSource;
  public ShelfNode Parent;
  public List<Book> Books=new List<Book>();
  public List<ShelfNode> Children=new List<ShelfNode>();
  public int Count;public DateTime CreatedUtc;public Book CoverBook;
 }
 public static class ShelfTree {
  static string NormalizeRelative(string value){
   if(string.IsNullOrEmpty(value))return "";
   return value.Replace('\\','/').Trim('/');
  }
  static string NodeKey(string source,string relative){
   string text=source.StartsWith("local:",StringComparison.OrdinalIgnoreCase)?relative.ToUpperInvariant():relative;
   return source+"|"+text;
  }
  static int CompareNodes(ShelfNode a,ShelfNode b){int c=NaturalOrder.Instance.Compare(a.Name,b.Name);return c!=0?c:string.CompareOrdinal(a.Key,b.Key);}
  static int CompareBooks(Book a,Book b){int c=NaturalOrder.Instance.Compare(a.Title,b.Title);return c!=0?c:string.CompareOrdinal(a.Id,b.Id);}
  public static List<ShelfNode> Build(IEnumerable<Book> books){
   var roots=new List<ShelfNode>();var bySource=new Dictionary<string,ShelfNode>(StringComparer.Ordinal);
   foreach(Book book in books){
    string source=book.SourceKey;if(string.IsNullOrEmpty(source))source="book:"+book.Id;
    ShelfNode root;
    if(!bySource.TryGetValue(source,out root)){
     root=new ShelfNode{Key=source+"|",Name=string.IsNullOrEmpty(book.SourceName)?source:book.SourceName,SourceKey=source,RelativePath="",IsSource=true};
     bySource[source]=root;roots.Add(root);
    }
    string relative=NormalizeRelative(book.RelativePath);ShelfNode node=root;
    if(relative.Length>0){
     string accumulated="";string[] parts=relative.Split('/');
     for(int i=0;i<parts.Length;i++){
      string part=parts[i];if(part.Length==0)continue;
      accumulated=accumulated.Length==0?part:accumulated+"/"+part;string key=NodeKey(source,accumulated);
      ShelfNode child=null;for(int j=0;j<node.Children.Count;j++)if(node.Children[j].Key==key){child=node.Children[j];break;}
      if(child==null){child=new ShelfNode{Key=key,Name=part,SourceKey=source,RelativePath=accumulated,Parent=node};node.Children.Add(child);}
      node=child;
     }
    }
    node.Books.Add(book);
   }
   foreach(ShelfNode root in roots)Compute(root);
   roots.Sort(CompareNodes);return roots;
  }
  static void Compute(ShelfNode node){
   node.Books.Sort(CompareBooks);node.Children.Sort(CompareNodes);
   int count=node.Books.Count;DateTime created=DateTime.MinValue;Book cover=node.Books.Count>0?node.Books[0]:null;
   foreach(Book book in node.Books)if(book.CreatedUtc>created)created=book.CreatedUtc;
   foreach(ShelfNode child in node.Children){
    Compute(child);count+=child.Count;if(child.CreatedUtc>created)created=child.CreatedUtc;
    if(cover==null||(child.CoverBook!=null&&CompareBooks(child.CoverBook,cover)<0))cover=child.CoverBook;
   }
   node.Count=count;node.CreatedUtc=created;node.CoverBook=cover;
  }
  public static ShelfNode Find(List<ShelfNode> roots,string key){
   if(string.IsNullOrEmpty(key))return null;
   foreach(ShelfNode root in roots){ShelfNode found=Find(root,key);if(found!=null)return found;}
   return null;
  }
  static ShelfNode Find(ShelfNode node,string key){
   if(string.Equals(node.Key,key,StringComparison.OrdinalIgnoreCase))return node;
   foreach(ShelfNode child in node.Children){ShelfNode found=Find(child,key);if(found!=null)return found;}
   return null;
  }
  public static List<ShelfNode> Path(List<ShelfNode> roots,string key){
   var path=new List<ShelfNode>();ShelfNode node=Find(roots,key);
   while(node!=null){path.Insert(0,node);node=node.Parent;}
   return path;
  }
  public static List<Book> Descendants(ShelfNode node){
   var books=new List<Book>();Collect(node,books);return books;
  }
  static void Collect(ShelfNode node,List<Book> books){
   books.AddRange(node.Books);foreach(ShelfNode child in node.Children)Collect(child,books);
  }
 }
 internal static class ShelfGroups {
  static string Normalize(string root){try{return Path.GetFullPath(root);}catch{return null;}}
  internal static void AssignLocal(Book book,IEnumerable<string> roots){
   string path=Path.GetFullPath(book.Id);
   string root=roots.Select(Normalize).Where(r=>r!=null).Where(r=>string.Equals(path,r,StringComparison.OrdinalIgnoreCase)||path.StartsWith(r.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).OrderByDescending(r=>r.Length).FirstOrDefault()??book.Folder??path;
   root=root.TrimEnd(Path.DirectorySeparatorChar);
   string folder=book.Pages.Count>0&&book.Pages[0].Entry!=null?Path.GetDirectoryName(path):path;
   string relative="";
   if(!string.IsNullOrEmpty(folder)&&folder.Length>root.Length&&folder.StartsWith(root,StringComparison.OrdinalIgnoreCase))relative=folder.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   book.RelativePath=relative.Replace('\\','/');
   book.SourceKey="local:"+root.ToUpperInvariant();book.SourceName=Path.GetFileName(root);if(string.IsNullOrEmpty(book.SourceName))book.SourceName=root;
  }
  internal static List<Book> Groups(IEnumerable<Book> books){return books.GroupBy(b=>b.SourceKey??b.Id).Select(g=>{
   var first=g.OrderBy(b=>b.Title,NaturalOrder.Instance).ThenBy(b=>b.Id,NaturalOrder.Instance).First();
   return new Book{Id="group:"+g.Key,GroupId=g.Key,GroupCount=g.Count(),Title=first.SourceName??first.SourceLabel??first.Folder??first.Title,Folder=first.Folder,SourceLabel=first.SourceLabel,Pages=first.Pages,CreatedUtc=g.Max(b=>b.CreatedUtc)};
  }).OrderBy(b=>b.Title,NaturalOrder.Instance).ThenBy(b=>b.Id,NaturalOrder.Instance).ToList();}
 }
}
