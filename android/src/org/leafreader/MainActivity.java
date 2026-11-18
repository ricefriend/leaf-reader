package org.leafreader;

import android.app.*;
import android.os.*;
import android.content.*;
import android.net.*;
import android.view.*;
import android.widget.*;
import android.graphics.*;
import android.graphics.drawable.*;
import android.content.res.ColorStateList;
import android.content.res.Configuration;
import java.util.*;
import java.util.concurrent.*;
import org.json.*;
import static org.leafreader.Models.*;

public final class MainActivity extends Activity {
 Storage storage;Images images;ThreadPoolExecutor catalog=new ThreadPoolExecutor(1,1,0,TimeUnit.MILLISECONDS,new LinkedBlockingQueue<>());Api.Request listing;int operation;
 List<Book> books=new ArrayList<>();List<Server> servers=new ArrayList<>();LinearLayout root,header,footer,searchRow;TextView status,pageLabel,brandTitle;AlertDialog activePairDialog;GridView shelf;ShelfLayout shelfPane;TextView shelfInfo;ReaderCanvas reader;SeekBar progressBar;Button endJoin;boolean updating,chrome=true,foreground=true;Handler handler=new Handler();
 final Runnable saveReading=()->{if(reader!=null&&reader.book!=null)storage.saveProgress(reader.book,reader.progress);};
 final Runnable hideChrome=new Runnable(){public void run(){if(reader!=null&&reader.book!=null&&foreground){if(storage.prefs.getBoolean("autoHide",true)){if(reader.moving())handler.postDelayed(this,500);else setChrome(false);}}}};
 ConnectivityManager connectivity;ConnectivityManager.NetworkCallback network;
 Button shelfUp;String shelfSource,shelfPath="";HorizontalScrollView breadcrumbScroll;LinearLayout breadcrumb;
 Book openingBook,failedOpenBook;Future<?> openWork;String openStage;long openStarted;int openPage=-1;
 final List<Api.Request> coverRequests=new ArrayList<>();final List<Future<?>> coverWork=new ArrayList<>();
 final Runnable openPulse=new Runnable(){public void run(){handler.removeCallbacks(this);if(openingBook==null||!foreground)return;message(openStage+" · "+openingBook.name+" · "+((SystemClock.uptimeMillis()-openStarted)/1000)+L10n.t(" 秒 · 返回可取消"));handler.postDelayed(this,1000);}};
 void cancelCovers(){generation++;for(Api.Request r:coverRequests)r.cancel();for(Future<?> f:coverWork)f.cancel(true);coverRequests.clear();coverWork.clear();((ThreadPoolExecutor)images.executor).purge();}
 void cancelOpening(){if(openingBook!=null&&listing!=null)listing.cancel();if(openWork!=null)openWork.cancel(true);openWork=null;catalog.purge();openingBook=null;handler.removeCallbacks(openPulse);}
 void reportOpen(int op,Api.Request request,String stage){runOnUiThread(()->{if(op==operation&&!request.cancelled&&openingBook!=null){openStage=stage;openPulse.run();}});}
 void updateImageStatus(){if(reader.book==null)return;String text=reader.imageStatus();message(text==null?reader.book.source+" · "+reader.book.name:text);status.setVisibility(chrome||text!=null?View.VISIBLE:View.GONE);}
 void retryImages(){reader.cancel();reader.errors.clear();reader.resume();updateImageStatus();}
 int generation;String restoreId,uiLanguage;String searchQuery="";boolean leftForeground,pendingLocalRefresh;
 public void onCreate(Bundle state){super.onCreate(state);storage=new Storage(this);uiLanguage=storage.language();L10n.language=uiLanguage;images=new Images(this);try{servers=storage.servers();}catch(Exception ex){toast(ex.getMessage());}shelfSource=state==null?null:state.getString("shelfSource");shelfPath=state==null?"":state.getString("shelfPath");if(shelfPath==null)shelfPath="";restoreId=state==null?storage.prefs.getString("activeBook",""):state.getString("book","");build();Intent intent=getIntent();if(intent.getData()!=null)pairLink(intent.getData().toString());else refresh();
  connectivity=(ConnectivityManager)getSystemService(CONNECTIVITY_SERVICE);network=new ConnectivityManager.NetworkCallback(){public void onLost(Network n){runOnUiThread(()->message(L10n.t("网络已断开；已缓存图片仍可阅读，恢复网络后点重试。")));}public void onAvailable(Network n){runOnUiThread(()->{if(reader.book!=null)message(L10n.t("网络可用 · 点重试可恢复远程图片读取"));});}};try{connectivity.registerDefaultNetworkCallback(network);}catch(Exception ignored){}
 }
 int dp(int n){return (int)(n*getResources().getDisplayMetrics().density+.5f);}
 TextView label(String value,int size){TextView t=new TextView(this);t.setText(value);t.setTextColor(Color.rgb(232,236,240));t.setTextSize(size);return t;}
 GradientDrawable shape(int color,int radius){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(radius));return d;}
 void input(EditText e){e.setTextColor(Color.rgb(231,236,224));e.setHintTextColor(Color.rgb(130,149,135));e.setTextSize(14);e.setPadding(dp(16),dp(8),dp(12),dp(8));e.setBackground(shape(Color.rgb(29,39,32),14));e.setSingleLine(true);}
 Button button(String value,Runnable action){Button b=new Button(this);b.setText(value);b.setAllCaps(false);b.setTextSize(12);b.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));b.setTextColor(Color.rgb(188,218,187));b.setMinWidth(0);b.setMinimumWidth(0);b.setMinHeight(dp(44));b.setMinimumHeight(dp(44));b.setPadding(dp(13),dp(8),dp(13),dp(8));b.setElevation(0);b.setStateListAnimator(null);b.setBackground(new RippleDrawable(ColorStateList.valueOf(Color.rgb(56,77,60)),shape(Color.rgb(28,39,31),12),null));LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-2,dp(44));lp.setMargins(dp(3),dp(3),dp(3),dp(3));b.setLayoutParams(lp);b.setOnClickListener(v->{action.run();restartIdle();});return b;}

 void build(){root=new LinearLayout(this);root.setOrientation(1);root.setBackgroundColor(Color.rgb(16,24,19));root.setFitsSystemWindows(true);setContentView(root);getWindow().setStatusBarColor(Color.rgb(16,24,19));getWindow().setNavigationBarColor(Color.rgb(16,24,19));
  root.setOnApplyWindowInsetsListener((v,insets)->{root.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets.consumeSystemWindowInsets();});
  header=new LinearLayout(this);header.setOrientation(1);header.setPadding(dp(13),dp(10),dp(13),0);LinearLayout top=new LinearLayout(this);top.setGravity(Gravity.CENTER_VERTICAL);brandTitle=label(L10n.t("栞 · Leaf Reader"),23);brandTitle.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));brandTitle.setSingleLine(true);brandTitle.setEllipsize(android.text.TextUtils.TruncateAt.END);top.addView(brandTitle,new LinearLayout.LayoutParams(0,dp(58),1));top.addView(button(L10n.t("＋ 本地"),this::chooseDirectory));top.addView(button(L10n.t("服务器"),this::manageServers));top.addView(button(L10n.pick("设置","Settings"),()->startActivity(new Intent(this,SettingsActivity.class))));header.addView(top);
  searchRow=new LinearLayout(this);searchRow.setOrientation(1);LinearLayout searchTools=new LinearLayout(this);searchTools.setGravity(Gravity.CENTER_VERTICAL);EditText search=new EditText(this);input(search);search.setHint(L10n.t("搜索书名或文件夹"));LinearLayout.LayoutParams sp=new LinearLayout.LayoutParams(0,dp(44),1);sp.setMargins(0,dp(4),dp(7),dp(4));searchTools.addView(search,sp);searchTools.addView(button(L10n.t("刷新"),this::refresh));searchTools.addView(button(L10n.t("排序"),this::sortDialog));searchRow.addView(searchTools);LinearLayout viewTools=new LinearLayout(this);viewTools.addView(button(L10n.t("书架展示"),this::viewDialog));shelfUp=button("‹",this::goUp);shelfUp.setVisibility(View.GONE);viewTools.addView(shelfUp);searchRow.addView(viewTools);breadcrumb=new LinearLayout(this);breadcrumb.setOrientation(LinearLayout.HORIZONTAL);breadcrumb.setGravity(Gravity.CENTER_VERTICAL);breadcrumbScroll=new HorizontalScrollView(this);breadcrumbScroll.setHorizontalScrollBarEnabled(false);breadcrumbScroll.addView(breadcrumb);searchRow.addView(breadcrumbScroll,new LinearLayout.LayoutParams(-1,dp(38)));shelfInfo=label(L10n.t("我的书架"),13);shelfInfo.setTextColor(Color.rgb(148,174,152));shelfInfo.setPadding(dp(3),dp(18),0,dp(5));searchRow.addView(shelfInfo);header.addView(searchRow);root.addView(header);
  shelfPane=new ShelfLayout(this);shelf=shelfPane.grid;root.addView(shelfPane,new LinearLayout.LayoutParams(-1,0,1));
  search.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int start,int before,int count){renderShelf(s.toString());}public void afterTextChanged(android.text.Editable e){}});
  if(reader==null)reader=new ReaderCanvas(this,images);else if(reader.getParent()!=null)((ViewGroup)reader.getParent()).removeView(reader);reader.setVisibility(View.GONE);reader.changed=()->{if(reader.book!=null){handler.removeCallbacks(saveReading);handler.postDelayed(saveReading,400);updateReading();}};reader.toggleChrome=()->setChrome(!chrome);reader.imageStatusChanged=this::updateImageStatus;root.addView(reader,new LinearLayout.LayoutParams(-1,0,1));
  footer=new LinearLayout(this);footer.setOrientation(1);footer.setPadding(dp(8),dp(6),dp(8),dp(5));footer.setBackgroundColor(Color.rgb(20,30,23));LinearLayout actions=new LinearLayout(this);actions.addView(button(L10n.t("‹ 书架"),this::backToShelf));actions.addView(button(L10n.t("模式"),this::modeDialog));actions.addView(button(L10n.t("拼前页"),()->{if(!reader.join(-1))toast(L10n.t("已经是第一页"));}));actions.addView(button(L10n.t("拼后页"),()->{if(!reader.join(1))toast(L10n.t("已经是最后一页"));}));endJoin=button(L10n.t("结束拼接"),()->reader.closeJoin());endJoin.setVisibility(View.GONE);actions.addView(endJoin);actions.addView(button(L10n.t("书签"),this::bookmarkDialog));actions.addView(button(L10n.t("重试"),()->{reader.cancel();reader.errors.clear();reader.resume();message(L10n.t("正在重新请求图片…"));}));footer.addView(new HorizontalScrollView(this){{addView(actions);}});
  LinearLayout nav=new LinearLayout(this);nav.addView(button(L10n.t("上一页"),()->reader.turn(-1)));pageLabel=label("",13);pageLabel.setGravity(Gravity.CENTER);nav.addView(pageLabel,new LinearLayout.LayoutParams(0,dp(46),1));nav.addView(button(L10n.t("下一页"),()->reader.turn(1)));footer.addView(nav);progressBar=new SeekBar(this);progressBar.setProgressTintList(ColorStateList.valueOf(Color.rgb(172,211,170)));progressBar.setThumbTintList(ColorStateList.valueOf(Color.rgb(193,227,187)));progressBar.setProgressBackgroundTintList(ColorStateList.valueOf(Color.rgb(52,67,55)));progressBar.setLayoutDirection(View.LAYOUT_DIRECTION_LTR);progressBar.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar b,int value,boolean user){if(user&&reader.book!=null){int p=reader.progress.mode==2?b.getMax()-value:value;reader.jump(p,true);}}public void onStartTrackingTouch(SeekBar s){handler.removeCallbacks(hideChrome);}public void onStopTrackingTouch(SeekBar s){restartIdle();}});footer.addView(progressBar);footer.setVisibility(View.GONE);root.addView(footer);
  status=label(L10n.t("导入手机目录，或连接电脑共享"),11);status.setTextColor(Color.rgb(136,157,142));status.setSingleLine(true);status.setEllipsize(android.text.TextUtils.TruncateAt.END);status.setPadding(dp(16),dp(8),dp(16),dp(10));status.setOnClickListener(v->{if(failedOpenBook!=null){openBook(failedOpenBook);return;}if(reader.book!=null&&reader.visibleError()){retryImages();return;}new AlertDialog.Builder(this).setTitle(L10n.t("来源与连接状态")).setMessage(status.getText()).setPositiveButton(L10n.t("关闭"),null).show();});root.addView(status);
 }
 void message(String text){if(isFinishing())return;status.setText(text);status.setContentDescription(text);}
 void toast(String text){Toast.makeText(this,text,Toast.LENGTH_LONG).show();}
 void chooseDirectory(){Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION|Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION|Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);startActivityForResult(intent,10);}
 protected void onActivityResult(int code,int result,Intent intent){super.onActivityResult(code,result,intent);if(result!=RESULT_OK||intent==null)return;if(code==10&&intent.getData()!=null){Uri uri=intent.getData();try{getContentResolver().takePersistableUriPermission(uri,Intent.FLAG_GRANT_READ_URI_PERMISSION);storage.addTree(uri);refresh();}catch(Exception ex){toast(L10n.t("目录授权失败，请重新选择目录"));}}else if(code==11)pairLink(intent.getStringExtra("text"));}
 static String localSignature(List<Book> list){StringBuilder sb=new StringBuilder();for(Book b:list){sb.append(b.id).append('|').append(b.name).append('|').append(b.relativePath).append('|').append(b.count);for(Page p:b.pages)sb.append('|').append(p.id).append(':').append(p.version);sb.append('\n');}return sb.toString();}
 void refreshLocalQuietly(){
  if(openingBook!=null||reader.book!=null){pendingLocalRefresh=true;return;}
  if(storage.trees().isEmpty())return;
  final Api.Request request=new Api.Request();final int op=operation;
  catalog.submit(()->{try{
   List<Book> scanned=storage.scanLocal(images,request);
   runOnUiThread(()->{
    if(op!=operation||request.cancelled||isFinishing())return;
    if(openingBook!=null||reader.book!=null){pendingLocalRefresh=true;return;}
    List<Book> currentLocal=new ArrayList<>(),currentRemote=new ArrayList<>();
    for(Book b:books){if(b.server==null)currentLocal.add(b);else currentRemote.add(b);}
    if(localSignature(currentLocal).equals(localSignature(scanned)))return;
    List<Book> merged=new ArrayList<>(scanned);merged.addAll(currentRemote);
    merged.sort((a,b)->NATURAL.compare(a.name,b.name));
    books=merged;renderShelf(searchQuery);
    message(L10n.t("扫描完成 · ")+books.size()+L10n.t(" 本"));
   });
  }catch(Exception ignored){}});
 }
 void refresh(){cancelOpening();failedOpenBook=null;pendingLocalRefresh=false;if(listing!=null)listing.cancel();Api.Request request=new Api.Request();listing=request;int op=++operation;message(L10n.t("正在扫描本地目录和连接已保存服务器…"));List<Server> profiles=new ArrayList<>(servers);catalog.submit(()->{List<Book> result=new ArrayList<>();List<String> errors=new ArrayList<>();try{result.addAll(storage.scanLocal(images,request));errors.addAll(storage.scanWarnings);}catch(Exception ex){errors.add(L10n.t("本地：")+ex.getMessage());}for(Server s:profiles){try{request.check();result.addAll(Api.books(s,request));}catch(Exception ex){errors.add(s.name+"："+connectionError(ex));}}runOnUiThread(()->{if(op!=operation||request.cancelled)return;books=result;renderShelf(searchQuery);message(errors.isEmpty()?L10n.t("已连接 · ")+books.size()+L10n.t(" 本"):L10n.t("部分来源未连接：")+String.join("；",errors));if(!restoreId.isEmpty()){String id=restoreId;restoreId="";for(Book b:books)if(b.id.equals(id)){openBook(b);break;}}});});}
 String connectionError(Exception ex){if(ex instanceof java.net.SocketTimeoutException)return L10n.t("读取超时：目录可能较大或电脑响应缓慢，请重试");if(ex instanceof Api.Failure)return ex.getMessage();return L10n.t("连接中断：检查共享是否开启、地址/端口、电脑休眠或网络，再点刷新重连");}
 void sortDialog(){String[] choices={L10n.t("名称"),L10n.t("最近阅读"),L10n.t("创建时间 · 新的在前"),L10n.t("创建时间 · 旧的在前")};new AlertDialog.Builder(this).setTitle(L10n.t("书架排序")).setSingleChoiceItems(choices,storage.prefs.getInt("sort",0),(d,which)->{storage.prefs.edit().putInt("sort",which).apply();renderShelf(searchQuery);d.dismiss();}).setNegativeButton(L10n.t("关闭"),null).show();}
 void viewDialog(){String[] choices={L10n.t("平铺书架"),L10n.t("按来源分组")};new AlertDialog.Builder(this).setTitle(L10n.t("书架展示")).setSingleChoiceItems(choices,storage.prefs.getBoolean("shelfGrouped",false)?1:0,(d,which)->{storage.prefs.edit().putBoolean("shelfGrouped",which==1).apply();renderShelf(searchQuery);d.dismiss();}).setNegativeButton(L10n.t("关闭"),null).show();}
 void localSources(){List<String> trees=storage.trees();new AlertDialog.Builder(this).setTitle(L10n.t("移除本地来源（不删除图片）")).setItems(trees.toArray(new String[0]),(d,i)->{storage.removeTree(trees.get(i));refresh();}).setNegativeButton(L10n.t("关闭"),null).show();}
 boolean hasRead(Book b){return b.groupId!=null?b.groupRead:storage.prefs.contains("progress:"+b.id);}
 boolean matches(Book b,String query){if(query==null||query.isEmpty())return true;String q=query.toLowerCase(Locale.ROOT);return b.name.toLowerCase(Locale.ROOT).contains(q)||(b.relativePath!=null&&b.relativePath.toLowerCase(Locale.ROOT).contains(q))||(b.rootName!=null&&b.rootName.toLowerCase(Locale.ROOT).contains(q))||(b.source!=null&&b.source.toLowerCase(Locale.ROOT).contains(q));}
 void goUp(){
  if(shelfSource==null)return;
  if(shelfPath!=null&&!shelfPath.isEmpty()){int cut=shelfPath.lastIndexOf('/');shelfPath=cut<0?"":shelfPath.substring(0,cut);}
  else shelfSource=null;
  renderShelf(searchQuery);
 }
 void updateBreadcrumb(List<ShelfNode> roots,ShelfNode node){
  if(breadcrumb==null)return;breadcrumb.removeAllViews();
  addCrumb(L10n.t("书架"),null,"",node==null);
  if(node!=null){List<ShelfNode> trail=Models.path(roots,node.key);for(int i=0;i<trail.size();i++){ShelfNode n=trail.get(i);addCrumb(n.name,n.sourceKey,n.relativePath,i==trail.size()-1);}}
 }
 void addCrumb(String value,final String source,final String path,boolean current){
  TextView text=new TextView(this);text.setText(value);text.setTextSize(12);text.setSingleLine(true);text.setGravity(Gravity.CENTER_VERTICAL);text.setPadding(dp(6),dp(2),dp(6),dp(2));text.setTextColor(current?Color.rgb(232,236,240):Color.rgb(148,174,152));
  if(!current)text.setOnClickListener(v->{shelfSource=source;shelfPath=path==null?"":path;renderShelf(searchQuery);});
  breadcrumb.addView(text);
  if(!current){TextView sep=new TextView(this);sep.setText("›");sep.setTextSize(11);sep.setTextColor(Color.rgb(120,140,125));breadcrumb.addView(sep);}
 }
 void renderShelf(String query){
  cancelCovers();searchQuery=query;int gen=generation;boolean grouped=storage.prefs.getBoolean("shelfGrouped",false);
  List<ShelfNode> roots=Models.tree(books);
  ShelfNode node=null;
  if(shelfSource!=null){
   if(shelfPath==null)shelfPath="";
   node=Models.find(roots,Models.key(shelfSource,shelfPath));
   while(node==null&&!shelfPath.isEmpty()){int cut=shelfPath.lastIndexOf('/');shelfPath=cut<0?"":shelfPath.substring(0,cut);node=Models.find(roots,Models.key(shelfSource,shelfPath));}
   if(node==null){shelfSource=null;shelfPath="";}
  }
  final String source=shelfSource;final String path=shelfPath;final ShelfNode currentNode=node;final List<ShelfNode> treeRoots=roots;
  List<Book> scope=source==null?books:Models.descendants(currentNode);
  List<Book> selected=new ArrayList<>();
  if(!query.isEmpty()||!grouped){for(Book b:scope)if(matches(b,query))selected.add(b);}
  else if(source==null){for(ShelfNode root:treeRoots){Book group=Models.pseudo(root);group.groupRead=Models.descendants(root).stream().anyMatch(x->storage.prefs.contains("progress:"+x.id));selected.add(group);}}
  else{
   List<ShelfNode> folders=new ArrayList<>();List<Book> direct=new ArrayList<>(currentNode.books);
   for(ShelfNode child:currentNode.children){if(!child.children.isEmpty())folders.add(child);else direct.addAll(child.books);}
   for(ShelfNode folder:folders){Book group=Models.pseudo(folder);group.groupRead=Models.descendants(folder).stream().anyMatch(x->storage.prefs.contains("progress:"+x.id));selected.add(group);}
   selected.addAll(direct);
  }
  int sort=storage.prefs.getInt("sort",0);
  selected.sort((a,b)->{if(sort==2||sort==3){int c=Long.compare(a.created,b.created)*(sort==2?-1:1);if(c!=0)return c;}if(sort==1){boolean ar=hasRead(a),br=hasRead(b);if(ar!=br)return ar?-1:1;}return NATURAL.compare(a.name,b.name);});
  shelfUp.setVisibility(source!=null&&!path.isEmpty()?View.VISIBLE:View.GONE);
  updateBreadcrumb(treeRoots,currentNode);
  shelfInfo.setText((query.isEmpty()?(source==null?L10n.t("我的书架"):currentNode.name):L10n.t("搜索结果"))+L10n.t("  ·  ")+selected.size()+(grouped&&source==null?L10n.t(" 个来源"):L10n.t(" 本")));
  shelf.setAdapter(new BaseAdapter(){public int getCount(){return selected.size();}public Object getItem(int p){return selected.get(p);}public long getItemId(int p){return selected.get(p).id.hashCode();}public View getView(int position,View old,android.view.ViewGroup parent){Book b=selected.get(position);LinearLayout card=old instanceof LinearLayout?(LinearLayout)old:new LinearLayout(MainActivity.this);Object previous=card.getTag();if(previous instanceof Api.Request)((Api.Request)previous).cancel();card.removeAllViews();card.setOrientation(1);int width=shelfPane.cardWidth>0?shelfPane.cardWidth:dp(140);int coverHeight=Math.round(width*1.4f);card.setPadding(0,0,0,0);card.setLayoutParams(new AbsListView.LayoutParams(width,coverHeight+dp(62)));card.setBackground(new RippleDrawable(ColorStateList.valueOf(Color.rgb(45,62,47)),shape(Color.TRANSPARENT,12),null));ImageView cover=new ShelfLayout.Cover(MainActivity.this);cover.setBackground(shape(Color.rgb(31,43,34),12));cover.setClipToOutline(true);cover.setScaleType(ImageView.ScaleType.CENTER_CROP);card.addView(cover,new LinearLayout.LayoutParams(-1,coverHeight));TextView title=label((b.groupId!=null?"▸ ":"")+b.name,13);title.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));title.setPadding(dp(1),dp(7),0,0);title.setMaxLines(2);title.setEllipsize(android.text.TextUtils.TruncateAt.END);card.addView(title,new LinearLayout.LayoutParams(-1,dp(42)));TextView sub=label((b.groupId!=null?b.groupCount+L10n.t(" 本 · 点击进入"):(b.server==null?L10n.t("本地"):L10n.t("远程"))+"  ·  "+b.count+L10n.t(" 页")),10);sub.setTextColor(Color.rgb(143,165,146));sub.setSingleLine(true);card.addView(sub,new LinearLayout.LayoutParams(-1,dp(20)));card.setContentDescription(b.name+" · "+(b.groupId!=null?b.groupCount+L10n.t(" 本 · 点击进入"):b.count+L10n.t("页，"))+b.source);Api.Request request=new Api.Request();card.setTag(request);Page page=b.pages.get(0);Bitmap hit=images.memory.get(page.key()+"|thumbnail|256");if(hit!=null)cover.setImageBitmap(hit);else {coverRequests.add(request);coverRequests.removeIf(r->r.cancelled);coverWork.removeIf(Future::isDone);coverWork.add(images.executor.submit(()->{try{Bitmap image=images.preview(page,256,request);runOnUiThread(()->{if(gen==generation&&card.getTag()==request&&!request.cancelled)cover.setImageBitmap(image);});}catch(Exception ignored){}finally{runOnUiThread(()->coverRequests.remove(request));}}));}card.setOnClickListener(v->{if(b.groupId!=null){ShelfNode target=Models.find(treeRoots,b.groupId);if(target!=null){cancelOpening();failedOpenBook=null;shelfSource=target.sourceKey;shelfPath=target.relativePath;renderShelf(searchQuery);}}else openBook(b);});card.setOnLongClickListener(v->{if(b.groupId==null&&b.server==null&&b.pages.size()>1&&b.pages.get(0).entry==null){showBookActions(b);return true;}return false;});return card;}});
 }
 TextView actionRow(String text,int background,int foreground){
  TextView row=new TextView(this);row.setText(text);row.setTextSize(14);row.setTextColor(foreground);row.setGravity(Gravity.CENTER_VERTICAL);row.setPadding(dp(14),0,dp(14),0);row.setMinHeight(dp(48));row.setBackground(new RippleDrawable(ColorStateList.valueOf(Color.rgb(56,77,60)),shape(background,12),null));LinearLayout.LayoutParams lp=new LinearLayout.LayoutParams(-1,dp(48));lp.setMargins(0,dp(6),0,0);row.setLayoutParams(lp);return row;
 }
 void showBookActions(final Book book){
  final Dialog dialog=new Dialog(this);LinearLayout sheet=new LinearLayout(this);sheet.setOrientation(1);sheet.setPadding(dp(20),dp(12),dp(20),dp(18));
  GradientDrawable bg=new GradientDrawable();bg.setColor(Color.rgb(23,37,29));bg.setCornerRadii(new float[]{dp(22),dp(22),dp(22),dp(22),0,0,0,0});sheet.setBackground(bg);
  View handle=new View(this);handle.setBackgroundColor(Color.rgb(72,102,82));LinearLayout.LayoutParams hp=new LinearLayout.LayoutParams(dp(34),dp(4));hp.gravity=Gravity.CENTER_HORIZONTAL;hp.setMargins(0,dp(2),0,dp(12));sheet.addView(handle,hp);
  TextView title=label(book.name,17);title.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));sheet.addView(title);
  TextView sub=label(book.count+L10n.t(" 页")+" · "+(book.server==null?L10n.t("本地"):L10n.t("远程")),11);sub.setTextColor(Color.rgb(143,165,146));sub.setPadding(0,dp(4),0,dp(6));sheet.addView(sub);
  TextView read=actionRow("▶  "+L10n.t("开始阅读"),Color.rgb(185,221,182),Color.rgb(19,32,24));read.setOnClickListener(v->{dialog.dismiss();openBook(book);});sheet.addView(read);
  TextView grid=actionRow("▦  "+L10n.t("平铺查看全部")+" "+book.pages.size()+L10n.t("张图"),Color.rgb(30,48,37),Color.rgb(216,238,221));grid.setOnClickListener(v->{dialog.dismiss();showPageGrid(book);});sheet.addView(grid);
  TextView cancel=actionRow(L10n.t("取消"),Color.TRANSPARENT,Color.rgb(143,165,146));cancel.setGravity(Gravity.CENTER);cancel.setOnClickListener(v->dialog.dismiss());LinearLayout.LayoutParams cp=(LinearLayout.LayoutParams)cancel.getLayoutParams();cp.height=dp(40);cp.setMargins(0,dp(8),0,0);cancel.setLayoutParams(cp);sheet.addView(cancel);
  dialog.setContentView(sheet);dialog.show();Window window=dialog.getWindow();if(window!=null){window.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));window.setGravity(Gravity.BOTTOM);window.setLayout(ViewGroup.LayoutParams.MATCH_PARENT,ViewGroup.LayoutParams.WRAP_CONTENT);window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);window.setDimAmount(.45f);}
 }
 void showPageGrid(final Book book){
  final Dialog dialog=new Dialog(this);dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);
  LinearLayout root=new LinearLayout(this);root.setOrientation(1);root.setBackgroundColor(Color.rgb(16,24,19));
  LinearLayout head=new LinearLayout(this);head.setGravity(Gravity.CENTER_VERTICAL);head.setPadding(dp(18),dp(10),dp(12),dp(8));
  TextView title=label(L10n.t("平铺")+" · "+book.name,18);title.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));head.addView(title,new LinearLayout.LayoutParams(0,dp(50),1));
  head.addView(button(L10n.t("关闭"),dialog::dismiss));root.addView(head);
  GridView grid=new GridView(this);grid.setNumColumns(3);grid.setHorizontalSpacing(dp(8));grid.setVerticalSpacing(dp(10));grid.setPadding(dp(12),dp(2),dp(12),dp(16));grid.setClipToPadding(false);grid.setVerticalScrollBarEnabled(false);root.addView(grid,new LinearLayout.LayoutParams(-1,0,1));
  final List<Api.Request> requests=new ArrayList<>();
  grid.setAdapter(new BaseAdapter(){
   public int getCount(){return book.pages.size();}
   public Object getItem(int p){return book.pages.get(p);}
   public long getItemId(int p){return p;}
   public View getView(int position,View old,ViewGroup parent){
    FrameLayout cell=old instanceof FrameLayout?(FrameLayout)old:new FrameLayout(MainActivity.this);Object tag=cell.getTag();if(tag instanceof Api.Request)((Api.Request)tag).cancel();cell.removeAllViews();
    final Page page=book.pages.get(position);ImageView image=new ShelfLayout.Cover(MainActivity.this);image.setScaleType(ImageView.ScaleType.CENTER_CROP);image.setBackground(shape(Color.rgb(31,43,34),12));image.setClipToOutline(true);cell.addView(image,new FrameLayout.LayoutParams(-1,dp(160)));
    TextView num=label((position+1)+" / "+book.pages.size(),10);num.setTextColor(Color.rgb(225,238,226));num.setGravity(Gravity.CENTER);num.setBackground(shape(Color.argb(190,16,24,19),6));FrameLayout.LayoutParams np=new FrameLayout.LayoutParams(-2,dp(20),Gravity.BOTTOM|Gravity.END);np.setMargins(0,0,dp(6),dp(6));cell.addView(num,np);
    cell.setLayoutParams(new AbsListView.LayoutParams(-1,dp(160)));final int index=position;cell.setOnClickListener(v->{dialog.dismiss();openPage=index;openBook(book);});
    Api.Request request=new Api.Request();cell.setTag(request);requests.add(request);
    Bitmap hit=images.memory.get(page.key()+"|thumbnail|256");if(hit!=null)image.setImageBitmap(hit);else images.executor.submit(()->{try{Bitmap bitmap=images.preview(page,256,request);runOnUiThread(()->{if(cell.getTag()==request&&dialog.isShowing())image.setImageBitmap(bitmap);});}catch(Exception ignored){}});
    return cell;
   }
  });
  dialog.setOnDismissListener(d->{for(Api.Request r:requests)r.cancel();});dialog.setContentView(root);dialog.show();Window window=dialog.getWindow();if(window!=null){window.setBackgroundDrawable(new ColorDrawable(Color.rgb(16,24,19)));window.setLayout(ViewGroup.LayoutParams.MATCH_PARENT,ViewGroup.LayoutParams.MATCH_PARENT);}
 }
 void openBook(Book book){
  if(openingBook!=null&&openingBook.id.equals(book.id)){openPulse.run();return;}
  int targetPage=openPage;openPage=-1;cancelOpening();failedOpenBook=null;if(listing!=null)listing.cancel();cancelCovers();Api.Request request=new Api.Request();listing=request;int op=++operation;openingBook=book;openStarted=SystemClock.uptimeMillis();openStage=L10n.t("等待打开目录");openPulse.run();
  openWork=catalog.submit(()->{try{
   request.check();reportOpen(op,request,L10n.t("正在读取图片列表，首次打开可能较慢"));Api.loadPages(book,request);
   int count=0;for(Page page:book.pages){request.check();if(!page.metadata){if(count++%20==0)reportOpen(op,request,L10n.t("正在读取图片尺寸"));try{images.info(page,request);}catch(Exception ex){request.check();if(page.server==null){page.width=800;page.height=1200;}}}}
   request.check();runOnUiThread(()->{if(op!=operation||request.cancelled||isFinishing())return;openingBook=null;openWork=null;handler.removeCallbacks(openPulse);shelfPane.setVisibility(View.GONE);shelf.setVisibility(View.GONE);searchRow.setVisibility(View.GONE);brandTitle.setText(book.name);reader.setVisibility(View.VISIBLE);footer.setVisibility(View.VISIBLE);storage.prefs.edit().putString("activeBook",book.id).apply();reader.open(book,storage.progress(book));if(targetPage>=0)reader.jump(targetPage,true);awake();setChrome(true);updateImageStatus();});
  }catch(Exception ex){runOnUiThread(()->{if(op!=operation||request.cancelled||isFinishing())return;openingBook=null;openWork=null;failedOpenBook=book;handler.removeCallbacks(openPulse);message(L10n.t("打开失败 · ")+book.name+" · "+connectionError(ex)+L10n.t(" · 点此重试"));renderShelf(searchQuery);});}});
 }
 void updateReading(){if(reader.book==null)return;endJoin.setVisibility(reader.joined>=0?View.VISIBLE:View.GONE);updating=true;int n=Models.count(reader.book,reader.progress.page,reader.progress.mode,reader.progress.anchor,reader.cover,reader.wide);if(reader.joined>=0){n=2;}int shown=reader.joined>=0?reader.joined:reader.progress.page;pageLabel.setText((shown+1)+(n==2?"–"+(shown+2):"")+" / "+reader.book.pages.size());progressBar.setMax(reader.book.pages.size()-1);progressBar.setProgress(reader.progress.mode==2?progressBar.getMax()-reader.progress.page:reader.progress.page);updating=false;updateImageStatus();}
 void restartIdle(){handler.removeCallbacks(hideChrome);if(reader!=null&&reader.book!=null&&chrome&&foreground&&storage.prefs.getBoolean("autoHide",true))handler.postDelayed(hideChrome,3000);}
 void setChrome(boolean show){chrome=show;header.setVisibility(show?View.VISIBLE:View.GONE);footer.setVisibility(reader.book!=null&&show?View.VISIBLE:View.GONE);status.setVisibility(show||reader.book!=null&&reader.imageStatus()!=null?View.VISIBLE:View.GONE);restartIdle();}
 void modeDialog(){handler.removeCallbacks(hideChrome);new AlertDialog.Builder(this).setTitle(L10n.t("阅读模式")).setSingleChoiceItems(new String[]{L10n.t("单页"),L10n.t("双页 · 左至右"),L10n.t("双页 · 右至左"),L10n.t("纵向连续 · 默认")},reader.progress.mode,(d,which)->{reader.mode(which);d.dismiss();restartIdle();}).setNegativeButton(L10n.t("关闭"),(d,w)->restartIdle()).show();}
 void bookmarkDialog(){handler.removeCallbacks(hideChrome);Book b=reader.book;String id=b.pages.get(reader.progress.page).id;List<Integer> marked=new ArrayList<>();List<String> labels=new ArrayList<>();labels.add(reader.progress.bookmarks.contains(id)?L10n.t("取消当前页书签"):L10n.t("标记当前页"));for(int i=0;i<b.pages.size();i++)if(reader.progress.bookmarks.contains(b.pages.get(i).id)){marked.add(i);labels.add(L10n.t("第 ")+(i+1)+L10n.t(" 页 · ")+b.pages.get(i).name);}new AlertDialog.Builder(this).setTitle(L10n.t("书签")).setItems(labels.toArray(new String[0]),(d,i)->{if(i==0){if(!reader.progress.bookmarks.remove(id))reader.progress.bookmarks.add(id);storage.saveProgress(b,reader.progress);}else reader.jump(marked.get(i-1),true);restartIdle();}).setNegativeButton(L10n.t("关闭"),(d,w)->restartIdle()).show();}
 void backToShelf(){cancelOpening();failedOpenBook=null;if(reader.book!=null){storage.saveProgress(reader.book,reader.progress);reader.stopMotion();reader.cancel();reader.book=null;awake();reader.previews.clear();reader.tiles.clear();}storage.prefs.edit().remove("activeBook").apply();handler.removeCallbacks(hideChrome);reader.setVisibility(View.GONE);shelfPane.setVisibility(View.VISIBLE);shelf.setVisibility(View.VISIBLE);searchRow.setVisibility(View.VISIBLE);brandTitle.setText(L10n.t("栞 · Leaf Reader"));chrome=true;header.setVisibility(View.VISIBLE);footer.setVisibility(View.GONE);status.setVisibility(View.VISIBLE);renderShelf(searchQuery);message(L10n.t("书架 · 阅读进度已保存"));if(pendingLocalRefresh){pendingLocalRefresh=false;refreshLocalQuietly();}}
 public void onBackPressed(){if(openingBook!=null){cancelOpening();++operation;renderShelf(searchQuery);message(L10n.t("已取消打开目录"));return;}if(reader.book!=null&&reader.closeJoin())return;if(reader.book!=null)backToShelf();else if(shelfSource!=null){goUp();}else super.onBackPressed();}
 protected void onSaveInstanceState(Bundle state){super.onSaveInstanceState(state);state.putString("shelfSource",shelfSource);state.putString("shelfPath",shelfPath);state.putString("book",reader.book==null?"":reader.book.id);if(reader.book!=null)storage.saveProgress(reader.book,reader.progress);}
 public void onConfigurationChanged(Configuration config){super.onConfigurationChanged(config);if(reader.book!=null)storage.saveProgress(reader.book,reader.progress);else shelfPane.requestLayout();}
 protected void onStop(){super.onStop();if(!isChangingConfigurations())leftForeground=true;}
 protected void onPause(){foreground=false;handler.removeCallbacks(openPulse);cancelCovers();handler.removeCallbacks(hideChrome);if(reader!=null&&reader.book!=null){storage.saveProgress(reader.book,reader.progress);reader.pause();}super.onPause();}
 protected void onResume(){super.onResume();foreground=true;if(storage==null||reader==null)return;String language=storage.language();L10n.language=language;if(!language.equals(uiLanguage)){uiLanguage=language;Book reading=reader.book;boolean wasChrome=chrome;String query=searchQuery;generation++;build();for(Book book:books)if(book.server==null)book.source=L10n.t(book.id.startsWith("local-zip:")?L10n.t("手机本地 · 压缩包"):L10n.t("手机本地"));renderShelf(query);if(reading!=null){shelfPane.setVisibility(View.GONE);shelf.setVisibility(View.GONE);searchRow.setVisibility(View.GONE);reader.setVisibility(View.VISIBLE);brandTitle.setText(reading.name);updateReading();setChrome(wasChrome);}}
  reader.cover=storage.prefs.getBoolean("cover",true);reader.wide=storage.prefs.getBoolean("wide",true);if(storage.prefs.getBoolean("cacheCleared",false)){storage.prefs.edit().remove("cacheCleared").apply();images.clear();reader.cancel();reader.previews.clear();reader.tiles.clear();}if(storage.prefs.getBoolean("sourcesChanged",false)){storage.prefs.edit().remove("sourcesChanged").apply();servers=storage.servers();if(reader.book!=null){reader.progress=storage.progress(reader.book);backToShelf();}refresh();}else if(leftForeground){leftForeground=false;refreshLocalQuietly();}if(reader.book!=null){reader.resume();updateImageStatus();if(!storage.prefs.getBoolean("autoHide",true))setChrome(true);restartIdle();}else if(openingBook!=null){handler.removeCallbacks(openPulse);openPulse.run();}else renderShelf(searchQuery);awake();}
 void awake(){if(reader.book!=null&&storage.prefs.getBoolean("keepAwake",true))getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);else getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);}

 protected void onDestroy(){cancelOpening();cancelCovers();if(listing!=null)listing.cancel();if(reader!=null)reader.cancel();handler.removeCallbacksAndMessages(null);try{connectivity.unregisterNetworkCallback(network);}catch(Exception ignored){}catalog.shutdownNow();if(images!=null)images.close();super.onDestroy();}
 protected void onNewIntent(Intent intent){super.onNewIntent(intent);if(intent.getData()!=null)pairLink(intent.getData().toString());}
 void pairLink(String text){try{Uri uri=Uri.parse(text);if(!"leafreader".equals(uri.getScheme())||!"pair".equals(uri.getHost()))throw new Exception();pairDialog(uri.getQueryParameter("address"),uri.getQueryParameter("code"));}catch(Exception ex){toast(L10n.t("二维码不是 Leaf Reader 配对信息"));}}
 void pairDialog(String endpoint,String pairing){LinearLayout box=new LinearLayout(this);box.setOrientation(1);box.setPadding(dp(20),0,dp(20),0);EditText address=new EditText(this);input(address);address.setHint("http://192.168.1.10:8765");address.setSingleLine(true);address.setText(endpoint==null?"":endpoint);box.addView(address);EditText code=new EditText(this);input(code);code.setHint(L10n.t("配对码"));code.setSingleLine(true);code.setText(pairing==null?"":pairing);box.addView(code);
  activePairDialog=new AlertDialog.Builder(this).setTitle(L10n.t("配对服务器")).setView(box).setPositiveButton(L10n.t("连接"),(d,w)->{cancelOpening();failedOpenBook=null;Api.Request request=new Api.Request();int op=++operation;if(listing!=null)listing.cancel();listing=request;message(L10n.t("正在配对…"));catalog.submit(()->{try{Server s=Api.pair(address.getText().toString().trim(),code.getText().toString().trim(),request);runOnUiThread(()->{if(op!=operation||request.cancelled)return;servers.removeIf(p->p.id.equals(s.id));servers.add(s);try{storage.saveServers(servers);refresh();}catch(Exception ex){message(L10n.t("凭据保存失败，请重试"));}});}catch(Exception ex){runOnUiThread(()->{if(op==operation&&!request.cancelled)message(connectionError(ex));});}});}).setNegativeButton(L10n.t("取消"),null).show();
 }
 void manageServers(){handler.removeCallbacks(hideChrome);List<String> items=new ArrayList<>();items.add(L10n.t("＋ 输入地址配对"));items.add(L10n.t("扫码配对"));for(Server s:servers)items.add(s.name+" · "+s.address);
  new AlertDialog.Builder(this).setTitle(L10n.t("服务器管理")).setItems(items.toArray(new String[0]),(d,i)->{if(i==0)pairDialog(null,null);else if(i==1)startActivityForResult(new Intent(this,ScannerActivity.class),11);else{Server s=servers.get(i-2);new AlertDialog.Builder(this).setTitle(s.name).setItems(new String[]{L10n.t("重新连接"),L10n.t("浏览授权目录"),L10n.t("移除服务器")},(x,which)->{if(which==0)refresh();else if(which==1)authorizedRoots(s);else{servers.remove(s);books.removeIf(b->b.server!=null&&b.server.id.equals(s.id));try{storage.saveServers(servers);}catch(Exception ex){toast(L10n.t("凭据保存失败"));}renderShelf(searchQuery);}}).show();}}).setNegativeButton(L10n.t("关闭"),(d,w)->restartIdle()).show();
 }
 void authorizedRoots(Server s){Api.Request request=new Api.Request();catalog.submit(()->{try{JSONArray roots=Api.json(s,"/v1/roots",null,request).getJSONArray("roots");String[] names=new String[roots.length()];for(int i=0;i<names.length;i++)names[i]=roots.getJSONObject(i).getString("name");runOnUiThread(()->new AlertDialog.Builder(this).setTitle(L10n.t("授权目录 · ")+s.name).setItems(names,(d,index)->{try{String rootId=roots.getJSONObject(index).getString("id");storage.prefs.edit().putBoolean("shelfGrouped",true).apply();shelfSource="remote:"+s.id+":"+rootId;shelfPath="";renderShelf(searchQuery);message(L10n.t("正在浏览 · ")+names[index]);}catch(Exception ex){toast(L10n.t("目录列表已改变，请刷新"));}}).setNegativeButton(L10n.t("关闭"),null).show());}catch(Exception ex){runOnUiThread(()->message(connectionError(ex)));}});}
}
