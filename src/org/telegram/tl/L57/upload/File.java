package org.telegram.tl.L57.upload;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.storage.*;

public class File extends TLFile {

    public static final int ID = 0x96a18d5;

    public TLFileType type;
    public int mtime;
    public byte[] bytes;

    public File() {
    }

    public File(TLFileType type, int mtime, byte[] bytes) {
        this.type = type;
        this.mtime = mtime;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        type = (TLFileType) buffer.readTLObject(APIContext.getInstance());
        mtime = buffer.readInt();
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
        buff.writeTLObject(type);
        buff.writeInt(mtime);
        buff.writeBytes(bytes);
    }


    public int getConstructor() {
        return ID;
    }
}