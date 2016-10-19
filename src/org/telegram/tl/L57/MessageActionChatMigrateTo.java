package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageActionChatMigrateTo extends org.telegram.tl.TLMessageAction {

    public static final int ID = 0x51bdb021;

    public int channel_id;

    public MessageActionChatMigrateTo() {
    }

    public MessageActionChatMigrateTo(int channel_id) {
        this.channel_id = channel_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(8);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(channel_id);
    }


    public int getConstructor() {
        return ID;
    }
}