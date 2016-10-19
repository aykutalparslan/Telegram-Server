package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageMediaContact extends org.telegram.tl.TLMessageMedia {

    public static final int ID = 0x5e7d2f39;

    public String phone_number;
    public String first_name;
    public String last_name;
    public int user_id;

    public MessageMediaContact() {
    }

    public MessageMediaContact(String phone_number, String first_name, String last_name, int user_id) {
        this.phone_number = phone_number;
        this.first_name = first_name;
        this.last_name = last_name;
        this.user_id = user_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        phone_number = buffer.readString();
        first_name = buffer.readString();
        last_name = buffer.readString();
        user_id = buffer.readInt();
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
        buff.writeString(phone_number);
        buff.writeString(first_name);
        buff.writeString(last_name);
        buff.writeInt(user_id);
    }


    public int getConstructor() {
        return ID;
    }
}