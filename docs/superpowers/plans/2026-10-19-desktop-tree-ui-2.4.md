# Leaf Reader 2.4 Desktop Tree Shelf & UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Leaf Reader 2.4.0 for Windows with a hierarchical source/folder tree, drill-down grouped browsing, and a redesigned modern bookshelf, without regressing reading, progress, or existing imports.

**Architecture:** Add a normalized `Book.RelativePath` and a pure `ShelfTree`/`ShelfView` layer that turns the flat scan result into folder nodes, grouped items, flat items, search results, and breadcrumbs. `MainWindow` renders that view with a new sidebar (wide) or breadcrumb toolbar (narrow); `ShelfGrid` keeps its viewport-only card realization but gains the new card and folder-card visuals.

**Tech Stack:** C# 5, WPF, .NET Framework 4.8, `csc.exe` via `build.ps1`; no new dependencies; existing `SelfTests` + a new fast `--tree-test` harness.

**Spec:** `docs/superpowers/specs/2026-10-19-tree-ui-2.4-design.md`

## Global Constraints

- Windows desktop only in this plan. Android tree support is a separate follow-up plan; do not modify `android/src` except the generated `L10n.java` from the localization step.
- Keep the scan rule: every folder that directly contains images is one `Book`; ZIP/CBZ is one `Book` in its containing folder.
- Keep `Book.Id` and progress/bookmark identity unchanged.
- Keep old `library.json` readable; no required new persisted fields.
- All new user-visible copy goes through `L10n` and `localization/strings.json`; never hardcode Chinese or English in layout code.
- Fixed card size 168×288 (cover 168×224), grid gap 16/18, sidebar width 248, wide-screen threshold 1120 px.
- Target version 2.4.0 (`AssemblyVersion 2.4.0.0`).
- Work on branch `feat/2.4-tree-ui`; commit only touched files. Do not stage the pre-existing line-ending-only modifications.
- Build scripts are UTF-8 without BOM; Windows PowerShell 5.1 misreads them. Task 1 makes `build.ps1` UTF-8-with-BOM so `& .\build.ps1` works.

## Review Focus

These are the inputs most likely to break a real library; each has a test in the owning task.

1. **Mixed folder** (direct images plus book-bearing subfolders) must expose both the folder's own book and its children, with the own book not lost or double-counted.
2. **Deep/duplicate paths and archives** — `系列/第01卷/番外`, duplicate folder names under different parents, and a CBZ at a nested path must resolve to the right node and relative path.
3. **Stale current node after rescan** — deleting/renaming the current folder must fall back to the nearest surviving ancestor, never an empty or crashed shelf.
4. **Resize across 1120 px while scrolled** — sidebar show/hide must not reset the card grid to the top or change the selected folder.
5. **Search scope and clearing** — search inside a folder must stay inside that folder and clearing must return to it, not the root.

---

### Task 1: Fast test harness and a build that works in Windows PowerShell

**Files:**
- Modify: `build.ps1` (encoding only; content unchanged)
- Modify: `Program.cs:15-34`
- Create: `TreeTests.cs`

**Interfaces:**
- Produces: `public static class TreeTests { public static int Run(string directory); }`
- Produces: `LeafReader.exe --tree-test <dir>` writes `<dir>\tree-results.txt` with `RESULT: ALL TESTS PASSED` and exits 0, or `FAIL: …` and exits 1.

- [ ] **Step 1: Re-save build.ps1 as UTF-8 with BOM**

Run (PowerShell 5.1):

```powershell
$p='D:\AI\leaf-reader\build.ps1'
$text=[IO.File]::ReadAllText($p,[Text.Encoding]::UTF8)
[IO.File]::WriteAllText($p,$text,(New-Object Text.UTF8Encoding($true)))
```

Then run `& .\build.ps1` and expect `已生成：D:\AI\LeafReader.exe`, exit 0.

- [ ] **Step 2: Run the existing baseline tests**

