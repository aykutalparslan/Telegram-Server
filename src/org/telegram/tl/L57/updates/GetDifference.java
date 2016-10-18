package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetDifference extends TLObject {

    public static final int ID = 0xa041495;

    public int pts;
    public int date;
    public int qts;

    public GetDifference() {
    }

    public GetDifference(int pts, int date, int qts) {
        this.pts = pts;
        this.date = date;
        this.qts = qts;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        pts = buffer.readInt();
        date = buffer.readInt();
        qts = buffer.readInt();
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
        buff.writeInt(pts);
        buff.writeInt(date);
        buff.writeInt(qts);
    }


    public int getConstructor() {
        return ID;
    }
}