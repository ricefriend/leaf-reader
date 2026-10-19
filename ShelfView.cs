using System;
using System.Linq;
using System.Collections.Generic;
namespace LeafReader {
 public sealed class ShelfView {
  public ShelfNode Node;
  public List<ShelfNode> Path=new List<ShelfNode>();
  public List<ShelfNode> Folders=new List<ShelfNode>();
  public List<Book> Direct=new List<Book>();
  public List<Book> Items=new List<Book>();
  public int TotalBooks,TotalFolders,TotalPages;
  public Book ContinueBook;
  static string LastRead(Book book,Dictionary<string,Progress> progress){
   Progress p;return progress!=null&&progress.TryGetValue(book.Id,out p)&&p!=null?p.LastRead:null;
  }
  static void SetRead(Book book,Dictionary<string,Progress> progress){
   book.ReadPercent=0;Progress p;
   if(progress==null||!progress.TryGetValue(book.Id,out p)||p==null||string.IsNullOrEmpty(p.LastRead)||book.PageCount<=0)return;
   book.ReadPercent=Math.Max(0,Math.Min(100,(p.Page+1)*100/book.PageCount));
  }
  static bool Matches(Book book,string query){
   return Contains(book.Title,query)||Contains(book.RelativePath,query)||Contains(book.SourceName,query)||Contains(book.Folder,query);
  }
  static bool Contains(string value,string query){
   return !string.IsNullOrEmpty(value)&&value.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0;
  }
  static int CompareBooks(Book a,Book b,int sort,Dictionary<string,Progress> progress){
   int c=0;
   if(sort==1){
    string ar=LastRead(a,progress)??"",br=LastRead(b,progress)??"";
    c=string.CompareOrdinal(br,ar);
   }else if(sort==2){
    bool au=string.IsNullOrEmpty(LastRead(a,progress)),bu=string.IsNullOrEmpty(LastRead(b,progress));
    c=bu.CompareTo(au);
   }else if(sort==3||sort==4){
    c=a.CreatedUtc.CompareTo(b.CreatedUtc);if(sort==3)c=-c;
   }
   if(c!=0)return c;c=NaturalOrder.Instance.Compare(a.Title,b.Title);
   return c!=0?c:string.CompareOrdinal(a.Id,b.Id);
  }
  static int CompareFolders(ShelfNode a,ShelfNode b,int sort,Dictionary<string,Progress> progress,Dictionary<string,string> latest,Dictionary<string,bool> read){
   int c=0;
   if(sort==1){
    string ar=latest.ContainsKey(a.Key)?latest[a.Key]:"",br=latest.ContainsKey(b.Key)?latest[b.Key]:"";
    c=string.CompareOrdinal(br,ar);
   }else if(sort==2){
    bool au=read.ContainsKey(a.Key)&&read[a.Key],bu=read.ContainsKey(b.Key)&&read[b.Key];
    c=bu.CompareTo(au);
   }else if(sort==3||sort==4){
    c=a.CreatedUtc.CompareTo(b.CreatedUtc);if(sort==3)c=-c;
   }
   if(c!=0)return c;c=NaturalOrder.Instance.Compare(a.Name,b.Name);
   return c!=0?c:string.CompareOrdinal(a.Key,b.Key);
  }
  static Book Pseudo(ShelfNode node){
   return new Book{Id="group:"+node.Key,GroupId=node.Key,GroupCount=node.Count,Title=node.Name,Pages=node.CoverBook!=null?node.CoverBook.Pages:new List<Page>(),CreatedUtc=node.CreatedUtc,SourceName=node.Name,SourceKey=node.SourceKey};
  }
  static List<Book> AllBooks(List<ShelfNode> roots){
   var books=new List<Book>();foreach(ShelfNode root in roots)ShelfTree.Descendants(root).ForEach(books.Add);return books;
  }
  public static ShelfView Build(List<ShelfNode> roots,string key,bool grouped,string query,int sort,Dictionary<string,Progress> progress){
   var view=new ShelfView();sort=Math.Max(0,Math.Min(4,sort));
   ShelfNode node=string.IsNullOrEmpty(key)?null:ShelfTree.Find(roots,key);
   view.Node=node;
   List<Book> scope;
   if(node==null){scope=AllBooks(roots);}
   else{view.Path=ShelfTree.Path(roots,key);scope=ShelfTree.Descendants(node);}
   foreach(Book book in scope){book.IsCurrentFolderBook=false;SetRead(book,progress);}
   view.TotalBooks=scope.Count;
   foreach(Book book in scope)view.TotalPages+=book.PageCount;
   string text=query==null?"":query.Trim();
   if(text.Length>0){
    view.Items=scope.Where(b=>Matches(b,text)).ToList();
    view.Items.Sort((a,b)=>CompareBooks(a,b,sort,progress));
    view.TotalFolders=0;
    view.ContinueBook=FindContinueBook(view.Items,progress);
    return view;
   }
   if(!grouped){
    view.Items=scope;view.Items.Sort((a,b)=>CompareBooks(a,b,sort,progress));
    view.TotalFolders=0;
    view.ContinueBook=FindContinueBook(view.Items,progress);
    return view;
   }
   List<ShelfNode> folders=node==null?roots:node.Children;
   view.Folders=new List<ShelfNode>();
   var leafBooks=new List<Book>();
   foreach(ShelfNode child in folders){
    if(child.Children.Count>0)view.Folders.Add(child);
    else leafBooks.AddRange(child.Books);
   }
   var latest=new Dictionary<string,string>();var read=new Dictionary<string,bool>();
   foreach(ShelfNode folder in view.Folders){
    string last=null;bool any=false;
    foreach(Book book in ShelfTree.Descendants(folder)){
     string value=LastRead(book,progress);if(!string.IsNullOrEmpty(value)){any=true;if(last==null||string.CompareOrdinal(value,last)>0)last=value;}
    }
    latest[folder.Key]=last??"";read[folder.Key]=any;
   }
   view.Folders.Sort((a,b)=>CompareFolders(a,b,sort,progress,latest,read));
   view.Direct=node==null?new List<Book>():new List<Book>(node.Books);
   view.Direct.AddRange(leafBooks);
   view.Direct.Sort((a,b)=>CompareBooks(a,b,sort,progress));
   view.TotalFolders=view.Folders.Count;
   foreach(ShelfNode folder in view.Folders)if(folder.CoverBook!=null)view.Items.Add(Pseudo(folder));
   view.Items.AddRange(view.Direct);
   if(node!=null&&!node.IsSource&&node.Children.Count>0){
    foreach(Book book in view.Direct)if(string.Equals(book.RelativePath,node.RelativePath,StringComparison.OrdinalIgnoreCase))book.IsCurrentFolderBook=true;
   }
   view.ContinueBook=FindContinueBook(scope,progress);
   return view;
  }
  static Book FindContinueBook(IEnumerable<Book> books,Dictionary<string,Progress> progress){
   Book best=null;string bestRead=null;
   foreach(Book book in books){
    string value=LastRead(book,progress);if(string.IsNullOrEmpty(value))continue;
    if(best==null||string.CompareOrdinal(value,bestRead)>0){best=book;bestRead=value;}
   }
   return best;
  }
 }
}

