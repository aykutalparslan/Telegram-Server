package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateContactRegistered extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x2575bbb9;

    public int user_id;
    public int date;

    public UpdateContactRegistered() {
    }

    public UpdateContactRegistered(int user_id, int date) {
        this.user_id = user_id;
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        date = buffer.readInt();
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
        buff.writeInt(user_id);
        buff.writeInt(date);
    }


    public int getConstructor() {
        return ID;
    }
}