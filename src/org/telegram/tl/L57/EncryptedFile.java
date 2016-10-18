package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EncryptedFile extends TLEncryptedFile {

    public static final int ID = 0x4a70994c;

    public long id;
    public long access_hash;
    public int size;
    public int dc_id;
    public int key_fingerprint;

    public EncryptedFile() {
    }

    public EncryptedFile(long id, long access_hash, int size, int dc_id, int key_fingerprint) {
        this.id = id;
        this.access_hash = access_hash;
        this.size = size;
        this.dc_id = dc_id;
        this.key_fingerprint = key_fingerprint;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        access_hash = buffer.readLong();
        size = buffer.readInt();
        dc_id = buffer.readInt();
        key_fingerprint = buffer.readInt();
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
        buff.writeLong(id);
        buff.writeLong(access_hash);
        buff.writeInt(size);
        buff.writeInt(dc_id);
        buff.writeInt(key_fingerprint);
    }


    public int getConstructor() {
        return ID;
    }
}