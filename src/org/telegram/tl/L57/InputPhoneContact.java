package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputPhoneContact extends org.telegram.tl.TLInputContact {

    public static final int ID = 0xf392b7f4;

    public long client_id;
    public String phone;
    public String first_name;
    public String last_name;

    public InputPhoneContact() {
    }

    public InputPhoneContact(long client_id, String phone, String first_name, String last_name) {
        this.client_id = client_id;
        this.phone = phone;
        this.first_name = first_name;
        this.last_name = last_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        client_id = buffer.readLong();
        phone = buffer.readString();
        first_name = buffer.readString();
        last_name = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeLong(client_id);
        buff.writeString(phone);
        buff.writeString(first_name);
        buff.writeString(last_name);
    }


    public int getConstructor() {
        return ID;
    }
}