package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class KickFromChannel extends TLObject {

    public static final int ID = 0xa672de14;

    public TLInputChannel channel;
    public TLInputUser user_id;
    public boolean kicked;

    public KickFromChannel() {
    }

    public KickFromChannel(TLInputChannel channel, TLInputUser user_id, boolean kicked) {
        this.channel = channel;
        this.user_id = user_id;
        this.kicked = kicked;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        kicked = buffer.readBool();
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
        buff.writeTLObject(user_id);
        buff.writeBool(kicked);
    }


    public int getConstructor() {
        return ID;
    }
}