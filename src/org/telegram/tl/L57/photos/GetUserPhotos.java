package org.telegram.tl.L57.photos;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetUserPhotos extends TLObject {

    public static final int ID = 0x91cd32a8;

    public TLInputUser user_id;
    public int offset;
    public long max_id;
    public int limit;

    public GetUserPhotos() {
    }

    public GetUserPhotos(TLInputUser user_id, int offset, long max_id, int limit) {
        this.user_id = user_id;
        this.offset = offset;
        this.max_id = max_id;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        offset = buffer.readInt();
        max_id = buffer.readLong();
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
        buff.writeTLObject(user_id);
        buff.writeInt(offset);
        buff.writeLong(max_id);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}