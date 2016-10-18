package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendEncryptedFile extends TLObject {

    public static final int ID = 0x9a901b66;

    public TLInputEncryptedChat peer;
    public long random_id;
    public byte[] data;
    public TLInputEncryptedFile file;

    public SendEncryptedFile() {
    }

    public SendEncryptedFile(TLInputEncryptedChat peer, long random_id, byte[] data, TLInputEncryptedFile file) {
        this.peer = peer;
        this.random_id = random_id;
        this.data = data;
        this.file = file;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        random_id = buffer.readLong();
        data = buffer.readBytes();
        file = (TLInputEncryptedFile) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(file);
    }


    public int getConstructor() {
        return ID;
    }
}