package org.leafreader;

import android.content.*;
import android.database.Cursor;
import android.net.Uri;
import android.provider.DocumentsContract;
import android.security.keystore.*;
import android.util.Base64;
import org.json.*;
import java.util.*;
import java.io.*;
import java.security.*;
import javax.crypto.*;
import javax.crypto.spec.GCMParameterSpec;
import java.util.zip.*;
import static org.leafreader.Models.*;

final class Storage {
 final List<String> scanWarnings=new ArrayList<>();
 final Context context;final android.content.SharedPreferences prefs;
 Storage(Context c){context=c;prefs=c.getSharedPreferences("leafreader",Context.MODE_PRIVATE);}
 List<String> trees(){return new ArrayList<>(prefs.getStringSet("trees",new HashSet<>()));}
 void addTree(Uri uri){Set<String> set=new HashSet<>(trees());set.add(uri.toString());prefs.edit().putStringSet("trees",set).apply();}
 void removeTree(String uri){Set<String> set=new HashSet<>(trees());set.remove(uri);prefs.edit().putStringSet("trees",set).apply();try{context.getContentResolver().releasePersistableUriPermission(Uri.parse(uri),Intent.FLAG_GRANT_READ_URI_PERMISSION);}catch(Exception ignored){}}
 javax.crypto.SecretKey key()throws Exception{KeyStore store=KeyStore.getInstance("AndroidKeyStore");store.load(null);if(!store.containsAlias("leaf.credentials")){KeyGenerator g=KeyGenerator.getInstance("AES","AndroidKeyStore");g.init(new KeyGenParameterSpec.Builder("leaf.credentials",KeyProperties.PURPOSE_ENCRYPT|KeyProperties.PURPOSE_DECRYPT).setBlockModes(KeyProperties.BLOCK_MODE_GCM).setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());g.generateKey();}return (javax.crypto.SecretKey)store.getKey("leaf.credentials",null);}
 String encrypt(String text)throws Exception{Cipher c=Cipher.getInstance("AES/GCM/NoPadding");c.init(Cipher.ENCRYPT_MODE,key());return Base64.encodeToString(c.getIV(),Base64.NO_WRAP)+":"+Base64.encodeToString(c.doFinal(text.getBytes("UTF-8")),Base64.NO_WRAP);}
 String decrypt(String text)throws Exception{String[] parts=text.split(":");Cipher c=Cipher.getInstance("AES/GCM/NoPadding");c.init(Cipher.DECRYPT_MODE,key(),new GCMParameterSpec(128,Base64.decode(parts[0],Base64.NO_WRAP)));return new String(c.doFinal(Base64.decode(parts[1],Base64.NO_WRAP)),"UTF-8");}
 List<Server> servers(){List<Server> list=new ArrayList<>();try{String saved=prefs.getString("servers","");if(saved.isEmpty())return list;JSONArray a=new JSONArray(decrypt(saved));for(int i=0;i<a.length();i++){JSONObject o=a.getJSONObject(i);Server s=new Server();s.id=o.getString("id");s.name=o.getString("name");s.address=o.getString("address");s.token=o.getString("token");list.add(s);}}catch(Exception e){throw new IllegalStateException(L10n.t("保存的服务器凭据无法解密，请移除应用数据后重新配对"),e);}return list;}
 void saveServers(List<Server> list)throws Exception{JSONArray a=new JSONArray();for(Server s:list)a.put(s.json());prefs.edit().putString("servers",encrypt(a.toString())).apply();}
 Progress progress(Book b){Progress p=Progress.parse(prefs.getString("progress:"+b.id,"{}"));if(!prefs.contains("progress:"+b.id))p.mode=prefs.getInt("defaultMode",3);p.restore(b);return p;}
 void saveProgress(Book b,Progress p){if(b.pages.isEmpty())return;p.pageId=b.pages.get(p.page).id;p.anchorId=b.pages.get(Math.max(0,Math.min(b.pages.size()-1,p.anchor))).id;try{prefs.edit().putString("progress:"+b.id,p.json().toString()).apply();}catch(Exception ignored){}}
 String language(){String stored=prefs.getString("language","");return stored.equals("zh")||stored.equals("en")?stored:Locale.getDefault().getLanguage().equals("zh")?"zh":"en";}
 JSONObject backupState()throws Exception{JSONObject state=new JSONObject().put("format","leaf-reader-state").put("version",1);JSONObject options=new JSONObject();for(String k:new String[]{"language","sort","shelfGrouped","autoHide","cover","wide","keepAwake","defaultMode"}){Object v=prefs.getAll().get(k);if(v!=null)options.put(k,v);}JSONObject progress=new JSONObject();for(Map.Entry<String,?> e:prefs.getAll().entrySet())if(e.getKey().startsWith("progress:")&&e.getValue() instanceof String)progress.put(e.getKey().substring(9),new JSONObject((String)e.getValue()));JSONArray profiles=new JSONArray();for(Server server:servers())profiles.put(server.json());return state.put("options",options).put("progress",progress).put("servers",profiles);}
 void restoreState(JSONObject state)throws Exception{
  if(!"leaf-reader-state".equals(state.getString("format"))||state.getInt("version")!=1)throw new IOException(L10n.pick("不支持的备份版本","Unsupported backup version"));JSONObject options=state.getJSONObject("options"),progress=state.getJSONObject("progress");JSONArray profiles=state.getJSONArray("servers");if(progress.length()>50000||profiles.length()>100)throw new IOException(L10n.pick("备份条目过多","Too many backup entries"));Map<String,Server> merged=new LinkedHashMap<>();for(Server server:servers())merged.put(server.id,server);
  for(int i=0;i<profiles.length();i++){JSONObject o=profiles.getJSONObject(i);Server server=new Server();server.id=o.getString("id");server.name=o.getString("name");server.address=o.getString("address");server.token=o.getString("token");java.net.URI uri=new java.net.URI(server.address);if(server.id.isEmpty()||server.id.length()>128||server.name.length()>1024||server.token.length()>1024||!("http".equals(uri.getScheme())||"https".equals(uri.getScheme()))||uri.getHost()==null||uri.getUserInfo()!=null||uri.getQuery()!=null||uri.getFragment()!=null||!(uri.getPath().isEmpty()||uri.getPath().equals("/")))throw new IOException(L10n.pick("备份中的服务器信息无效","Invalid server profile in backup"));merged.put(server.id,server);}
  android.content.SharedPreferences.Editor editor=prefs.edit();Iterator<String> keys=progress.keys();while(keys.hasNext()){String id=keys.next();if(id.isEmpty()||id.length()>16384)throw new IOException(L10n.pick("备份中的书籍标识无效","Invalid book identity in backup"));JSONObject record=progress.getJSONObject(id);if(record.has("mode")&&(record.getInt("mode")<0||record.getInt("mode")>3)||record.has("fraction")&&(record.getDouble("fraction")<0||record.getDouble("fraction")>1||Double.isNaN(record.getDouble("fraction"))))throw new IOException(L10n.t("备份中的阅读记录无效"));Progress p=Progress.parse(record.toString());if(p.mode<0||p.mode>3||p.page<0||p.bookmarks.size()>10000||Float.isNaN(p.fraction)||Float.isInfinite(p.fraction))throw new IOException(L10n.pick("备份中的阅读记录无效","Invalid reading progress in backup"));editor.putString("progress:"+id,p.json().toString());}
  if(options.has("language")){String language=options.getString("language");if(!language.equals("zh")&&!language.equals("en"))throw new IOException(L10n.pick("备份语言无效","Invalid backup language"));editor.putString("language",language);}for(String k:new String[]{"shelfGrouped","autoHide","cover","wide","keepAwake"})if(options.has(k))editor.putBoolean(k,options.getBoolean(k));for(String k:new String[]{"sort","defaultMode"})if(options.has(k)){int v=options.getInt(k);if(v<0||v>3)throw new IOException(L10n.pick("备份设置无效","Invalid backup settings"));editor.putInt(k,v);}
  JSONArray encryptedProfiles=new JSONArray();for(Server server:merged.values())encryptedProfiles.put(server.json());editor.putString("servers",encrypt(encryptedProfiles.toString()));if(!editor.commit())throw new IOException(L10n.pick("备份恢复写入失败","Cannot save restored backup"));
 }
 List<Book> scanLocal(Images images,Api.Request request)throws Exception{scanWarnings.clear();List<Book> result=new ArrayList<>();Set<String> visited=new HashSet<>();for(String root:trees()){try{
  Uri tree=Uri.parse(root);String rootName=DocumentsContract.getTreeDocumentId(tree);try(Cursor c=context.getContentResolver().query(DocumentsContract.buildDocumentUriUsingTree(tree,rootName),new String[]{DocumentsContract.Document.COLUMN_DISPLAY_NAME},null,null,null)){if(c!=null&&c.moveToFirst())rootName=c.getString(0);}ArrayDeque<String> pending=new ArrayDeque<>();pending.add(DocumentsContract.getTreeDocumentId(tree));
  while(!pending.isEmpty()){request.check();String directory=pending.remove();String key=root+"|"+directory;if(!visited.add(key))continue;Uri doc=DocumentsContract.buildDocumentUriUsingTree(tree,directory);Book book=new Book();book.id="local:"+key;book.name=directory.substring(directory.lastIndexOf('/')+1);book.source=L10n.t("手机本地");book.rootId=root;book.rootName=rootName;
   Uri children=DocumentsContract.buildChildDocumentsUriUsingTree(tree,directory);String[] columns={DocumentsContract.Document.COLUMN_DOCUMENT_ID,DocumentsContract.Document.COLUMN_DISPLAY_NAME,DocumentsContract.Document.COLUMN_MIME_TYPE,DocumentsContract.Document.COLUMN_LAST_MODIFIED,DocumentsContract.Document.COLUMN_SIZE};
   try(Cursor cursor=context.getContentResolver().query(children,columns,null,null,null)){
    if(cursor==null)throw new IOException(L10n.t("目录授权不可用，请重新选择文件夹"));
    while(cursor.moveToNext()){request.check();String id=cursor.getString(0),name=cursor.getString(1),mime=cursor.getString(2);Uri uri=DocumentsContract.buildDocumentUriUsingTree(tree,id);
     if(DocumentsContract.Document.MIME_TYPE_DIR.equals(mime)){pending.add(id);continue;}
     String version=cursor.getLong(3)+"-"+cursor.getLong(4);if(Models.image(name)){Page p=new Page();p.uri=uri.toString();p.id=root+"|"+id;p.name=name;p.version=version;book.pages.add(p);}else if(Models.zip(name)){try{Book archive=zipBook(root,uri,name,version,images,request);archive.rootName=rootName;if(!archive.pages.isEmpty())result.add(archive);}catch(InterruptedIOException ex){throw ex;}catch(Exception ex){scanWarnings.add(L10n.t("压缩包不可读取：")+name+L10n.t("；其他书籍仍可阅读。"));}}
    }
   }
   try(Cursor c=context.getContentResolver().query(doc,new String[]{DocumentsContract.Document.COLUMN_DISPLAY_NAME,DocumentsContract.Document.COLUMN_LAST_MODIFIED},null,null,null)){if(c!=null&&c.moveToFirst()){book.name=c.getString(0);book.created=c.getLong(1);}}
   book.pages.sort((a,b)->NATURAL.compare(a.name,b.name));book.count=book.pages.size();if(!book.pages.isEmpty())result.add(book);
  }
 }catch(InterruptedIOException ex){throw ex;}catch(Exception ex){scanWarnings.add(L10n.t("某个本地来源不可用，请在来源管理中重新授权目录。 "));}}result.sort((a,b)->NATURAL.compare(a.name,b.name));return result;}
 Book zipBook(String root,Uri uri,String name,String version,Images images,Api.Request request)throws Exception{
  Page archive=new Page();archive.uri=uri.toString();archive.id=root+"|"+uri;archive.version=version;File file=images.original(archive,request);Book b=new Book();b.id="local-zip:"+archive.id;b.name=name;b.source=L10n.t("手机本地 · 压缩包");b.rootId=root;
  try(ZipFile zip=new ZipFile(file)){Enumeration<? extends ZipEntry> entries=zip.entries();while(entries.hasMoreElements()){ZipEntry e=entries.nextElement();if(!e.isDirectory()&&Models.image(e.getName())&&!e.getName().startsWith("__MACOSX/")){Page p=new Page();p.id=archive.id+"|"+e.getName();p.uri=archive.uri;p.archive=archive.id;p.entry=e.getName();p.name=e.getName();p.version=version;b.pages.add(p);}}}b.pages.sort((a,c)->NATURAL.compare(a.name,c.name));b.count=b.pages.size();return b;
 }
}
