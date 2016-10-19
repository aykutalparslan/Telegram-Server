package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class NearestDc extends org.telegram.tl.TLNearestDc {

    public static final int ID = 0x8e1a1775;

    public String country;
    public int this_dc;
    public int nearest_dc;

    public NearestDc() {
    }

    public NearestDc(String country, int this_dc, int nearest_dc) {
        this.country = country;
        this.this_dc = this_dc;
        this.nearest_dc = nearest_dc;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        country = buffer.readString();
        this_dc = buffer.readInt();
        nearest_dc = buffer.readInt();
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
        buff.writeString(country);
        buff.writeInt(this_dc);
        buff.writeInt(nearest_dc);
    }


    public int getConstructor() {
        return ID;
    }
}