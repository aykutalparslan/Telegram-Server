package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetChannelDifference extends TLObject {

    public static final int ID = 0xbb32d7c0;

    public TLInputChannel channel;
    public TLChannelMessagesFilter filter;
    public int pts;
    public int limit;

    public GetChannelDifference() {
    }

    public GetChannelDifference(TLInputChannel channel, TLChannelMessagesFilter filter, int pts, int limit) {
        this.channel = channel;
        this.filter = filter;
        this.pts = pts;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        filter = (TLChannelMessagesFilter) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
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
        buff.writeTLObject(channel);
        buff.writeTLObject(filter);
        buff.writeInt(pts);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}