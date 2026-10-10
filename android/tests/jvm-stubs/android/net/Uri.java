package android.net;
/** JVM transport tests only. Production uses the Android Uri implementation. */
public final class Uri {public static String encode(String text){try{return java.net.URLEncoder.encode(text,"UTF-8").replace("+","%20");}catch(java.io.UnsupportedEncodingException ex){throw new AssertionError(ex);}}}
