package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelParticipants extends org.telegram.tl.channels.TLChannelParticipants {

    public static final int ID = 0xf56ee2a8;

    public int count;
    public TLVector<org.telegram.tl.TLChannelParticipant> participants;
    public TLVector<org.telegram.tl.TLUser> users;

    public ChannelParticipants() {
        this.participants = new TLVector<>();
        this.users = new TLVector<>();
    }

    public ChannelParticipants(int count, TLVector<org.telegram.tl.TLChannelParticipant> participants, TLVector<org.telegram.tl.TLUser> users) {
        this.count = count;
        this.participants = participants;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        count = buffer.readInt();
        participants = (TLVector<org.telegram.tl.TLChannelParticipant>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(count);
        buff.writeTLObject(participants);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}