package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChatParticipantDelete extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x6e5f8c22;

    public int chat_id;
    public int user_id;
    public int version;

    public UpdateChatParticipantDelete() {
    }

    public UpdateChatParticipantDelete(int chat_id, int user_id, int version) {
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = buffer.readInt();
        version = buffer.readInt();
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
        buff.writeInt(chat_id);
        buff.writeInt(user_id);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}