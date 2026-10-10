package org.leafreader;

import java.io.*;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.util.Arrays;
import javax.crypto.*;
import javax.crypto.spec.*;
import org.json.*;

/** Password-protected portable backup; no Android Keystore material is exported. */
final class MigrationBackup {
 static final int ITERATIONS=210000,MAX_BYTES=16*1024*1024;
 static final byte[] AAD="leaf-reader-backup/v1/PBKDF2-SHA256/210000".getBytes(StandardCharsets.UTF_8);
 static byte[] derive(char[] password,byte[] salt)throws Exception{PBEKeySpec spec=new PBEKeySpec(password,salt,ITERATIONS,256);try{return SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).getEncoded();}finally{spec.clearPassword();}}
 static String encode(byte[] bytes){return java.util.Base64.getEncoder().encodeToString(bytes);}
 static byte[] decode(String text){return java.util.Base64.getDecoder().decode(text);}
 static byte[] seal(JSONObject state,char[] password)throws Exception{
  if(password.length<8)throw new IOException(L10n.pick("备份密码至少需要 8 个字符","Use at least 8 characters for the backup password"));
  byte[] plain=state.toString().getBytes(StandardCharsets.UTF_8);if(plain.length>MAX_BYTES/2)throw new IOException(L10n.pick("阅读记录过大，无法生成备份","Reading history is too large to back up"));
  byte[] salt=new byte[16],iv=new byte[12];SecureRandom random=new SecureRandom();random.nextBytes(salt);random.nextBytes(iv);byte[] key=derive(password,salt);
  try{Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.ENCRYPT_MODE,new SecretKeySpec(key,"AES"),new GCMParameterSpec(128,iv));cipher.updateAAD(AAD);byte[] encrypted=cipher.doFinal(plain);return new JSONObject().put("format","leaf-reader-backup").put("version",1).put("kdf","PBKDF2-HMAC-SHA256").put("iterations",ITERATIONS).put("salt",encode(salt)).put("iv",encode(iv)).put("ciphertext",encode(encrypted)).toString().getBytes(StandardCharsets.UTF_8);}finally{Arrays.fill(key,(byte)0);Arrays.fill(plain,(byte)0);}
 }
 static JSONObject open(byte[] bytes,char[] password)throws Exception{
  if(bytes.length>MAX_BYTES)throw new IOException(L10n.pick("备份文件超过 16 MB","Backup exceeds 16 MB"));JSONObject envelope;
  try{envelope=new JSONObject(new String(bytes,StandardCharsets.UTF_8));if(!"leaf-reader-backup".equals(envelope.getString("format"))||envelope.getInt("version")!=1||envelope.getInt("iterations")!=ITERATIONS||!"PBKDF2-HMAC-SHA256".equals(envelope.getString("kdf")))throw new Exception();}catch(Exception ex){throw new IOException(L10n.pick("不是受支持的 Leaf Reader 加密备份","Not a supported encrypted Leaf Reader backup"));}
  byte[] key=null,plain=null;
  try{byte[] salt=decode(envelope.getString("salt")),iv=decode(envelope.getString("iv")),data=decode(envelope.getString("ciphertext"));if(salt.length!=16||iv.length!=12||data.length<16)throw new Exception();key=derive(password,salt);Cipher cipher=Cipher.getInstance("AES/GCM/NoPadding");cipher.init(Cipher.DECRYPT_MODE,new SecretKeySpec(key,"AES"),new GCMParameterSpec(128,iv));cipher.updateAAD(AAD);plain=cipher.doFinal(data);JSONObject state=new JSONObject(new String(plain,StandardCharsets.UTF_8));if(!"leaf-reader-state".equals(state.getString("format"))||state.getInt("version")!=1)throw new Exception();return state;}catch(Exception ex){throw new IOException(L10n.pick("密码错误或备份已损坏；当前数据没有更改","Wrong password or damaged backup; current data was not changed"));}finally{if(key!=null)Arrays.fill(key,(byte)0);if(plain!=null)Arrays.fill(plain,(byte)0);}
 }
 static byte[] read(InputStream in)throws IOException{try(ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] buffer=new byte[16384];int n;while((n=in.read(buffer))!=-1){if(out.size()+n>MAX_BYTES)throw new IOException(L10n.pick("备份文件超过 16 MB","Backup exceeds 16 MB"));out.write(buffer,0,n);}return out.toByteArray();}}
}
