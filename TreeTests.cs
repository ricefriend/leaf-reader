using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace LeafReader {
 public static class TreeTests {
  static List<string> lines=new List<string>();
  static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);lines.Add("PASS: "+label);}
  static Book B(string source,string relative,string title){
   return new Book{Id=source+"#"+relative,Title=title,SourceKey=source,SourceName=source,RelativePath=relative,
    Pages=new List<Page>{new Page{Path="p:"+source+"#"+relative,Width=600,Height=900}}};
  }
  public static int Run(string directory){
   Directory.CreateDirectory(directory);
   try{
    Check(new[]{"10","2"}.OrderBy(x=>x,NaturalOrder.Instance).First()=="2","natural order sanity");

    string root=Path.Combine(directory,"lib");
    var folderBook=new Book{Id=Path.Combine(root,"A","01"),Title="01",Pages=new List<Page>{new Page{Path=Path.Combine(root,"A","01","001.jpg")}}};
    ShelfGroups.AssignLocal(folderBook,new[]{root});
    Check(folderBook.RelativePath=="A/01","local folder relative path uses forward slashes");

    var rootBook=new Book{Id=root,Title="root",Pages=new List<Page>{new Page{Path=Path.Combine(root,"001.jpg")}}};
    ShelfGroups.AssignLocal(rootBook,new[]{root});
    Check(rootBook.RelativePath=="","book directly under source root has empty relative path");

    var archive=new Book{Id=Path.Combine(root,"A","vol.cbz"),Title="vol",Pages=new List<Page>{new Page{Path=Path.Combine(root,"A","vol.cbz"),Entry="1.jpg"}}};
    ShelfGroups.AssignLocal(archive,new[]{root});
    Check(archive.RelativePath=="A","archive relative path is its containing folder");

    var variants=new[]{
     B("local:R","","root book"),
     B("local:R","A","A book"),
     B("local:R","A/01","01 book"),
     B("local:R","A/01/番外","番外 book"),
     B("local:R","a/02","02 book"),
     B("local:R","A/02","02 book duplicate case")
    };
    var roots=ShelfTree.Build(variants);
    Check(roots.Count==1,"tree keeps one source");
    Check(roots[0].Count==6,"tree counts every book once");
    Check(roots[0].Children.Count==1,"case-insensitive local paths share a folder");
    Check(roots[0].Books.Count==1&&roots[0].Children[0].Books.Count==1,"mixed folders keep direct books and children");
    Check(roots[0].Children[0].Children.Count==2,"deeper folders become child nodes");
    Check(roots[0].Children[0].Children[1].Books.Count==2,"case-insensitive paths merge book folders");
    var path=ShelfTree.Path(roots,"local:R|A/01");
    Check(path.Count==3&&path[0].Name=="local:R"&&path[1].Name=="A"&&path[2].Name=="01","path returns root, parent and node");
    Check(ShelfTree.Find(roots,"missing")==null,"unknown node returns null");
    Check(ShelfTree.Descendants(roots[0]).Count()==6,"descendants include every book once");
    Check(roots[0].CoverBook!=null&&roots[0].CoverBook==ShelfTree.Descendants(roots[0]).OrderBy(b=>b.Title,NaturalOrder.Instance).First(),"folder cover uses first natural descendant book");

    var sorted=ShelfTree.Build(new[]{
     B("local:S","","root"),
     B("local:S","10卷","ten"),
     B("local:S","2卷","two"),
     B("local:S","1卷","one")
    });
    Check(sorted[0].Children.Select(c=>c.Name).SequenceEqual(new[]{"1卷","2卷","10卷"}),"folder children use natural order");

    Check(ShelfTree.Build(new Book[0]).Count==0,"empty library has no roots");

    var progress=new Dictionary<string,Progress>();
    var grouped=ShelfView.Build(roots,"local:R|",true,"",0,progress);
    Check(grouped.TotalBooks==6&&grouped.Folders.Count==1&&grouped.Direct.Count==1,"grouped node separates folders and direct books");
    Check(grouped.Items.Count==2&&grouped.Items[0].GroupId=="local:R|A","folder card precedes direct book");
    var inside=ShelfView.Build(roots,"local:R|A",true,"",0,progress);
    Check(inside.Direct.Count==1&&inside.Direct[0].IsCurrentFolderBook,"direct book at a mixed folder is marked as this folder");
    Check(inside.Folders.Count==2&&inside.Items.Count==3,"mixed folder shows two child folders and its own book");
    var flat=ShelfView.Build(roots,"local:R|A",false,"",0,progress);
    Check(flat.TotalBooks==5&&flat.Items.Count==5&&flat.Folders.Count==0,"flat node lists all descendants");
    var search=ShelfView.Build(roots,"local:R|A",true,"番外",0,progress);
    Check(search.Items.Count==1&&search.Items[0].Title=="番外 book"&&search.Folders.Count==0,"search hides folder cards and stays in scope");
    var rootView=ShelfView.Build(roots,null,true,"",0,progress);
    Check(rootView.Items.Count==1&&rootView.Items[0].GroupId=="local:R|","virtual root shows sources");

    var recent=new Dictionary<string,Progress>();
    recent["local:R#A/02"]=new Progress{LastRead="2026-10-18T10:00:00Z",Page=0};
    var recentView=ShelfView.Build(roots,"local:R|A",true,"",1,recent);
    Check(recentView.Folders.Count==2&&recentView.Folders[0].Name=="02","recent folder sort uses descendant read time");
    Check(recentView.ContinueBook!=null&&recentView.ContinueBook.Id=="local:R#A/02","continue reading picks the most recent book");
    Check(recentView.Folders[0].CoverBook!=null,"folder keeps a cover book");

    File.WriteAllText(Path.Combine(directory,"tree-results.txt"),string.Join("\r\n",lines)+"\r\nRESULT: ALL TESTS PASSED");
    return 0;
   }catch(Exception ex){lines.Add(ex.Message);File.WriteAllText(Path.Combine(directory,"tree-results.txt"),string.Join("\r\n",lines)+"\r\nRESULT: FAILED");return 1;}
  }
 }
}
