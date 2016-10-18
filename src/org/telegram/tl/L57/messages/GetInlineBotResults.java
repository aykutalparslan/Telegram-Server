package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetInlineBotResults extends TLObject {

    public static final int ID = 0x514e999d;

    public int flags;
    public TLInputUser bot;
    public TLInputPeer peer;
    public TLInputGeoPoint geo_point;
    public String query;
    public String offset;

    public GetInlineBotResults() {
    }

    public GetInlineBotResults(int flags, TLInputUser bot, TLInputPeer peer, TLInputGeoPoint geo_point, String query, String offset) {
        this.flags = flags;
        this.bot = bot;
        this.peer = peer;
        this.geo_point = geo_point;
        this.query = query;
        this.offset = offset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        bot = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            geo_point = (TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
        }
        query = buffer.readString();
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
        if (geo_point != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(bot);
        buff.writeTLObject(peer);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(geo_point);
        }
        buff.writeString(query);
        buff.writeString(offset);
    }


    public int getConstructor() {
        return ID;
    }
}