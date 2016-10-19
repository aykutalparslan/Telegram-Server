package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateUserName extends org.telegram.tl.TLUpdate {

    public static final int ID = 0xa7332b73;

    public int user_id;
    public String first_name;
    public String last_name;
    public String username;

    public UpdateUserName() {
    }

    public UpdateUserName(int user_id, String first_name, String last_name, String username) {
        this.user_id = user_id;
        this.first_name = first_name;
        this.last_name = last_name;
        this.username = username;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        first_name = buffer.readString();
        last_name = buffer.readString();
        username = buffer.readString();
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
        buff.writeInt(user_id);
        buff.writeString(first_name);
        buff.writeString(last_name);
        buff.writeString(username);
    }


    public int getConstructor() {
        return ID;
    }
}