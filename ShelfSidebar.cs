using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LeafReader {
 public sealed class ShelfSidebar : Grid {
  public Action<string> Selected;
  public Action ManageSources;
  public Action Refresh;
  readonly StackPanel content=new StackPanel();
  readonly ScrollViewer scroll;
  readonly Border allRow;
  readonly Dictionary<string,NodeRow> rows=new Dictionary<string,NodeRow>(StringComparer.OrdinalIgnoreCase);
  List<ShelfNode> roots=new List<ShelfNode>();
  string active;
  int rowCount;
  sealed class NodeRow {
   public ShelfNode Node;public Border Border;public StackPanel Children;public TextBlock Caret;public bool Expanded;public bool Built;public int Depth;
  }
  public ShelfSidebar(){
   Width=248;SetResourceReference(Panel.BackgroundProperty,"Side");
   RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});
   RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var header=new TextBlock{Text=L10n.T("书架"),FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new Thickness(18,18,14,10)};
   header.SetResourceReference(TextBlock.ForegroundProperty,"Muted");Children.Add(header);
   scroll=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
   Grid.SetRow(scroll,1);Children.Add(scroll);
   allRow=MakeAllRow();
   var footer=new StackPanel{Margin=new Thickness(12,10,12,12)};
   footer.Children.Add(Line());
   var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};
   actions.Children.Add(Link(L10n.T("管理来源"),delegate{if(ManageSources!=null)ManageSources();}));
   actions.Children.Add(Link(L10n.T("刷新"),delegate{if(Refresh!=null)Refresh();}));
   footer.Children.Add(actions);Grid.SetRow(footer,2);Children.Add(footer);
   var edge=new Border{Width=1,HorizontalAlignment=HorizontalAlignment.Right,IsHitTestVisible=false};edge.SetResourceReference(Border.BackgroundProperty,"Line");Grid.SetRowSpan(edge,3);Children.Add(edge);
   SetTree(new List<ShelfNode>());
  }
  static Brush Res(FrameworkElement element,string key){return (Brush)element.FindResource(key);}
  StackPanel Line(){var line=new StackPanel{Height=1};line.SetResourceReference(Panel.BackgroundProperty,"Line");return line;}
  Button Link(string text,Action action){
   var button=new Button{Content=text,FontSize=11,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,4,0),Padding=new Thickness(8,5,8,5)};
   button.Click+=(s,e)=>action();return button;
  }
  Border MakeAllRow(){
   var border=new Border{Height=32,Margin=new Thickness(8,2,8,2),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(2,0,0,0),BorderBrush=Brushes.Transparent,Cursor=Cursors.Hand,ToolTip=L10n.T("全部书籍")};
   border.Background=Brushes.Transparent;
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var mark=new TextBlock{Text="▤",FontSize=10,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
   mark.SetResourceReference(TextBlock.ForegroundProperty,"Muted");grid.Children.Add(mark);
   var label=new TextBlock{Text=L10n.T("全部书籍"),FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,8,0)};
   label.SetResourceReference(TextBlock.ForegroundProperty,"Fg");Grid.SetColumn(label,1);grid.Children.Add(label);
   var count=new TextBlock{FontSize=10.5,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};
   count.SetResourceReference(TextBlock.ForegroundProperty,"Muted");Grid.SetColumn(count,2);grid.Children.Add(count);
   border.Child=grid;border.MouseLeftButtonUp+=(s,e)=>{Choose(null);};border.MouseEnter+=(s,e)=>Hover(border,false);border.MouseLeave+=(s,e)=>Hover(border,true);
   return border;
  }
  void Hover(Border border,bool leaving){
   if(border==allRow&&active==null){border.SetResourceReference(Panel.BackgroundProperty,"AccentSoft");return;}
   if(leaving)RefreshActive();else border.SetResourceReference(Panel.BackgroundProperty,"Hover");
  }
  public void SetTree(List<ShelfNode> tree){
   roots=tree??new List<ShelfNode>();content.Children.Clear();rows.Clear();rowCount=0;
   content.Children.Add(allRow);rowCount++;
   var total=(TextBlock)((Grid)allRow.Child).Children[2];total.Text=roots.Sum(r=>r.Count).ToString();
   var section=new TextBlock{Text=L10n.T("来源"),FontSize=9.5,FontWeight=FontWeights.SemiBold,Margin=new Thickness(20,16,12,6)};
   section.SetResourceReference(TextBlock.ForegroundProperty,"Muted");if(roots.Count>0)content.Children.Add(section);
   foreach(ShelfNode root in roots)AddNode(root,0,content);
   if(active!=null)SetActive(active);else RefreshActive();
  }
  static bool HasFolders(ShelfNode node){foreach(ShelfNode child in node.Children)if(child.Children.Count>0)return true;return false;}
  Border AddNode(ShelfNode node,int depth,Panel parent){
   var border=new Border{Height=32,CornerRadius=new CornerRadius(8),Margin=new Thickness(8,0,8,0),BorderThickness=new Thickness(2,0,0,0),BorderBrush=Brushes.Transparent,Cursor=Cursors.Hand,ToolTip=node.RelativePath};
   border.Background=Brushes.Transparent;
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var caret=new TextBlock{Text=HasFolders(node)?"›":"",FontSize=11,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
   caret.SetResourceReference(TextBlock.ForegroundProperty,"Muted");grid.Children.Add(caret);
   var label=new TextBlock{Text=node.Name,FontSize=12,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(2,0,8,0),FontWeight=node.IsSource?FontWeights.SemiBold:FontWeights.Normal};
   label.SetResourceReference(TextBlock.ForegroundProperty,"Fg");Grid.SetColumn(label,1);grid.Children.Add(label);
   var count=new TextBlock{Text=node.Count.ToString(),FontSize=10.5,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};
   count.SetResourceReference(TextBlock.ForegroundProperty,"Muted");Grid.SetColumn(count,2);grid.Children.Add(count);
   border.Child=grid;
   border.Padding=new Thickness(0);
   border.Margin=new Thickness(8+depth*14,0,8,0);
   border.MouseLeftButtonUp+=(s,e)=>{Choose(node.Key);};
   border.MouseEnter+=(s,e)=>Hover(border,false);border.MouseLeave+=(s,e)=>Hover(border,true);
   parent.Children.Add(border);
   var children=new StackPanel{Visibility=Visibility.Collapsed};
   parent.Children.Add(children);
   var row=new NodeRow{Node=node,Border=border,Children=children,Caret=caret,Depth=depth};
   rows[node.Key]=row;rowCount++;
   if(HasFolders(node)){
    caret.MouseLeftButtonUp+=(s,e)=>{e.Handled=true;Toggle(row);};
    caret.Cursor=Cursors.Hand;
   }
   return border;
  }
  void Toggle(NodeRow row){
   if(!row.Built){foreach(ShelfNode child in row.Node.Children)if(child.Children.Count>0)AddNode(child,row.Depth+1,row.Children);row.Built=true;}
   row.Expanded=!row.Expanded;row.Children.Visibility=row.Expanded?Visibility.Visible:Visibility.Collapsed;
   row.Caret.Text=row.Expanded?"⌄":"›";
  }
  void Choose(string key){
   active=key;SetActive(key);if(Selected!=null)Selected(key);
  }
  public void SetActive(string key){
   active=key;RefreshActive();
   if(key==null){allRow.BringIntoView();return;}
   List<ShelfNode> path=ShelfTree.Path(roots,key);
   if(path.Count==0)return;
   NodeRow parentRow=null;
   for(int i=0;i<path.Count-1;i++){
    NodeRow row;if(!rows.TryGetValue(path[i].Key,out row))break;
    if(!row.Expanded)Toggle(row);
    parentRow=row;
   }
   NodeRow target;if(rows.TryGetValue(path[path.Count-1].Key,out target))target.Border.BringIntoView();
   RefreshActive();
  }
  void RefreshActive(){
   if(active==null)allRow.SetResourceReference(Panel.BackgroundProperty,"AccentSoft");else allRow.Background=Brushes.Transparent;
   var allLabel=(TextBlock)((Grid)allRow.Child).Children[1];allLabel.SetResourceReference(TextBlock.ForegroundProperty,active==null?"Accent":"Fg");
   foreach(KeyValuePair<string,NodeRow> pair in rows){
    bool on=active!=null&&string.Equals(pair.Key,active,StringComparison.OrdinalIgnoreCase);
    if(on){pair.Value.Border.SetResourceReference(Panel.BackgroundProperty,"AccentSoft");pair.Value.Border.SetResourceReference(Border.BorderBrushProperty,"Accent");}
    else{pair.Value.Border.Background=Brushes.Transparent;pair.Value.Border.BorderBrush=Brushes.Transparent;}
    var label=(TextBlock)((Grid)pair.Value.Border.Child).Children[1];
    label.SetResourceReference(TextBlock.ForegroundProperty,on?"Accent":"Fg");
   }
  }
  internal int RowCount{get{return rowCount;}}
  internal bool IsExpanded(string key){NodeRow row;return rows.TryGetValue(key,out row)&&row.Expanded;}
 }
}
