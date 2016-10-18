package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendEncrypted extends TLObject {

    public static final int ID = 0xa9776773;

    public TLInputEncryptedChat peer;
    public long random_id;
    public byte[] data;

    public SendEncrypted() {
    }

    public SendEncrypted(TLInputEncryptedChat peer, long random_id, byte[] data) {
        this.peer = peer;
        this.random_id = random_id;
        this.data = data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readLong();
        data = buffer.readBytes();
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
        buff.writeLong(random_id);
        buff.writeBytes(data);
    }


    public int getConstructor() {
        return ID;
    }
}