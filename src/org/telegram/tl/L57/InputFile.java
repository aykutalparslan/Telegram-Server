package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputFile extends TLInputFile {

    public static final int ID = 0xf52ff27f;

    public long id;
    public int parts;
    public String name;
    public String md5_checksum;

    public InputFile() {
    }

    public InputFile(long id, int parts, String name, String md5_checksum) {
        this.id = id;
        this.parts = parts;
        this.name = name;
        this.md5_checksum = md5_checksum;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        parts = buffer.readInt();
        name = buffer.readString();
        md5_checksum = buffer.readString();
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
        buff.writeString(name);
        buff.writeString(md5_checksum);
    }


    public int getConstructor() {
        return ID;
    }
}