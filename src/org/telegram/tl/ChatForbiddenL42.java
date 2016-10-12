package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class ChatForbiddenL42 extends TLChat {

    public static final int ID = 0x7328bdb;

    public int id;
    public String title;

    public ChatForbiddenL42() {
    }

    public ChatForbiddenL42(int id, String title) {
        this.id = id;
        this.title = title;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readInt();
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
        buff.writeInt(id);
        buff.writeString(title);
    }


    public int getConstructor() {
        return ID;
    }
}