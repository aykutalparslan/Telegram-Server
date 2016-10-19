package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageActionChannelCreate extends org.telegram.tl.TLMessageAction {

    public static final int ID = 0x95d2ac92;

    public String title;

    public MessageActionChannelCreate() {
    }

    public MessageActionChannelCreate(String title) {
        this.title = title;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        title = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(12);
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