package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChatParticipantAdd extends org.telegram.tl.TLUpdate {

    public static final int ID = 0xea4b0e5c;

    public int chat_id;
    public int user_id;
    public int inviter_id;
    public int date;
    public int version;

    public UpdateChatParticipantAdd() {
    }

    public UpdateChatParticipantAdd(int chat_id, int user_id, int inviter_id, int date, int version) {
        this.chat_id = chat_id;
        this.user_id = user_id;
        this.inviter_id = inviter_id;
        this.date = date;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        user_id = buffer.readInt();
        inviter_id = buffer.readInt();
        date = buffer.readInt();
        version = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(chat_id);
        buff.writeInt(user_id);
        buff.writeInt(inviter_id);
        buff.writeInt(date);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}