package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChatParticipantAdmin extends TLUpdate {

    public static final int ID = 0xb6901959;

    public int chat_id;
    public int user_id;
    public boolean is_admin;
    public int version;

    public UpdateChatParticipantAdmin() {
    }

    public UpdateChatParticipantAdmin(int chat_id, int user_id, boolean is_admin, int version) {
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.is_admin = is_admin;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = buffer.readInt();
        is_admin = buffer.readBool();
        version = buffer.readInt();
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
        buff.writeBool(is_admin);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}