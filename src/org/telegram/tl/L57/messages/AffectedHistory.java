package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class AffectedHistory extends TLAffectedHistory {

    public static final int ID = 0xb45c69d1;

    public int pts;
    public int pts_count;
    public int offset;

    public AffectedHistory() {
    }

    public AffectedHistory(int pts, int pts_count, int offset) {
        this.pts = pts;
        this.pts_count = pts_count;
        this.offset = offset;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        pts = buffer.readInt();
        pts_count = buffer.readInt();
        offset = buffer.readInt();
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
        buff.writeInt(pts_count);
        buff.writeInt(offset);
    }


    public int getConstructor() {
        return ID;
    }
}