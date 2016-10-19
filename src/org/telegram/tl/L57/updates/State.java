package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class State extends org.telegram.tl.updates.TLState {

    public static final int ID = 0xa56c2a3e;

    public int pts;
    public int qts;
    public int date;
    public int seq;
    public int unread_count;

    public State() {
    }

    public State(int pts, int qts, int date, int seq, int unread_count) {
        this.pts = pts;
        this.qts = qts;
        this.date = date;
        this.seq = seq;
        this.unread_count = unread_count;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        pts = buffer.readInt();
        qts = buffer.readInt();
        date = buffer.readInt();
        seq = buffer.readInt();
        unread_count = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(pts);
        buff.writeInt(qts);
        buff.writeInt(date);
        buff.writeInt(seq);
        buff.writeInt(unread_count);
    }


    public int getConstructor() {
        return ID;
    }
}