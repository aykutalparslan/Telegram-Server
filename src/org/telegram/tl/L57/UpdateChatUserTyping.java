package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChatUserTyping extends TLUpdate {

    public static final int ID = 0x9a65ea1f;

    public int chat_id;
    public int user_id;
    public TLSendMessageAction action;

    public UpdateChatUserTyping() {
    }

    public UpdateChatUserTyping(int chat_id, int user_id, TLSendMessageAction action) {
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.action = action;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = buffer.readInt();
        action = (TLSendMessageAction) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(user_id);
        buff.writeTLObject(action);
    }


    public int getConstructor() {
        return ID;
    }
}