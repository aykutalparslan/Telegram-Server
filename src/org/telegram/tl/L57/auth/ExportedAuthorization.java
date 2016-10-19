package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ExportedAuthorization extends org.telegram.tl.auth.TLExportedAuthorization {

    public static final int ID = 0xdf969c2d;

    public int id;
    public byte[] bytes;

    public ExportedAuthorization() {
    }

    public ExportedAuthorization(int id, byte[] bytes) {
        this.id = id;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
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
        buff.writeInt(id);
        buff.writeBytes(bytes);
    }


    public int getConstructor() {
        return ID;
    }
}