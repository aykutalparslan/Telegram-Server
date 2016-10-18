package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateDeviceLocked extends TLObject {

    public static final int ID = 0x38df3532;

    public int period;

    public UpdateDeviceLocked() {
    }

    public UpdateDeviceLocked(int period) {
        this.period = period;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        period = buffer.readInt();
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
        buff.writeInt(period);
    }


    public int getConstructor() {
        return ID;
    }
}