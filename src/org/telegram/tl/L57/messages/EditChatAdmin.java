package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EditChatAdmin extends TLObject {

    public static final int ID = 0xa9e69f2e;

    public int chat_id;
    public TLInputUser user_id;
    public boolean is_admin;

    public EditChatAdmin() {
    }

    public EditChatAdmin(int chat_id, TLInputUser user_id, boolean is_admin) {
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.is_admin = is_admin;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        is_admin = buffer.readBool();
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
        buff.writeInt(chat_id);
        buff.writeTLObject(user_id);
        buff.writeBool(is_admin);
    }


    public int getConstructor() {
        return ID;
    }
}