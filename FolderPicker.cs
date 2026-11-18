using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LeafReader {
 static class FolderPicker {
  const uint FOS_PICKFOLDERS=0x00000020;
  const uint FOS_FORCEFILESYSTEM=0x00000040;
  const uint FOS_PATHMUSTEXIST=0x00000800;
  const uint SIGDN_FILESYSPATH=0x80058000;
  const int ERROR_CANCELLED=unchecked((int)0x800704C7);

  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]
  static extern int SHCreateItemFromParsingName(string pszPath,IntPtr pbc,ref Guid riid,out IShellItem ppv);

  [ComImport,Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
  class FileOpenDialogNative {}

  [ComImport,Guid("42f85136-db7e-439c-85f1-e4075d135fc8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  internal interface IFileDialog {
   [PreserveSig] int Show(IntPtr parent);
   void SetFileTypes(uint cFileTypes,IntPtr rgFilterSpec);
   void SetFileTypeIndex(uint iFileType);
   void GetFileTypeIndex(out uint piFileType);
   void Advise(IntPtr pfde,out uint pdwCookie);
   void Unadvise(uint dwCookie);
   void SetOptions(uint fos);
   void GetOptions(out uint pfos);
   void SetDefaultFolder(IShellItem psi);
   void SetFolder(IShellItem psi);
   void GetFolder(out IShellItem ppsi);
   void GetCurrentSelection(out IShellItem ppsi);
   void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
   void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
   void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
   void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
   void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
   void GetResult(out IShellItem ppsi);
   void AddPlace(IShellItem psi,int alignment);
   void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
   void Close(int hr);
   void SetClientGuid(ref Guid guid);
   void ClearClientData();
   void SetFilter(IntPtr pFilter);
  }

  [ComImport,Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  internal interface IShellItem {
   void BindToHandler(IntPtr pbc,ref Guid bhid,ref Guid riid,out IntPtr ppv);
   void GetParent(out IShellItem ppsi);
   void GetDisplayName(uint sigdnName,[MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
   void GetAttributes(uint sfgaoMask,out uint psfgaoAttribs);
   void Compare(IShellItem psi,uint hint,out int piOrder);
  }

  internal static bool TryCreateNativePicker(string title,string initialFolder,out IFileDialog dialog){
   dialog=null;
   try{
    var instance=(IFileDialog)new FileOpenDialogNative();
    uint options;
    instance.GetOptions(out options);
    instance.SetOptions(options|FOS_PICKFOLDERS|FOS_FORCEFILESYSTEM|FOS_PATHMUSTEXIST);
    if(!string.IsNullOrEmpty(title))instance.SetTitle(title);
    if(!string.IsNullOrEmpty(initialFolder)&&Directory.Exists(initialFolder)){
     Guid shellItemId=typeof(IShellItem).GUID;
     IShellItem item;
     if(SHCreateItemFromParsingName(initialFolder,IntPtr.Zero,ref shellItemId,out item)==0&&item!=null){
      instance.SetFolder(item);
      if(Marshal.IsComObject(item))Marshal.FinalReleaseComObject(item);
     }
    }
    dialog=instance;
    return true;
   }catch{
    if(dialog!=null&&Marshal.IsComObject(dialog))Marshal.FinalReleaseComObject(dialog);
    dialog=null;
    return false;
   }
  }

  public static string Pick(Window owner,string title,string initialFolder=null){
   IFileDialog dialog;
   if(TryCreateNativePicker(title,initialFolder,out dialog)){
    try{
     IntPtr hwnd=owner!=null?new WindowInteropHelper(owner).Handle:IntPtr.Zero;
     int hr=dialog.Show(hwnd);
     if(hr==ERROR_CANCELLED)return null;
     if(hr!=0)Marshal.ThrowExceptionForHR(hr);
     IShellItem item;
     dialog.GetResult(out item);
     try{
      string path;
      item.GetDisplayName(SIGDN_FILESYSPATH,out path);
      return path;
     }finally{
      if(item!=null&&Marshal.IsComObject(item))Marshal.FinalReleaseComObject(item);
     }
    }catch{
     // Fall back only if the native Explorer dialog fails unexpectedly.
    }finally{
     if(Marshal.IsComObject(dialog))Marshal.FinalReleaseComObject(dialog);
    }
   }
   using(var fallback=new System.Windows.Forms.FolderBrowserDialog{Description=title,ShowNewFolderButton=false}){
    if(!string.IsNullOrEmpty(initialFolder)&&Directory.Exists(initialFolder))fallback.SelectedPath=initialFolder;
    return fallback.ShowDialog()==System.Windows.Forms.DialogResult.OK?fallback.SelectedPath:null;
   }
  }
 }
}
