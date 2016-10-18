package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputEncryptedFileBigUploaded extends TLInputEncryptedFile {

    public static final int ID = 0x2dc173c8;

    public long id;
    public int parts;
    public int key_fingerprint;

    public InputEncryptedFileBigUploaded() {
    }

    public InputEncryptedFileBigUploaded(long id, int parts, int key_fingerprint) {
        this.id = id;
        this.parts = parts;
        this.key_fingerprint = key_fingerprint;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        parts = buffer.readInt();
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
        buff.writeInt(parts);
        buff.writeInt(key_fingerprint);
    }


    public int getConstructor() {
        return ID;
    }
}