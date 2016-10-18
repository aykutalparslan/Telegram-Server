package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateDeleteChannelMessages extends TLUpdate {

    public static final int ID = 0xc37521c9;

    public int channel_id;
    public TLVector<Integer> messages;
    public int pts;
    public int pts_count;

    public UpdateDeleteChannelMessages() {
        this.messages = new TLVector<>();
    }

    public UpdateDeleteChannelMessages(int channel_id, TLVector<Integer> messages, int pts, int pts_count) {
        this.channel_id = channel_id;
        this.messages = messages;
        this.pts = pts;
        this.pts_count = pts_count;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
        messages = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
        pts_count = buffer.readInt();
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
        buff.writeInt(channel_id);
        buff.writeTLObject(messages);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
    }


    public int getConstructor() {
        return ID;
    }
}