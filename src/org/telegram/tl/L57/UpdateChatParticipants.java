package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChatParticipants extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x7761198;

    public org.telegram.tl.TLChatParticipants participants;

    public UpdateChatParticipants() {
    }

    public UpdateChatParticipants(org.telegram.tl.TLChatParticipants participants) {
        this.participants = participants;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        participants = (org.telegram.tl.TLChatParticipants) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(participants);
    }


    public int getConstructor() {
        return ID;
    }
}