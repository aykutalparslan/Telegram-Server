package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Contact extends org.telegram.tl.TLContact {

    public static final int ID = 0xf911c994;

    public int user_id;
    public boolean mutual;

    public Contact() {
    }

    public Contact(int user_id, boolean mutual) {
        this.user_id = user_id;
        this.mutual = mutual;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        mutual = buffer.readBool();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(9);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(user_id);
        buff.writeBool(mutual);
    }


    public int getConstructor() {
        return ID;
    }
}