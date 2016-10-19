package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChatAdmins extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x6e947941;

    public int chat_id;
    public boolean enabled;
    public int version;

    public UpdateChatAdmins() {
    }

    public UpdateChatAdmins(int chat_id, boolean enabled, int version) {
        this.chat_id = chat_id;
        this.enabled = enabled;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        enabled = buffer.readBool();
        version = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(13);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(chat_id);
        buff.writeBool(enabled);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}