```powershell
$qa='D:\LeafQA\baseline-2.4'
if(Test-Path $qa){Remove-Item $qa -Recurse -Force}
$p=Start-Process ..\LeafReader.exe -ArgumentList '--self-test',$qa -WindowStyle Hidden -PassThru
$p.WaitForExit(); $p.ExitCode
Get-Content "$qa\test-results.txt" | Select-Object -Last 1
```

Expected: exit 0, `RESULT: ALL TESTS PASSED`.

- [ ] **Step 3: Add the `--tree-test` entry point**

In `Program.cs`, before the normal window path, add a branch mirroring `--self-test`:

```csharp
if(args.Length>=2&&args[0]=="--tree-test"){
 app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
 app.Startup+=(s,e)=>{int result=TreeTests.Run(Path.GetFullPath(args[1]));app.Shutdown(result);};return app.Run();
}
```

- [ ] **Step 4: Create TreeTests.cs with the harness and one sanity check**

```csharp
public static class TreeTests {
 static List<string> lines=new List<string>();
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);lines.Add("PASS: "+label);}
 public static int Run(string directory){
  Directory.CreateDirectory(directory);
  try{ Check(new[]{"10","2"}.OrderBy(x=>x,NaturalOrder.Instance).First()=="2","natural order sanity");
   File.WriteAllText(Path.Combine(directory,"tree-results.txt"),string.Join("\n",lines)+"\nRESULT: ALL TESTS PASSED");
   return 0;
  }catch(Exception ex){lines.Add(ex.Message);File.WriteAllText(Path.Combine(directory,"tree-results.txt"),string.Join("\n",lines)+"\nRESULT: FAILED");return 1;}
 }
}
```

- [ ] **Step 5: Build and run the harness**

```powershell
& .\build.ps1
$qa='D:\LeafQA\tree'
if(Test-Path $qa){Remove-Item $qa -Recurse -Force}
$p=Start-Process ..\LeafReader.exe -ArgumentList '--tree-test',$qa -WindowStyle Hidden -PassThru
$p.WaitForExit(); $p.ExitCode
Get-Content "$qa\tree-results.txt"
```

Expected: exit 0, `RESULT: ALL TESTS PASSED`.

- [ ] **Step 6: Commit**

```powershell
git add build.ps1 Program.cs TreeTests.cs
git commit -m "test: add fast tree test harness and fix build script encoding"
```

---

### Task 2: Relative paths and the folder tree

**Files:**
- Modify: `Core.cs:66-71` (`Book`)
- Modify: `ShelfGroups.cs`
- Modify: `Sources.cs:55` (`RemoteApi.Books`)
- Modify: `TreeTests.cs`

**Interfaces:**
- Produces: `public string Book.RelativePath` — `/` separated, no leading/trailing slash, `""` for a book directly under the source root.
- Produces: `public sealed class ShelfNode { public string Key,Name,SourceKey,RelativePath; public bool IsSource; public List<Book> Books; public List<ShelfNode> Children; public int Count; public DateTime CreatedUtc; public Book CoverBook; }`
- Produces: `public static class ShelfTree { public static List<ShelfNode> Build(IEnumerable<Book> books); public static ShelfNode Find(List<ShelfNode> roots,string key); public static List<ShelfNode> Path(List<ShelfNode> roots,string key); public static List<Book> Descendants(ShelfNode node); }`
- Consumes: `ShelfGroups.AssignLocal` continues to assign `SourceKey`/`SourceName`.

- [ ] **Step 1: Write failing tests for relative paths**

Add to `TreeTests.Run`, before the sanity check:

```csharp
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
```

- [ ] **Step 2: Write failing tree tests**

```csharp
// helper used by these tests
static Book B(string source,string rel,string title){
 return new Book{Id=source+"#"+rel,Title=title,SourceKey=source,SourceName=source,RelativePath=rel,
  Pages=new List<Page>{new Page{Path="p:"+source+"#"+rel,Width=600,Height=900}}};
}
var books=new[]{
 B("local:R","","root book"),
 B("local:R","A","A book"),
 B("local:R","A/01","01 book"),
 B("local:R","A/01/番外","番外 book")
};
var roots=ShelfTree.Build(books);
Check(roots.Count==1&&roots[0].Count==4&&roots[0].Children.Count==1,"tree counts descendants once");
Check(roots[0].Books.Count==1&&roots[0].Children[0].Books.Count==1,"mixed folders keep a direct book and children");
Check(ShelfTree.Path(roots,"local:R|A/01").Count==3,"path returns root, parent and node");
Check(ShelfTree.Find(roots,"missing")==null,"unknown node returns null");
Check(ShelfTree.Descendants(roots[0]).Count()==4,"descendants include the node's own book");
```

