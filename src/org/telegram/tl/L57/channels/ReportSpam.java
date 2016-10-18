package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ReportSpam extends TLObject {

    public static final int ID = 0xfe087810;

    public TLInputChannel channel;
    public TLInputUser user_id;
    public TLVector<Integer> id;

    public ReportSpam() {
        this.id = new TLVector<>();
    }

    public ReportSpam(TLInputChannel channel, TLInputUser user_id, TLVector<Integer> id) {
        this.channel = channel;
        this.user_id = user_id;
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(user_id);
        buff.writeTLObject(id);
    }


    public int getConstructor() {
        return ID;
    }
}