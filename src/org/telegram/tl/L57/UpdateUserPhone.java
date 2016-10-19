package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateUserPhone extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x12b9417b;

    public int user_id;
    public String phone;

    public UpdateUserPhone() {
    }

    public UpdateUserPhone(int user_id, String phone) {
        this.user_id = user_id;
        this.phone = phone;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        phone = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(user_id);
        buff.writeString(phone);
    }


    public int getConstructor() {
        return ID;
    }
}