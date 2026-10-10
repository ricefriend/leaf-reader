using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace LeafReader {
 internal static class ShelfGroups {
  static string Normalize(string root){try{return Path.GetFullPath(root);}catch{return null;}}
  internal static void AssignLocal(Book book,IEnumerable<string> roots){
   string path=Path.GetFullPath(book.Id);
   string root=roots.Select(Normalize).Where(r=>r!=null).Where(r=>string.Equals(path,r,StringComparison.OrdinalIgnoreCase)||path.StartsWith(r.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)).OrderByDescending(r=>r.Length).FirstOrDefault()??book.Folder??path;
   root=root.TrimEnd(Path.DirectorySeparatorChar);book.SourceKey="local:"+root.ToUpperInvariant();book.SourceName=Path.GetFileName(root);if(string.IsNullOrEmpty(book.SourceName))book.SourceName=root;
  }
  internal static List<Book> Groups(IEnumerable<Book> books){return books.GroupBy(b=>b.SourceKey??b.Id).Select(g=>{
   var first=g.OrderBy(b=>b.Title,NaturalOrder.Instance).ThenBy(b=>b.Id,NaturalOrder.Instance).First();
   return new Book{Id="group:"+g.Key,GroupId=g.Key,GroupCount=g.Count(),Title=first.SourceName??first.SourceLabel??first.Folder??first.Title,Folder=first.Folder,SourceLabel=first.SourceLabel,Pages=first.Pages,CreatedUtc=g.Max(b=>b.CreatedUtc)};
  }).OrderBy(b=>b.Title,NaturalOrder.Instance).ThenBy(b=>b.Id,NaturalOrder.Instance).ToList();}
 }
}
