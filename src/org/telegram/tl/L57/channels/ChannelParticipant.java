package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelParticipant extends TLChannelParticipant {

    public static final int ID = 0xd0d9b163;

    public TLChannelParticipant participant;
    public TLVector<TLUser> users;

    public ChannelParticipant() {
        this.users = new TLVector<>();
    }

    public ChannelParticipant(TLChannelParticipant participant, TLVector<TLUser> users) {
        this.participant = participant;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        participant = (TLChannelParticipant) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(participant);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}