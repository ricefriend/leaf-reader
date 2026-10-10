package org.leafreader;

import android.provider.*;
import android.database.*;
import android.os.*;
import java.io.*;

// Included only in the instrumented APK; not in the delivered reader APK.
public final class TestDocuments extends DocumentsProvider {
 File root;public boolean onCreate(){root=new File(getContext().getFilesDir(),"test-saf");root.mkdirs();return true;}
 File file(String id)throws FileNotFoundException{try{File value="root".equals(id)?root:new File(root,id);if(!value.getCanonicalPath().equals(root.getCanonicalPath())&&!value.getCanonicalPath().startsWith(root.getCanonicalPath()+File.separator))throw new FileNotFoundException();return value;}catch(IOException e){throw new FileNotFoundException();}}
 static final String[] FIELDS={DocumentsContract.Document.COLUMN_DOCUMENT_ID,DocumentsContract.Document.COLUMN_DISPLAY_NAME,DocumentsContract.Document.COLUMN_MIME_TYPE,DocumentsContract.Document.COLUMN_SIZE,DocumentsContract.Document.COLUMN_LAST_MODIFIED,DocumentsContract.Document.COLUMN_FLAGS};
 void row(MatrixCursor cursor,File file){String id=file.equals(root)?"root":file.getAbsolutePath().substring(root.getAbsolutePath().length()+1);String type=file.isDirectory()?DocumentsContract.Document.MIME_TYPE_DIR:"image/jpeg";MatrixCursor.RowBuilder row=cursor.newRow();for(String field:cursor.getColumnNames()){Object value=field.equals(FIELDS[0])?id:field.equals(FIELDS[1])?file.getName():field.equals(FIELDS[2])?type:field.equals(FIELDS[3])?file.length():field.equals(FIELDS[4])?file.lastModified():0;row.add(value);}}
 public Cursor queryRoots(String[] columns){String[] fields=columns==null?new String[]{DocumentsContract.Root.COLUMN_ROOT_ID,DocumentsContract.Root.COLUMN_DOCUMENT_ID,DocumentsContract.Root.COLUMN_TITLE,DocumentsContract.Root.COLUMN_FLAGS}:columns;MatrixCursor cursor=new MatrixCursor(fields);MatrixCursor.RowBuilder row=cursor.newRow();for(String f:fields)row.add(f.equals(DocumentsContract.Root.COLUMN_TITLE)?"Leaf test directory":f.equals(DocumentsContract.Root.COLUMN_FLAGS)?DocumentsContract.Root.FLAG_SUPPORTS_IS_CHILD:"root");return cursor;}
 public Cursor queryDocument(String id,String[] projection)throws FileNotFoundException{MatrixCursor cursor=new MatrixCursor(projection==null?FIELDS:projection);row(cursor,file(id));return cursor;}
 public Cursor queryChildDocuments(String id,String[] projection,String sortOrder)throws FileNotFoundException{MatrixCursor cursor=new MatrixCursor(projection==null?FIELDS:projection);File[] children=file(id).listFiles();if(children!=null)for(File child:children)row(cursor,child);return cursor;}
 public ParcelFileDescriptor openDocument(String id,String mode,CancellationSignal cancellation)throws FileNotFoundException{if(!mode.equals("r"))throw new FileNotFoundException("read only");return ParcelFileDescriptor.open(file(id),ParcelFileDescriptor.MODE_READ_ONLY);}
 public boolean isChildDocument(String parent,String child){try{return file(child).getCanonicalPath().startsWith(file(parent).getCanonicalPath()+File.separator);}catch(Exception e){return false;}}
}
