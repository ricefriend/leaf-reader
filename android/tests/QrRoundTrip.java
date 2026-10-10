package org.leafreader;
import java.io.*;
import javax.imageio.ImageIO;
import java.awt.image.BufferedImage;
import com.google.zxing.*;
import com.google.zxing.common.HybridBinarizer;
public final class QrRoundTrip {
 public static void main(String[] args)throws Exception{BufferedImage image=ImageIO.read(new File(args[0]));int[] pixels=image.getRGB(0,0,image.getWidth(),image.getHeight(),null,0,image.getWidth());String decoded=new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(new RGBLuminanceSource(image.getWidth(),image.getHeight(),pixels)))).getText();String expected=new String(java.nio.file.Files.readAllBytes(java.nio.file.Paths.get(args[1])),"UTF-8");if(!decoded.equals(expected))throw new AssertionError("QR mismatch");System.out.println("PASS QRCoder Windows pairing QR decodes exactly with Android ZXing core");}
}
