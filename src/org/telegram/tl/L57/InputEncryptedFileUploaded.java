package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputEncryptedFileUploaded extends org.telegram.tl.TLInputEncryptedFile {

    public static final int ID = 0x64bd0306;

    public long id;
    public int parts;
    public String md5_checksum;
    public int key_fingerprint;

    public InputEncryptedFileUploaded() {
    }

    public InputEncryptedFileUploaded(long id, int parts, String md5_checksum, int key_fingerprint) {
        this.id = id;
        this.parts = parts;
        this.md5_checksum = md5_checksum;
        this.key_fingerprint = key_fingerprint;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        parts = buffer.readInt();
        md5_checksum = buffer.readString();
        key_fingerprint = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeLong(id);
        buff.writeInt(parts);
        buff.writeString(md5_checksum);
        buff.writeInt(key_fingerprint);
    }


    public int getConstructor() {
        return ID;
    }
}