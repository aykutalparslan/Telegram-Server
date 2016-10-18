package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SetAccountTTL extends TLObject {

    public static final int ID = 0x2442485e;

    public TLAccountDaysTTL ttl;

    public SetAccountTTL() {
    }

    public SetAccountTTL(TLAccountDaysTTL ttl) {
        this.ttl = ttl;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        ttl = (TLAccountDaysTTL) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(ttl);
    }


    public int getConstructor() {
        return ID;
    }
}