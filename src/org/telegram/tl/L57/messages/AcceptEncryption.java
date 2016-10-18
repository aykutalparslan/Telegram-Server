package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class AcceptEncryption extends TLObject {

    public static final int ID = 0x3dbc0415;

    public TLInputEncryptedChat peer;
    public byte[] g_b;
    public long key_fingerprint;

    public AcceptEncryption() {
    }

    public AcceptEncryption(TLInputEncryptedChat peer, byte[] g_b, long key_fingerprint) {
        this.peer = peer;
        this.g_b = g_b;
        this.key_fingerprint = key_fingerprint;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        g_b = buffer.readBytes();
        key_fingerprint = buffer.readLong();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(peer);
        buff.writeBytes(g_b);
        buff.writeLong(key_fingerprint);
    }


    public int getConstructor() {
        return ID;
    }
}