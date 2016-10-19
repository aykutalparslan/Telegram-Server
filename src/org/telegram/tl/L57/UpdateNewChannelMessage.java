package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateNewChannelMessage extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x62ba04d9;

    public org.telegram.tl.TLMessage message;
    public int pts;
    public int pts_count;

    public UpdateNewChannelMessage() {
    }

    public UpdateNewChannelMessage(org.telegram.tl.TLMessage message, int pts, int pts_count) {
        this.message = message;
        this.pts = pts;
        this.pts_count = pts_count;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        message = (org.telegram.tl.TLMessage) buffer.readTLObject(APIContext.getInstance());
        pts = buffer.readInt();
        pts_count = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(message);
        buff.writeInt(pts);
        buff.writeInt(pts_count);
    }


    public int getConstructor() {
        return ID;
    }
}