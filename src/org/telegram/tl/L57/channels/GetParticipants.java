package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetParticipants extends TLObject {

    public static final int ID = 0x24d98f92;

    public TLInputChannel channel;
    public TLChannelParticipantsFilter filter;
    public int offset;
    public int limit;

    public GetParticipants() {
    }

    public GetParticipants(TLInputChannel channel, TLChannelParticipantsFilter filter, int offset, int limit) {
        this.channel = channel;
        this.filter = filter;
        this.offset = offset;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        filter = (TLChannelParticipantsFilter) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(channel);
        buff.writeTLObject(filter);
        buff.writeInt(offset);
        buff.writeInt(limit);
    }


    public int getConstructor() {
        return ID;
    }
}