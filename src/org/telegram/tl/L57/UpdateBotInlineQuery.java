package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateBotInlineQuery extends TLUpdate {

    public static final int ID = 0x54826690;

    public int flags;
    public long query_id;
    public int user_id;
    public String query;
    public TLGeoPoint geo;
    public String offset;

    public UpdateBotInlineQuery() {
    }

    public UpdateBotInlineQuery(int flags, long query_id, int user_id, String query, TLGeoPoint geo, String offset) {
        this.flags = flags;
        this.query_id = query_id;
        this.user_id = user_id;
        this.query = query;
        this.geo = geo;
        this.offset = offset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        query_id = buffer.readLong();
        user_id = buffer.readInt();
        query = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            geo = (TLGeoPoint) buffer.readTLObject(APIContext.getInstance());
        }
        offset = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (geo != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeLong(query_id);
        buff.writeInt(user_id);
        buff.writeString(query);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(geo);
        }
        buff.writeString(offset);
    }


    public int getConstructor() {
        return ID;
    }
}