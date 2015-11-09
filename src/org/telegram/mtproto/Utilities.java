/*
 * This is the source code of Telegram for Android v. 2.x.x.
 * It is licensed under GNU GPL v. 2 or later.
 * You should have received a copy of the license in this archive (see LICENSE).
 *
 * Copyright Nikolai Kudashov, 2013-2015.
 */


package org.telegram.mtproto;

import org.bouncycastle.jce.provider.BouncyCastleProvider;
import org.bouncycastle.util.encoders.Base64;
import org.bouncycastle.util.io.pem.PemObject;
import org.bouncycastle.util.io.pem.PemReader;
import org.bouncycastle.util.io.pem.PemWriter;
import org.telegram.tl.TLObject;



import javax.crypto.Cipher;
import java.io.*;
import java.math.BigInteger;
import java.nio.ByteBuffer;
import java.security.*;
import java.security.spec.*;
import java.util.ArrayList;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.zip.CRC32;
import java.util.zip.GZIPInputStream;
import java.util.zip.GZIPOutputStream;

public class Utilities {

    public static Pattern pattern = Pattern.compile("[0-9]+");
    public static SecureRandom random = new SecureRandom();

    private static byte[] decompressBuffer;

    public static ArrayList<String> goodPrimes = new ArrayList<>();

    public static class TPFactorizedValue {
        public long p, q;
    }


    final protected static char[] hexArray = "0123456789ABCDEF".toCharArray();

    public static Integer parseInt(String value) {
        if (value == null) {
            return 0;
        }
        Integer val = 0;
        try {
            Matcher matcher = pattern.matcher(value);
            if (matcher.find()) {
                String num = matcher.group(0);
                val = Integer.parseInt(num);
            }
        } catch (Exception e) {
            //FileLog.e("tmessages", e);
        }
        return val;
    }

    public static String parseIntToString(String value) {
        Matcher matcher = pattern.matcher(value);
        if (matcher.find()) {
            return matcher.group(0);
        }
        return null;
    }

    public static String bytesToHex(byte[] bytes) {
        if (bytes == null) {
            return "";
        }
        char[] hexChars = new char[bytes.length * 2];
        int v;
        for (int j = 0; j < bytes.length; j++) {
            v = bytes[j] & 0xFF;
            hexChars[j * 2] = hexArray[v >>> 4];
            hexChars[j * 2 + 1] = hexArray[v & 0x0F];
        }
        return new String(hexChars);
    }

    public static byte[] hexToBytes(String hex) {
        if (hex == null) {
            return null;
        }
        int len = hex.length();
        byte[] data = new byte[len / 2];
        for (int i = 0; i < len; i += 2) {
            data[i / 2] = (byte) ((Character.digit(hex.charAt(i), 16) << 4) + Character.digit(hex.charAt(i + 1), 16));
        }
        return data;
    }

    public static boolean isGoodPrime(byte[] prime, int g) {
        if (!(g >= 2 && g <= 7)) {
            return false;
        }

        if (prime.length != 256 || prime[0] >= 0) {
            return false;
        }

        BigInteger dhBI = new BigInteger(1, prime);

        if (g == 2) { // p mod 8 = 7 for g = 2;
            BigInteger res = dhBI.mod(BigInteger.valueOf(8));
            if (res.intValue() != 7) {
                return false;
            }
        } else if (g == 3) { // p mod 3 = 2 for g = 3;
            BigInteger res = dhBI.mod(BigInteger.valueOf(3));
            if (res.intValue() != 2) {
                return false;
            }
        } else if (g == 5) { // p mod 5 = 1 or 4 for g = 5;
            BigInteger res = dhBI.mod(BigInteger.valueOf(5));
            int val = res.intValue();
            if (val != 1 && val != 4) {
                return false;
            }
        } else if (g == 6) { // p mod 24 = 19 or 23 for g = 6;
            BigInteger res = dhBI.mod(BigInteger.valueOf(24));
            int val = res.intValue();
            if (val != 19 && val != 23) {
                return false;
            }
        } else if (g == 7) { // p mod 7 = 3, 5 or 6 for g = 7.
            BigInteger res = dhBI.mod(BigInteger.valueOf(7));
            int val = res.intValue();
            if (val != 3 && val != 5 && val != 6) {
                return false;
            }
        }

        String hex = bytesToHex(prime);
        for (String cached : goodPrimes) {
            if (cached.equals(hex)) {
                return true;
            }
        }

        BigInteger dhBI2 = dhBI.subtract(BigInteger.valueOf(1)).divide(BigInteger.valueOf(2));
        if (!dhBI.isProbablePrime(30) || !dhBI2.isProbablePrime(30)) {
            return false;
        }

        goodPrimes.add(hex);

        /*globalQueue.postRunnable(new Runnable() {
            @Override
            public void run() {
                try {
                    SerializedData data = new SerializedData();
                    data.writeInt32(goodPrimes.size());
                    for (String pr : goodPrimes) {
                        data.writeString(pr);
                    }
                    byte[] bytes = data.toByteArray();
                    data.cleanup();
                    SharedPreferences preferences = ApplicationLoader.applicationContext.getSharedPreferences("primes", Context.MODE_PRIVATE);
                    SharedPreferences.Editor editor = preferences.edit();
                    editor.putString("primes", Base64.encodeToString(bytes, Base64.DEFAULT));
                    editor.commit();
                } catch (Exception e) {
                    FileLog.e("tmessages", e);
                }
            }
        });*/

        return true;
    }

