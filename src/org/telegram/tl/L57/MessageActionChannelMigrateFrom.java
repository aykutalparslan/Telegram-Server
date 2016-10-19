package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageActionChannelMigrateFrom extends org.telegram.tl.TLMessageAction {

    public static final int ID = 0xb055eaee;

    public String title;
    public int chat_id;

    public MessageActionChannelMigrateFrom() {
    }

    public MessageActionChannelMigrateFrom(String title, int chat_id) {
        this.title = title;
        this.chat_id = chat_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        title = buffer.readString();
        chat_id = buffer.readInt();
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
        buff.writeString(title);
        buff.writeInt(chat_id);
    }


    public int getConstructor() {
        return ID;
    }
}