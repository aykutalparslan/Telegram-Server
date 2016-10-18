package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateShort extends TLUpdates {

    public static final int ID = 0x78d4dec1;

    public TLUpdate update;
    public int date;

    public UpdateShort() {
    }

    public UpdateShort(TLUpdate update, int date) {
        this.update = update;
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        update = (TLUpdate) buffer.readTLObject(APIContext.getInstance());
        date = buffer.readInt();
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
        buff.writeTLObject(update);
        buff.writeInt(date);
    }


    public int getConstructor() {
        return ID;
    }
}