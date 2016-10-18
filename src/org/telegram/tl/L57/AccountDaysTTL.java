package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class AccountDaysTTL extends TLAccountDaysTTL {

    public static final int ID = 0xb8d0afdf;

    public int days;

    public AccountDaysTTL() {
    }

    public AccountDaysTTL(int days) {
        this.days = days;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        days = buffer.readInt();
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
        buff.writeInt(days);
    }


    public int getConstructor() {
        return ID;
    }
}