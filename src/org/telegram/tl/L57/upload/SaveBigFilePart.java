package org.telegram.tl.L57.upload;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SaveBigFilePart extends TLObject {

    public static final int ID = 0xde7b673d;

    public long file_id;
    public int file_part;
    public int file_total_parts;
    public byte[] bytes;

    public SaveBigFilePart() {
    }

    public SaveBigFilePart(long file_id, int file_part, int file_total_parts, byte[] bytes) {
        this.file_id = file_id;
        this.file_part = file_part;
        this.file_total_parts = file_total_parts;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file_id = buffer.readLong();
        file_part = buffer.readInt();
        file_total_parts = buffer.readInt();
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
        buff.writeInt(file_total_parts);
        buff.writeBytes(bytes);
    }


    public int getConstructor() {
        return ID;
    }
}