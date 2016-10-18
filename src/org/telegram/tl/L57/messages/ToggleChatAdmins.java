package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ToggleChatAdmins extends TLObject {

    public static final int ID = 0xec8bd9e1;

    public int chat_id;
    public boolean enabled;

    public ToggleChatAdmins() {
    }

    public ToggleChatAdmins(int chat_id, boolean enabled) {
        this.chat_id = chat_id;
        this.enabled = enabled;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        enabled = buffer.readBool();
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
        buff.writeBool(enabled);
    }


    public int getConstructor() {
        return ID;
    }
}