    public static boolean isGoodGaAndGb(BigInteger g_a, BigInteger p) {
        return !(g_a.compareTo(BigInteger.valueOf(1)) != 1 || g_a.compareTo(p.subtract(BigInteger.valueOf(1))) != -1);
    }


    public static boolean arraysEquals(byte[] arr1, int offset1, byte[] arr2, int offset2) {
        if (arr1 == null || arr2 == null || offset1 < 0 || offset2 < 0 || arr1.length - offset1 != arr2.length - offset2 || arr1.length - offset1 < 0 || arr2.length - offset2 < 0) {
            return false;
        }
        boolean result = true;
        for (int a = offset1; a < arr1.length; a++) {
            if (arr1[a + offset1] != arr2[a + offset2]) {
                result = false;
            }
        }
        return result;
    }

    public static byte[] computeSHA1(byte[] convertme, int offset, int len) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-1");
            md.update(convertme, offset, len);
            return md.digest();
        } catch (Exception e) {
            //FileLog.e("tmessages", e);
        }
        return null;
    }

    public static byte[] computeSHA1(ByteBuffer convertme, int offset, int len) {
        int oldp = convertme.position();
        int oldl = convertme.limit();
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-1");
            convertme.position(offset);
            convertme.limit(len);
            md.update(convertme);
            return md.digest();
        } catch (Exception e) {
            //FileLog.e("tmessages", e);
        } finally {
            convertme.limit(oldl);
            convertme.position(oldp);
        }
        return new byte[0];
    }

    public static byte[] computeSHA1(ByteBuffer convertme) {
        return computeSHA1(convertme, 0, convertme.limit());
    }

    public static byte[] computeSHA1(byte[] convertme) {
        return computeSHA1(convertme, 0, convertme.length);
    }

    public static byte[] computeSHA256(byte[] convertme, int offset, int len) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            md.update(convertme, offset, len);
            return md.digest();
        } catch (Exception e) {
            //FileLog.e("tmessages", e);
        }
        return null;
    }

    public static byte[] encryptWithRSA(BigInteger[] key, byte[] data) {
        try {
            KeyFactory fact = KeyFactory.getInstance("RSA");
            RSAPublicKeySpec keySpec = new RSAPublicKeySpec(key[0], key[1]);
            PublicKey publicKey = fact.generatePublic(keySpec);
            final Cipher cipher = Cipher.getInstance("RSA");
            cipher.init(Cipher.ENCRYPT_MODE, publicKey);
            return cipher.doFinal(data);
        } catch (Exception e) {
            //FileLog.e("tmessages", e);
        }
        return null;
    }

    public static byte[] encryptWithRSA(PublicKey publicKey, byte[] data) {
        try {
            final Cipher cipher = Cipher.getInstance("RSA/ECB/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, publicKey);
            return cipher.doFinal(data);
        } catch (Exception e) {
            //FileLog.e("tmessages", e);
        }
        return null;
    }

    public static byte[] decryptWithRSA(PrivateKey privateKey, byte[] buffer) {
        try {
            Cipher rsa = Cipher.getInstance("RSA/ECB/NoPadding");
            rsa.init(Cipher.DECRYPT_MODE, privateKey);
            return rsa.doFinal(buffer);
        } catch (Exception e) {
            e.printStackTrace();
        }
        return null;
    }

    public static long bytesToLong(byte[] bytes) {
        return ((long) bytes[7] << 56) + (((long) bytes[6] & 0xFF) << 48) + (((long) bytes[5] & 0xFF) << 40) + (((long) bytes[4] & 0xFF) << 32)
                + (((long) bytes[3] & 0xFF) << 24) + (((long) bytes[2] & 0xFF) << 16) + (((long) bytes[1] & 0xFF) << 8) + ((long) bytes[0] & 0xFF);
    }
    public static long bytesToLong2(byte[] b) {
        long result = 0;
        for (int i = 0; i < 8; i++) {
            result <<= 8;
            result |= (b[i] & 0xFF);
        }
        return result;
    }



    public static byte[] compress(byte[] data) {
        if (data == null) {
            return null;
        }

        byte[] packedData = null;
        ByteArrayOutputStream bytesStream = new ByteArrayOutputStream();
        try {
            GZIPOutputStream zip = new GZIPOutputStream(bytesStream);
            zip.write(data);
            zip.close();
            packedData = bytesStream.toByteArray();
        } catch (IOException e) {
            //FileLog.e("tmessages", e);
        } finally {
            try {
                bytesStream.close();
            } catch (Exception e) {
                //FileLog.e("tmessages", e);
            }
        }
        return packedData;
    }

    public static String MD5(String md5) {
        if (md5 == null) {
            return null;
        }
        try {
            MessageDigest md = MessageDigest.getInstance("MD5");
            byte[] array = md.digest(md5.getBytes());
            StringBuilder sb = new StringBuilder();
            for (byte anArray : array) {
                sb.append(Integer.toHexString((anArray & 0xFF) | 0x100).substring(1, 3));
            }
            return sb.toString();
        } catch (java.security.NoSuchAlgorithmException e) {
            //FileLog.e("tmessages", e);
        }
        return null;
    }

    public static PrivateKey getPemPrivateKey(String filename, String algorithm) throws Exception {
        BufferedReader br = new BufferedReader(new FileReader(filename));
        Security.addProvider(new BouncyCastleProvider());
        PemReader pp = new PemReader(br);
        PemObject pem = pp.readPemObject();
        byte[] content = pem.getContent();
        pp.close();

        PKCS8EncodedKeySpec spec = new PKCS8EncodedKeySpec(content);
        KeyFactory kf = KeyFactory.getInstance(algorithm);
        return kf.generatePrivate(spec);
    }

    public static PublicKey getPemPublicKey(String filename, String algorithm) throws Exception {
        BufferedReader br = new BufferedReader(new FileReader(filename));
        Security.addProvider(new BouncyCastleProvider());
        PemReader pp = new PemReader(br);
        PemObject pem = pp.readPemObject();
        byte[] content = pem.getContent();
        pp.close();

        X509EncodedKeySpec spec =
                new X509EncodedKeySpec(content);
        KeyFactory kf = KeyFactory.getInstance(algorithm);
        return kf.generatePublic(spec);
    }

    public static KeyPair loadRSAKeyPair(){
        File f = new File("private.key");
        if(!f.exists() || f.isDirectory()) {
            return generateRSAKeyPair();
        } else {
            try{
                PrivateKey privateKey = getPemPrivateKey("private.key","RSA");
                PublicKey publicKey = getPemPublicKey("public.key", "RSA");

                KeyPair kp = new KeyPair(publicKey, privateKey);

                return kp;
            }catch (Exception e){
                return null;
            }
        }
    }

    public static KeyPair generateRSAKeyPair(){
        try {
            KeyPairGenerator kpg = KeyPairGenerator.getInstance("RSA");
            kpg.initialize(2048);
            KeyPair kp = kpg.genKeyPair();

            saveKeyPair(new java.io.File( "." ).getCanonicalPath(), kp);
            return  kp;
        }catch (Exception e){
            return null;
        }

    }

    public static void saveKeyPair(String path, KeyPair keyPair) throws IOException {
        PrivateKey privateKey = keyPair.getPrivate();
        PublicKey publicKey = keyPair.getPublic();

        // Store Public Key.
        FileOutputStream fos = new FileOutputStream(path + "/public.key");
        fos.write(toPEM("RSA PUBLIC KEY", publicKey.getEncoded()).getBytes());
        fos.close();

        // Store Private Key.
        fos = new FileOutputStream(path + "/private.key");
        fos.write(toPEM("RSA PRIVATE KEY", privateKey.getEncoded()).getBytes());
        fos.close();
    }

    public static String toPEM(String type, byte[] data) {
        try{
            StringWriter writer = new StringWriter();
            PemWriter pemWriter = new PemWriter(writer);
            pemWriter.writeObject(new PemObject(type, data));
            pemWriter.flush();
            pemWriter.close();
            return writer.toString();
        }catch (Exception e){
            return "";
        }

    }

    public static int getCRC32(String value) {
        CRC32 crc = new CRC32();
        crc.update(value.getBytes());
        return (int) (crc.getValue());
    }
}
