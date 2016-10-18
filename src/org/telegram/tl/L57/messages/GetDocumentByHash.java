package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetDocumentByHash extends TLObject {

    public static final int ID = 0x338e2464;

    public byte[] sha256;
    public int size;
    public String mime_type;

    public GetDocumentByHash() {
    }

    public GetDocumentByHash(byte[] sha256, int size, String mime_type) {
        this.sha256 = sha256;
        this.size = size;
        this.mime_type = mime_type;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        sha256 = buffer.readBytes();
        size = buffer.readInt();
        mime_type = buffer.readString();
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
        buff.writeBytes(sha256);
        buff.writeInt(size);
        buff.writeString(mime_type);
    }


    public int getConstructor() {
        return ID;
    }
}