- [ ] **Step 3: Run and confirm red**

`& .\build.ps1` → expected compile error: `Book` has no `RelativePath` / `ShelfTree` missing. This is the red state.

- [ ] **Step 4: Add `Book.RelativePath` and compute it in `AssignLocal`**

In `ShelfGroups.AssignLocal`, after resolving `root`:

```csharp
string full=Path.GetFullPath(book.Id);
string folder=book.Pages.Count>0&&book.Pages[0].Entry!=null?Path.GetDirectoryName(full):full;
string rel=folder.Length<=root.Length?"":folder.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
book.RelativePath=rel.Replace(Path.DirectorySeparatorChar,'/').Replace(Path.AltDirectorySeparatorChar,'/');
```

Keep `root` trimmed of trailing separators. Set `RelativePath` for every book assigned.

- [ ] **Step 5: Set remote relative paths**

In `RemoteApi.Books`, set `book.RelativePath=Wire.S(b,"relativePath")` (normalize `\` to `/`).

- [ ] **Step 6: Implement `ShelfNode` and `ShelfTree` in `ShelfGroups.cs`**

`Build` groups by `SourceKey`, creates a root node per source (`RelativePath=""`, `Name=SourceName`), walks each book's `RelativePath` segments, appends the book, sorts children with `NaturalOrder.Instance`, then recursively computes `Count`, `CreatedUtc`, `CoverBook`. `Key` is `SourceKey+"|"+rel` with local path keys uppercased via `ToUpperInvariant()`; source root key is `SourceKey+"|"`.

`Path` returns the ancestor chain excluding the virtual "all books" root; empty if not found. `Descendants` yields all books depth-first.

- [ ] **Step 7: Run tree tests until green**

Expected: all PASS, `RESULT: ALL TESTS PASSED`.

- [ ] **Step 8: Commit**

```powershell
git add Core.cs ShelfGroups.cs Sources.cs TreeTests.cs
git commit -m "feat: build hierarchical shelf tree from relative paths"
```

---

### Task 3: View model for grouped, flat, and search shelves

**Files:**
- Modify: `Core.cs` (`Book` transient display fields)
- Create: `ShelfView.cs`
- Modify: `TreeTests.cs`

**Interfaces:**
- Produces: `public int Book.ReadPercent` (0–100, 0 when unread) and `public bool Book.IsCurrentFolderBook`.
- Produces: `public sealed class ShelfView { public ShelfNode Node; public List<ShelfNode> Path; public List<ShelfNode> Folders; public List<Book> Direct; public List<Book> Items; public int TotalBooks; public int TotalFolders; public Book ContinueBook; public static ShelfView Build(List<ShelfNode> roots,string key,bool grouped,string query,int sort,Dictionary<string,Progress> progress); }`
- Consumes: `ShelfTree`, `ShelfGroups`, `NaturalOrder`, `Library.ByCreated`.

- [ ] **Step 1: Write failing view tests**

```csharp
var p=new Dictionary<string,Progress>{{"b2",new Progress{LastRead="2026-10-18T10:00:00Z"}}};
var grouped=ShelfView.Build(roots,"local:R",true,"",0,p);
Check(grouped.TotalBooks==4&&grouped.Folders.Count==1&&grouped.Direct.Count==1,"grouped node separates folders and direct books");
Check(grouped.Items.Count==2&&grouped.Items[0].GroupId=="local:R|A","folder card precedes direct book");
Check(grouped.Direct[0].IsCurrentFolderBook,"direct book at a non-source node is marked as this folder");
var flat=ShelfView.Build(roots,"local:R",false,"",0,p);
Check(flat.Items.Count==4&&flat.Folders.Count==0,"flat node lists all descendants");
var search=ShelfView.Build(roots,"local:R",true,"番外",0,p);
Check(search.Items.Count==1&&search.Items[0].Title=="番外 book"&&search.Folders.Count==0,"search hides folder cards and stays in scope");
var rootView=ShelfView.Build(roots,null,true,"",0,p);
Check(rootView.Items.Count==1&&rootView.Items[0].GroupId=="local:R","virtual root shows sources");
```

- [ ] **Step 2: Run and confirm red**

`& .\build.ps1` → expected compile error: `ShelfView` missing.

- [ ] **Step 3: Add transient `Book` fields**

`ReadPercent` and `IsCurrentFolderBook` are in-memory only, never serialized by `Settings`.

- [ ] **Step 4: Implement `ShelfView.Build`**

Rules:
- `key==null` = virtual root. Grouped: one pseudo-book per source (pseudo-book id `"group:"+node.Key`). Flat: every book.
- `key` not found: fall back to `""` (all books) and leave `Node==null`; `MainWindow` resolves a nearer ancestor before calling.
- Grouped at a node: `Folders` sorted per `sort`; `Direct` sorted per `sort`; `Items` = folder pseudo-books then direct books.
- Flat at a node: `Items` = all descendants sorted.
- Query non-empty: `Items` = matching descendants (title, relative path, source name; case-insensitive), no folders.
- Pseudo-book: `Id="group:"+node.Key`, `GroupId=node.Key`, `GroupCount=node.Count`, `Title=node.Name`, `Pages=node.CoverBook.Pages`, `SourceName=node.Name` for sources, `CreatedUtc=node.CreatedUtc`.
- Sort indexes: 0 name, 1 recent read, 2 unread first, 3 created new, 4 created old. Folder sort keys use descendant books.
- `ContinueBook` = most recent `LastRead` among the view's books (direct + descendants), else null.
- Set `ReadPercent` from `progress[book.Id]` and `Book.PageCount`.
- Set `IsCurrentFolderBook` when the node is not a source and `book.RelativePath==node.RelativePath`.

- [ ] **Step 5: Run tests until green; commit**

```powershell
git add Core.cs ShelfView.cs TreeTests.cs
git commit -m "feat: add grouped flat and search shelf view model"
```

---

### Task 4: Theme tokens, sidebar, and responsive shell

**Files:**
- Modify: `Theme.xaml`
- Create: `ShelfSidebar.cs`
- Modify: `MainWindow.cs`
- Modify: `SelfTests.cs`

**Interfaces:**
- Produces: `public sealed class ShelfSidebar : Grid { public Action<string> Selected; public Action ManageSources; public Action Refresh; public void SetTree(List<ShelfNode> roots); public void SetActive(string key); internal int RowCount; internal bool IsExpanded(string key); }`
- Produces in `MainWindow`: `internal bool SidebarVisible`, `internal int SidebarRowCount`, `internal int ShelfItemCount`, `internal string ShelfKey`, `internal void SelectShelfNode(string key)`.
- Consumes: `ShelfTree.Build`.

- [ ] **Step 1: Add failing UI assertions to `SelfTests.cs`**

After the window is shown at 1220×820:

```csharp
Check(window.SidebarVisible,"sidebar is visible at 1220px");
Check(window.SidebarRowCount>=3,"sidebar lists all-books and sources");
window.Width=960;window.UpdateLayout();await Task.Delay(150);
Check(!window.SidebarVisible,"sidebar collapses under 1120px");
window.Width=1220;window.UpdateLayout();await Task.Delay(150);
Check(window.SidebarVisible,"sidebar returns above 1120px");
```

- [ ] **Step 2: Run and confirm red**

Expected compile errors for the new members / missing sidebar.

- [ ] **Step 3: Extend `Theme.xaml` with the design tokens and styles**

Add brushes `Side`, `Card`, `Line`, `AccentSoft`, `AccentLine`, `AccentInk`, keep `Bg`, `Panel`, `Hover`, `Fg`, `Muted`, `Accent`. Add keyed styles `PrimaryButton`, `GhostButton`, `SegmentButton`, `CardButton`, `SidebarRow`; update text box / combo box / tooltip radii to 9–10.

- [ ] **Step 4: Build `ShelfSidebar`**

A `Grid` with header row, scrollable rows, footer row. Rows are built from `ShelfNode` lazily: each row is a `Grid` with caret, name, count; children render in a nested `StackPanel` only after first expand. Indent 14 px per depth. Clicking a row raises `Selected(node.Key)`; caret toggles expansion only. `SetActive` expands ancestors, highlights the row, and calls `BringIntoView`. Expose `RowCount` (rendered rows) and `IsExpanded(key)` for tests.

- [ ] **Step 5: Restructure `MainWindow` body into sidebar + content**

`Build()` becomes: header row (existing actions, brand), body `Grid` with columns `Auto` + `*`, sidebar in column 0, content in column 1, message row at the bottom. Keep the existing shelf head/cards temporarily inside the content column so the app still runs. Add `SizeChanged` → `UpdateSidebarVisibility()` using `ActualWidth >= 1120`; add a `reading` guard so the sidebar stays hidden while a book is open. `ShelfSidebar.Selected` sets `shelfGroup` and re-renders; `ManageSources`/`Refresh` call existing methods.

- [ ] **Step 6: Apply the new palette in both themes**

`ApplyTheme` sets all new brush keys: dark `#0E1116/#11161D/#151B23/#1E2732/#232C37/#E9EEF5/#93A1B5/#A8D9BC`, light `#F3F2ED/#EAE7DF/#FBFAF6/#E3E0D6/#D9D5C9/#1E2A24/#61706A/#3F7D5C`. Set `Reader.Light` as today.

- [ ] **Step 7: Build, run self-test, capture wide/narrow screenshots**

Expect the new assertions green; inspect `01-bookshelf.png` and the narrow screenshot for a coherent shell.

- [ ] **Step 8: Commit**

```powershell
git add Theme.xaml ShelfSidebar.cs MainWindow.cs SelfTests.cs
git commit -m "feat: add bookshelf sidebar and responsive shell"
```

---

### Task 5: Toolbar, breadcrumb, hero, and drill-down navigation

**Files:**
- Modify: `MainWindow.cs`
- Modify: `SelfTests.cs`

**Interfaces:**
- Produces: `internal bool ShelfCanGoUp`, `internal string[] ShelfBreadcrumb`, `internal void GoUpShelf()`, `internal void SetShelfGrouped(bool grouped)`, `internal bool ShelfGrouped`.
- Consumes: `ShelfView.Build`, `ShelfTree.Path`.

- [ ] **Step 1: Write failing navigation tests**

Using the nested fixture from Task 4:

```csharp
string sourceKey="local:"+root.ToUpperInvariant();
window.SelectShelfNode(sourceKey+"|系列");
Check(window.ShelfBreadcrumb.Length==3,"breadcrumb includes root source and folder");
Check(window.ShelfCanGoUp,"folder can go up");
window.GoUpShelf();
Check(window.ShelfKey==sourceKey+"|","up returns to source");
Key(window,Key.Escape);
Check(window.ShelfKey==null,"escape at source returns to all books");
window.SelectShelfNode("deleted-node");
Check(window.ShelfKey==null,"unknown key falls back to all books");
```

- [ ] **Step 2: Run and confirm red**

- [ ] **Step 3: Replace the old shelf head with toolbar + hero**

Toolbar row: breadcrumb buttons (last is text), `‹` back button only under 1120 px, sort combo, segmented `平铺 / 分组`. Hero row: eyebrow (`来源 · 名称`), title (source/folder/all books), meta (`N 本 · M 个目录 · P 页`), and a conditional `继续阅读` primary button that opens `ShelfView.ContinueBook`.

- [ ] **Step 4: Implement navigation state**

`RenderShelf` resolves the current key: if `ShelfTree.Find` returns null, walk `ShelfTree.Path` prefixes downward to the nearest surviving ancestor; if none, root. Rebuild `ShelfView` and update sidebar active/highlight. `SelectShelfNode`, `GoUpShelf`, `Esc`, `Backspace`, breadcrumb clicks all route here. Changing `分组/平铺` no longer resets the current folder.

- [ ] **Step 5: Wire search and empty states**

Search text filters within the current node via `ShelfView.Build`; clearing restores the node. Empty states: no books (`把故事，放进书架。`), no search results (with `清空搜索`). Hide the grid when empty.

- [ ] **Step 6: Reader mode hides shelf chrome**

`OpenBook` hides sidebar, toolbar, and hero; `Back` restores them and re-activates the sidebar row. `SetReadingUiHidden` keeps its current header/footer behavior.

- [ ] **Step 7: Run tests + screenshots; commit**

```powershell
git add MainWindow.cs SelfTests.cs
git commit -m "feat: add breadcrumb drill-down and shelf navigation"
```

---

### Task 6: Card and grid redesign

**Files:**
- Modify: `ShelfGrid.cs`
- Modify: `Theme.xaml`
- Modify: `MainWindow.cs`
- Modify: `SelfTests.cs`

**Interfaces:**
- Consumes: `Book.GroupId`, `GroupCount`, `IsCurrentFolderBook`, `ReadPercent`.
- Produces: fixed card bounds 168×288; folder cards in the same grid.

- [ ] **Step 1: Update failing size/group assertions**

```csharp
Check(rectangles.All(r=>r.Width==168&&r.Height==288),"cards use the 2.4 dimensions");
Check(window.ShelfControl.BookCount==window.ShelfItemCount&&window.ShelfItemCount>0,"grid renders every item in the current view");
Check(window.ShelfItemCount>0&&window.ShelfControl.BookCount>0,"folder cards render in the grouped grid");
```

- [ ] **Step 2: Run and confirm red**

- [ ] **Step 3: Rebuild `MakeCard`**

- 168×224 cover via `Rectangle` + `ImageBrush` (`Stretch=UniformToFill`, radius 10).
- Book card: title 13 SemiBold, meta 10.5 with `N 页 · 未读/读至 M 页`, 2 px progress bar when `ReadPercent>0`; `本目录` badge when `IsCurrentFolderBook`.
- Folder card: two offset layers behind the cover, `目录` pill top-left, bottom gradient + `N 本`, title = folder name, caption = `N 本 · 点击进入`.
- Hover: 120 ms translate −2 px + accent border; keyboard focus ring; no heavy drop shadow.
- Placeholder uses `Card` background + `Muted` text.

- [ ] **Step 4: Update grid math**

`cardWidth=168`, `cardHeight=288`, `gap=16`; cover area 224; rows = `cardHeight+18` stride. Preserve anchor-on-resize and viewport-only realization. Ensure group cards reuse the same `CoverKey` cache.

- [ ] **Step 5: Run tests + screenshots at 1220/960/2200; commit**

```powershell
git add ShelfGrid.cs Theme.xaml MainWindow.cs SelfTests.cs
git commit -m "feat: redesign shelf cards and folder cards"
```

---

### Task 7: Localization, English layout, and light theme

**Files:**
- Modify: `localization/strings.json`
- Generate: `L10n.cs`, `android/src/org/leafreader/L10n.java` (via script)
- Modify: `MainWindow.cs`, `ShelfSidebar.cs`, `ShelfGrid.cs` (copy only)
- Modify: `SelfTests.cs`

- [ ] **Step 1: Add the new strings**

Add zh→en entries: `全部书籍/All books`, `来源/Sources`, `子目录/Folders`, `本目录/This folder`, `返回上一级/Up one level`, `继续阅读/Continue reading`, `搜索结果/Search results`, `个目录/folders`, `本 · 点击进入/books · click to open`, `平铺/Grid`, `分组/Groups`, `清空搜索/Clear search`, `当前目录/Current folder`, `路径/Path`.

- [ ] **Step 2: Regenerate catalogs**

```powershell
& 'C:\Users\46842\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe' tools\sync-localization.py
```

Expected: `Generated native catalogs: N entries`; both `L10n.cs` and `L10n.java` updated; no empty values.

- [ ] **Step 3: Add an English layout assertion and screenshot**

Switch language in the self-test, confirm the sidebar/breadcrumb/空状态 labels are not clipped, capture `10-english.png`.

- [ ] **Step 4: Verify light theme screenshot and contrast**

Capture `11-light.png`; check text on `#F3F2ED`, cards, accent button, sidebar selected row.

- [ ] **Step 5: Commit**

```powershell
git add localization/strings.json L10n.cs android/src/org/leafreader/L10n.java MainWindow.cs ShelfSidebar.cs ShelfGrid.cs SelfTests.cs
git commit -m "feat: localize tree shelf and verify light theme"
```

---

### Task 8: Full regression, version, docs, and build

**Files:**
- Modify: `SelfTests.cs` (deep fixture, all new assertions)
- Modify: `Program.cs` (`AssemblyVersion 2.4.0.0`)
- Modify: `README.md`, `README.en.md`, `ARCHITECTURE.md`, `RELEASE_NOTES_DRAFT.md`
- Create: `VALIDATION-2.4.md`
- Replace: `docs/images/windows-bookshelf.png` with the new wide screenshot

- [ ] **Step 1: Expand the self-test fixture to three levels**

`library/01 森林来信/`; `library/系列/02 夜行列车/番外/`; a root-level CBZ; a mixed folder with one image + one child folder. Assert `RelativePath`, tree counts, mixed-folder book + child, drill-down/up, flat-in-node, scoped search, stale-node fallback, resize no-jump, card dims.

- [ ] **Step 2: Run the full Windows suite**

```powershell
& .\build.ps1
$qa='D:\LeafQA\desktop-2.4'
if(Test-Path $qa){Remove-Item $qa -Recurse -Force}
$p=Start-Process ..\LeafReader.exe -ArgumentList '--self-test',$qa -WindowStyle Hidden -PassThru
$p.WaitForExit(); $p.ExitCode
Get-Content "$qa\test-results.txt" | Select-Object -Last 3
```

Expected: exit 0, `RESULT: ALL TESTS PASSED`.

- [ ] **Step 3: Run protocol regression**

```powershell
$qa='D:\LeafQA\protocol-2.4'
if(Test-Path $qa){Remove-Item $qa -Recurse -Force}
$p=Start-Process ..\LeafReader.exe -ArgumentList '--protocol-test',$qa -WindowStyle Hidden -PassThru
$p.WaitForExit(); $p.ExitCode
Get-Content "$qa\protocol-results.txt" | Select-Object -Last 3
```

Expected: exit 0.

- [ ] **Step 4: Inspect screenshots**

Wide dark, narrow dark, light, English, mixed folder, empty state. Fix clipping/overlap before proceeding.

- [ ] **Step 5: Version and docs**

`AssemblyVersion("2.4.0.0")`; README feature list + 2.4 section; ARCHITECTURE “2.4 目录树与桌面 UI”; `VALIDATION-2.4.md` with exact commands/results and Android “source only, not built here”; update the README screenshot.

- [ ] **Step 6: Build release EXE and verify**

```powershell
& .\build.ps1
Get-Item ..\LeafReader.exe | Select-Object Length,LastWriteTime
```

Manually launch, open the QA library, verify drill-down, light/dark, resize.

- [ ] **Step 7: Commit and tag**

```powershell
git add SelfTests.cs Program.cs README.md README.en.md ARCHITECTURE.md RELEASE_NOTES_DRAFT.md VALIDATION-2.4.md docs/images/windows-bookshelf.png
git commit -m "release: Leaf Reader 2.4.0 desktop tree shelf and UI"
git tag -a v2.4.0 -m "Leaf Reader 2.4.0"
```

- [ ] **Step 8: Report Android follow-up**

Do not modify Android app code in this plan. Report that the Android tree plan starts from the same `ShelfTree` semantics and that no APK was built here (no Android SDK/JDK).
