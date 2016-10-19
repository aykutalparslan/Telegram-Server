package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DifferenceEmpty extends org.telegram.tl.updates.TLDifference {

    public static final int ID = 0x5d75a138;

    public int date;
    public int seq;

    public DifferenceEmpty() {
    }

    public DifferenceEmpty(int date, int seq) {
        this.date = date;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        date = buffer.readInt();
        seq = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(12);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(date);
        buff.writeInt(seq);
    }


    public int getConstructor() {
        return ID;
    }
}