package org.telegram.tl.L57.upload;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetFile extends TLObject {

    public static final int ID = 0xe3a6cfb5;

    public TLInputFileLocation location;
    public int offset;
    public int limit;

    public GetFile() {
    }

    public GetFile(TLInputFileLocation location, int offset, int limit) {
        this.location = location;
        this.offset = offset;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        location = (TLInputFileLocation) buffer.readTLObject(APIContext.getInstance());
        offset = buffer.readInt();
        limit = buffer.readInt();
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
        buff.writeTLObject(location);
        buff.writeInt(offset);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}