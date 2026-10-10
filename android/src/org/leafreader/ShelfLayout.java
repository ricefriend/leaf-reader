package org.leafreader;

import android.content.Context;
import android.graphics.*;
import android.view.*;
import android.view.accessibility.AccessibilityNodeInfo;
import android.os.Bundle;
import android.widget.*;

/** Uniform responsive covers with a proportional, draggable fast-scroll rail. */
final class ShelfLayout extends FrameLayout {
 final GridView grid;final Rail rail;final TextView empty;
 int columns=2,cardWidth,cardHeight;String section="";
 int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
 ShelfLayout(Context c){super(c);grid=new GridView(c);grid.setNumColumns(columns);grid.setStretchMode(GridView.STRETCH_COLUMN_WIDTH);grid.setHorizontalSpacing(dp(12));grid.setVerticalSpacing(dp(18));grid.setPadding(dp(16),dp(12),dp(36),dp(24));grid.setClipToPadding(false);grid.setVerticalScrollBarEnabled(false);grid.setSelector(new android.graphics.drawable.ColorDrawable(Color.TRANSPARENT));addView(grid,new LayoutParams(-1,-1));
  empty=new TextView(c);empty.setText(L10n.t("书架还空着\n导入本地目录，或连接电脑共享"));empty.setTextColor(Color.rgb(144,162,149));empty.setTextSize(15);empty.setGravity(Gravity.CENTER);empty.setLineSpacing(dp(10),1);addView(empty,new LayoutParams(-1,-1));grid.setEmptyView(empty);
  rail=new Rail(c);LayoutParams rp=new LayoutParams(dp(40),-1,Gravity.RIGHT);addView(rail,rp);grid.setOnScrollListener(new AbsListView.OnScrollListener(){public void onScrollStateChanged(AbsListView v,int state){rail.invalidate();}public void onScroll(AbsListView v,int first,int visible,int total){if(total>0){Object item=grid.getItemAtPosition(Math.min(first,total-1));section=item instanceof Models.Book?((Models.Book)item).name:"";}rail.invalidate();}});
 }
 protected void onSizeChanged(int w,int h,int ow,int oh){super.onSizeChanged(w,h,ow,oh);int available=Math.max(1,w-grid.getPaddingLeft()-grid.getPaddingRight());int next=Math.max(1,(available+dp(12))/(dp(126)+dp(12)));int width=(available-(next-1)*dp(12))/next;int first=grid.getFirstVisiblePosition();View child=grid.getChildAt(0);int top=child==null?0:child.getTop()-grid.getPaddingTop();if(next!=columns||width!=cardWidth){columns=next;cardWidth=width;cardHeight=Math.round(width*1.4f)+dp(62);grid.setNumColumns(columns);grid.setColumnWidth(width);if(grid.getAdapter() instanceof BaseAdapter)((BaseAdapter)grid.getAdapter()).notifyDataSetChanged();if(grid.getAdapter()!=null){View focus=getRootView().findFocus();grid.requestFocusFromTouch();grid.setSelectionFromTop(first,top);if(focus!=null&&focus!=grid)focus.requestFocus();}}}
 float stride(){return Math.max(1,cardHeight+grid.getVerticalSpacing());}
 float totalHeight(){int rows=(grid.getCount()+columns-1)/columns;return rows==0?0:rows*stride()-grid.getVerticalSpacing()+grid.getPaddingTop()+grid.getPaddingBottom();}
 float maxScroll(){return Math.max(0,totalHeight()-grid.getHeight());}
 float position(){View child=grid.getChildAt(0);if(child==null||maxScroll()==0)return 0;return Math.max(0,Math.min(1,((grid.getFirstVisiblePosition()/columns)*stride()+grid.getPaddingTop()-child.getTop())/maxScroll()));}
 void scrollTo(float fraction){float pixels=Math.max(0,Math.min(1,fraction))*maxScroll();int row=(int)(pixels/stride());/* GridView uses selected position for specific layout; leave touch mode first. */grid.requestFocusFromTouch();grid.setSelectionFromTop(Math.min(Math.max(0,grid.getCount()-1),row*columns),-(int)(pixels-row*stride()));rail.invalidate();}
 static final class Cover extends ImageView {
  final Path clip=new Path();final float radius;
  Cover(Context c){super(c);radius=12*c.getResources().getDisplayMetrics().density;}
  protected void onSizeChanged(int w,int h,int ow,int oh){super.onSizeChanged(w,h,ow,oh);clip.reset();clip.addRoundRect(new RectF(0,0,w,h),radius,radius,Path.Direction.CW);}
  protected void onDraw(Canvas canvas){int save=canvas.save();canvas.clipPath(clip);super.onDraw(canvas);canvas.restoreToCount(save);}
 }
 final class Rail extends View {
  final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);boolean dragging;float grabbed,requested;final RectF thumb=new RectF();
  Rail(Context c){super(c);setFocusable(true);setContentDescription(L10n.t("书架快速滚动条，上下拖动快速定位"));setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_YES);}
  float top(){return dp(18);}float bottom(){return Math.max(top()+1,getHeight()-dp(18));}
  float length(){return bottom()-top();}float handle(){return Math.min(length(),Math.max(dp(48),length()*Math.min(1,grid.getHeight()/Math.max(1,totalHeight()))));}
  void geometry(){float t=top()+(length()-handle())*(dragging?requested:position());thumb.set(getWidth()-dp(dragging?15:12),t,getWidth()-dp(7),t+handle());}
  protected void onDraw(Canvas c){if(maxScroll()<=0||grid.getCount()==0)return;geometry();paint.setColor(Color.rgb(41,53,46));c.drawRoundRect(new RectF(getWidth()-dp(11),top(),getWidth()-dp(8),bottom()),dp(2),dp(2),paint);paint.setColor(dragging?Color.rgb(193,228,188):Color.rgb(113,146,121));c.drawRoundRect(thumb,dp(5),dp(5),paint);}
  public boolean onTouchEvent(android.view.MotionEvent e){if(maxScroll()<=0)return false;switch(e.getActionMasked()){
   case MotionEvent.ACTION_DOWN:geometry();dragging=true;requested=position();grabbed=e.getY()>=thumb.top&&e.getY()<=thumb.bottom?e.getY()-thumb.top:handle()/2;getParent().requestDisallowInterceptTouchEvent(true);move(e.getY());return true;
   case MotionEvent.ACTION_MOVE:move(e.getY());return true;
   case MotionEvent.ACTION_UP:move(e.getY());dragging=false;getParent().requestDisallowInterceptTouchEvent(false);performClick();announceForAccessibility(L10n.t("已定位：")+section);ShelfLayout.this.invalidate();invalidate();return true;
   case MotionEvent.ACTION_CANCEL:dragging=false;getParent().requestDisallowInterceptTouchEvent(false);ShelfLayout.this.invalidate();invalidate();return true;
  }return true;}
  void move(float y){requested=Math.max(0,Math.min(1,(y-grabbed-top())/Math.max(1,length()-handle())));ShelfLayout.this.scrollTo(requested);ShelfLayout.this.invalidate();invalidate();}
  public boolean performClick(){super.performClick();return true;}
  public void onInitializeAccessibilityNodeInfo(AccessibilityNodeInfo info){super.onInitializeAccessibilityNodeInfo(info);info.setClassName("android.widget.SeekBar");info.setRangeInfo(AccessibilityNodeInfo.RangeInfo.obtain(AccessibilityNodeInfo.RangeInfo.RANGE_TYPE_FLOAT,0,1,position()));info.addAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_SCROLL_FORWARD);info.addAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_SCROLL_BACKWARD);}
  public boolean performAccessibilityAction(int action,Bundle args){if(action==AccessibilityNodeInfo.ACTION_SCROLL_FORWARD||action==AccessibilityNodeInfo.ACTION_SCROLL_BACKWARD){ShelfLayout.this.scrollTo(position()+(action==AccessibilityNodeInfo.ACTION_SCROLL_FORWARD?.1f:-.1f));return true;}return super.performAccessibilityAction(action,args);}
 }
 protected void dispatchDraw(Canvas c){super.dispatchDraw(c);if(rail.dragging&&grid.getCount()>0){float width=Math.min(dp(220),getWidth()-dp(68));float top=Math.max(dp(12),Math.min(getHeight()-dp(76),rail.thumb.centerY()-dp(32)));RectF box=new RectF(getWidth()-dp(48)-width,top,getWidth()-dp(48),top+dp(64));Paint p=rail.paint;p.setColor(Color.rgb(43,61,47));c.drawRoundRect(box,dp(14),dp(14),p);p.setColor(Color.rgb(224,236,218));p.setTextSize(dp(13));p.setTypeface(Typeface.create("sans-serif-medium",Typeface.NORMAL));int target=Math.min(grid.getCount()-1,Math.round(rail.requested*(grid.getCount()-1)));Object item=grid.getItemAtPosition(target);String full=item instanceof Models.Book?((Models.Book)item).name:section;String name=full;while(name.length()>1&&p.measureText(name)>width-dp(24))name=name.substring(0,name.length()-1);if(!name.equals(full))name+="…";c.drawText(name,box.left+dp(12),box.top+dp(24),p);p.setTextSize(dp(11));p.setColor(Color.rgb(165,192,170));c.drawText((target+1)+" / "+grid.getCount()+L10n.t(" 本"),box.left+dp(12),box.top+dp(47),p);}}
}
