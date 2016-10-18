package org.telegram.tl.L57.upload;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SaveFilePart extends TLObject {

    public static final int ID = 0xb304a621;

    public long file_id;
    public int file_part;
    public byte[] bytes;

    public SaveFilePart() {
    }

    public SaveFilePart(long file_id, int file_part, byte[] bytes) {
        this.file_id = file_id;
        this.file_part = file_part;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file_id = buffer.readLong();
        file_part = buffer.readInt();
        bytes = buffer.readBytes();
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
        buff.writeLong(file_id);
        buff.writeInt(file_part);
        buff.writeBytes(bytes);
    }


    public int getConstructor() {
        return ID;
    }
}