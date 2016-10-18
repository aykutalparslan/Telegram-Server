package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatParticipants extends TLChatParticipants {

    public static final int ID = 0x3f460fed;

    public int chat_id;
    public TLVector<TLChatParticipant> participants;
    public int version;

    public ChatParticipants() {
        this.participants = new TLVector<>();
    }

    public ChatParticipants(int chat_id, TLVector<TLChatParticipant> participants, int version) {
        this.chat_id = chat_id;
        this.participants = participants;
        this.version = version;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        participants = (TLVector<TLChatParticipant>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(participants);
        buff.writeInt(version);
    }


    public int getConstructor() {
        return ID;
    }
}