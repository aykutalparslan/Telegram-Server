package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DeleteMessages extends TLObject {

    public static final int ID = 0x84c1fd4e;

    public TLInputChannel channel;
    public TLVector<Integer> id;

    public DeleteMessages() {
        this.id = new TLVector<>();
    }

    public DeleteMessages(TLInputChannel channel, TLVector<Integer> id) {
        this.channel = channel;
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        id = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(id);
    }


    public int getConstructor() {
        return ID;
    }
}