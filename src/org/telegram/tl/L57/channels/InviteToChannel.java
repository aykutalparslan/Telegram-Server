package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InviteToChannel extends TLObject {

    public static final int ID = 0x199f3a6c;

    public TLInputChannel channel;
    public TLVector<TLInputUser> users;

    public InviteToChannel() {
        this.users = new TLVector<>();
    }

    public InviteToChannel(TLInputChannel channel, TLVector<TLInputUser> users) {
        this.channel = channel;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLInputUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}