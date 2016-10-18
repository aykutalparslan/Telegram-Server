package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class AddChatUser extends TLObject {

    public static final int ID = 0xf9a0aa09;

    public int chat_id;
    public TLInputUser user_id;
    public int fwd_limit;

    public AddChatUser() {
    }

    public AddChatUser(int chat_id, TLInputUser user_id, int fwd_limit) {
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.fwd_limit = fwd_limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        fwd_limit = buffer.readInt();
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
        buff.writeInt(fwd_limit);
    }


    public int getConstructor() {
        return ID;
    }
}