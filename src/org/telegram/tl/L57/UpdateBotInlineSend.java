package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateBotInlineSend extends TLUpdate {

    public static final int ID = 0xe48f964;

    public int flags;
    public int user_id;
    public String query;
    public TLGeoPoint geo;
    public String id;
    public TLInputBotInlineMessageID msg_id;

    public UpdateBotInlineSend() {
    }

    public UpdateBotInlineSend(int flags, int user_id, String query, TLGeoPoint geo, String id, TLInputBotInlineMessageID msg_id) {
        this.flags = flags;
        this.user_id = user_id;
        this.query = query;
        this.geo = geo;
        this.id = id;
        this.msg_id = msg_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        user_id = buffer.readInt();
        query = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            geo = (TLGeoPoint) buffer.readTLObject(APIContext.getInstance());
        }
        id = buffer.readString();
        if ((flags & (1 << 1)) != 0) {
            msg_id = (TLInputBotInlineMessageID) buffer.readTLObject(APIContext.getInstance());
        }
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
        if (msg_id != null) {
            flags |= (1 << 1);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(user_id);
        buff.writeString(query);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(geo);
        }
        buff.writeString(id);
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(msg_id);
        }
    }


    public int getConstructor() {
        return ID;
    }
}