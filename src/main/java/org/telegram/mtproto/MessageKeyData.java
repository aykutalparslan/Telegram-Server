/*
 * This is the source code of Telegram for Android v. 2.x.x.
 * It is licensed under GNU GPL v. 2 or later.
 * You should have received a copy of the license in this archive (see LICENSE).
 *
 * Copyright Nikolai Kudashov, 2013-2015.
 */


package org.telegram.mtproto;

import java.nio.ByteBuffer;
import java.security.MessageDigest;

public class MessageKeyData {
    public byte[] aesKey;
    public byte[] aesIv;

    private static byte[] computeSHA1(byte[] convertme, int offset, int len) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-1");
            md.update(convertme, offset, len);
            return md.digest();
        } catch (Exception e) {
            //FileLog.e("tmessages", e);
        }
        return null;
    }

    private static byte[] computeSHA1(ByteBuffer convertme, int offset, int len) {
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

    private static byte[] computeSHA1(ByteBuffer convertme) {
        return computeSHA1(convertme, 0, convertme.limit());
    }

    public static byte[] computeSHA1(byte[] convertme) {
        return computeSHA1(convertme, 0, convertme.length);
    }

    public static MessageKeyData generateMessageKeyData(byte[] authKey, byte[] messageKey, boolean outgoing) {
        MessageKeyData keyData = new MessageKeyData();
        if (authKey == null || authKey.length == 0) {
            keyData.aesIv = null;
            keyData.aesKey = null;
            return keyData;
        }

        int x = outgoing ? 8 : 0;

        ProtocolBuffer data = new ProtocolBuffer(64);
        data.write(messageKey);
        data.write(authKey, x, 32);
        byte[] sha1_a = computeSHA1(data.getBytes());
        data.release();

        data = new ProtocolBuffer(64);
        data.write(authKey, 32 + x, 16);
        data.write(messageKey);
        data.write(authKey, 48 + x, 16);
        byte[] sha1_b = computeSHA1(data.getBytes());
        data.release();

        data = new ProtocolBuffer(64);
        data.write(authKey, 64 + x, 32);
        data.write(messageKey);
        byte[] sha1_c = computeSHA1(data.getBytes());
        data.release();

        data = new ProtocolBuffer(64);
        data.write(messageKey);
        data.write(authKey, 96 + x, 32);
        byte[] sha1_d = computeSHA1(data.getBytes());
        data.release();

        data = new ProtocolBuffer(64);
        data.write(sha1_a, 0, 8);
        data.write(sha1_b, 8, 12);
        data.write(sha1_c, 4, 12);
        keyData.aesKey = data.getBytes();
        data.release();

        data = new ProtocolBuffer(64);
        data.write(sha1_a, 8, 12);
        data.write(sha1_b, 0, 8);
        data.write(sha1_c, 16, 4);
        data.write(sha1_d, 0, 8);
        keyData.aesIv = data.getBytes();
        data.release();

        return keyData;
    }
}
