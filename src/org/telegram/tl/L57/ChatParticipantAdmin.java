package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatParticipantAdmin extends TLChatParticipant {

    public static final int ID = 0xe2d6e436;

    public int user_id;
    public int inviter_id;
    public int date;

    public ChatParticipantAdmin() {
    }

    public ChatParticipantAdmin(int user_id, int inviter_id, int date) {
        this.user_id = user_id;
        this.inviter_id = inviter_id;
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        inviter_id = buffer.readInt();
        date = buffer.readInt();
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
        buff.writeInt(user_id);
        buff.writeInt(inviter_id);
        buff.writeInt(date);
    }


    public int getConstructor() {
        return ID;
    }
}