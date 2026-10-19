using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace LeafReader {
 public static class TreeTests {
  static List<string> lines=new List<string>();
  static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);lines.Add("PASS: "+label);}
  public static int Run(string directory){
   Directory.CreateDirectory(directory);
   try{
    Check(new[]{"10","2"}.OrderBy(x=>x,NaturalOrder.Instance).First()=="2","natural order sanity");
    File.WriteAllText(Path.Combine(directory,"tree-results.txt"),string.Join("\r\n",lines)+"\r\nRESULT: ALL TESTS PASSED");
    return 0;
   }catch(Exception ex){lines.Add(ex.Message);File.WriteAllText(Path.Combine(directory,"tree-results.txt"),string.Join("\r\n",lines)+"\r\nRESULT: FAILED");return 1;}
  }
 }
}
