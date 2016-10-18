package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageActionChatEditTitle extends TLMessageAction {

    public static final int ID = 0xb5a1ce5a;

    public String title;

    public MessageActionChatEditTitle() {
    }

    public MessageActionChatEditTitle(String title) {
        this.title = title;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        title = buffer.readString();
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
        buff.writeString(title);
    }


    public int getConstructor() {
        return ID;
    }
}