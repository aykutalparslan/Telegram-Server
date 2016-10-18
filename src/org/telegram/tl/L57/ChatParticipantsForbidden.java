package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatParticipantsForbidden extends TLChatParticipants {

    public static final int ID = 0xfc900c2b;

    public int flags;
    public int chat_id;
    public TLChatParticipant self_participant;

    public ChatParticipantsForbidden() {
    }

    public ChatParticipantsForbidden(int flags, int chat_id, TLChatParticipant self_participant) {
        this.flags = flags;
        this.chat_id = chat_id;
        this.self_participant = self_participant;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        chat_id = buffer.readInt();
        if ((flags & (1 << 0)) != 0) {
            self_participant = (TLChatParticipant) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (self_participant != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeInt(chat_id);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(self_participant);
        }
    }


    public int getConstructor() {
        return ID;
    }
}