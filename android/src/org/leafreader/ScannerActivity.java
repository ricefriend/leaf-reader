package org.leafreader;

import android.app.Activity;
import android.os.Bundle;
import android.content.*;
import android.content.pm.PackageManager;
import android.Manifest;
import android.hardware.Camera;
import android.view.*;
import android.widget.*;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
import java.util.*;
import java.util.concurrent.*;

public final class ScannerActivity extends Activity implements SurfaceHolder.Callback,Camera.PreviewCallback {
 SurfaceView preview;Camera camera;final ExecutorService decoder=Executors.newSingleThreadExecutor();volatile boolean decoding,closing;long lastFrame;
 public void onCreate(Bundle saved){super.onCreate(saved);LinearLayout root=new LinearLayout(this);root.setOrientation(1);root.setFitsSystemWindows(true);TextView hint=new TextView(this);hint.setText(L10n.t("对准电脑共享窗口的二维码。返回取消扫描。"));hint.setPadding(20,25,20,25);root.addView(hint);preview=new SurfaceView(this);root.addView(preview,new LinearLayout.LayoutParams(-1,0,1));setContentView(root);preview.getHolder().addCallback(this);if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED)requestPermissions(new String[]{Manifest.permission.CAMERA},1);}
 public void onRequestPermissionsResult(int request,String[] permissions,int[] results){super.onRequestPermissionsResult(request,permissions,results);if(results.length>0&&results[0]==PackageManager.PERMISSION_GRANTED)start();else{Toast.makeText(this,L10n.t("未授权相机，可在服务器管理中手动输入地址和配对码"),Toast.LENGTH_LONG).show();finish();}}
 public void surfaceCreated(SurfaceHolder holder){start();}public void surfaceChanged(SurfaceHolder h,int format,int width,int height){}public void surfaceDestroyed(SurfaceHolder h){stop();}
 void start(){if(camera!=null||closing||checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED||!preview.getHolder().getSurface().isValid())return;try{camera=Camera.open();Camera.Parameters parameters=camera.getParameters();List<Camera.Size> sizes=parameters.getSupportedPreviewSizes();Camera.Size selected=sizes.get(sizes.size()-1);for(Camera.Size s:sizes)if(s.width<=1280&&s.height<=960&&s.width*s.height>selected.width*selected.height)selected=s;parameters.setPreviewSize(selected.width,selected.height);if(parameters.getSupportedFocusModes()!=null&&parameters.getSupportedFocusModes().contains(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE))parameters.setFocusMode(Camera.Parameters.FOCUS_MODE_CONTINUOUS_PICTURE);camera.setParameters(parameters);camera.setDisplayOrientation(90);camera.setPreviewDisplay(preview.getHolder());camera.setPreviewCallback(this);camera.startPreview();}catch(Exception ex){stop();Toast.makeText(this,L10n.t("无法启动相机，请手动配对"),Toast.LENGTH_LONG).show();finish();}}
 void stop(){Camera c=camera;camera=null;if(c!=null){try{c.setPreviewCallback(null);c.stopPreview();}catch(Exception ignored){}c.release();}}
 public void onPreviewFrame(byte[] data,Camera device){if(decoding||closing||android.os.SystemClock.uptimeMillis()-lastFrame<300)return;lastFrame=android.os.SystemClock.uptimeMillis();decoding=true;Camera.Size size=device.getParameters().getPreviewSize();byte[] frame=Arrays.copyOf(data,size.width*size.height);decoder.submit(()->{try{Map<DecodeHintType,Object> hints=new EnumMap<>(DecodeHintType.class);hints.put(DecodeHintType.POSSIBLE_FORMATS,Collections.singletonList(BarcodeFormat.QR_CODE));MultiFormatReader reader=new MultiFormatReader();reader.setHints(hints);PlanarYUVLuminanceSource source=new PlanarYUVLuminanceSource(frame,size.width,size.height,0,0,size.width,size.height,false);Result result=reader.decodeWithState(new BinaryBitmap(new HybridBinarizer(source)));String text=result.getText();if(text.startsWith("leafreader://pair?"))runOnUiThread(()->{if(closing)return;closing=true;stop();setResult(RESULT_OK,new Intent().putExtra("text",text));finish();});}catch(Exception ignored){}finally{decoding=false;}});}
 protected void onPause(){stop();super.onPause();}protected void onResume(){super.onResume();if(preview!=null)start();}protected void onDestroy(){closing=true;stop();decoder.shutdownNow();super.onDestroy();}
}
