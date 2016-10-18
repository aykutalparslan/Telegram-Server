package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelParticipants extends TLChannelParticipants {

    public static final int ID = 0xf56ee2a8;

    public int count;
    public TLVector<TLChannelParticipant> participants;
    public TLVector<TLUser> users;

    public ChannelParticipants() {
        this.participants = new TLVector<>();
        this.users = new TLVector<>();
    }

    public ChannelParticipants(int count, TLVector<TLChannelParticipant> participants, TLVector<TLUser> users) {
        this.count = count;
        this.participants = participants;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        count = buffer.readInt();
        participants = (TLVector<TLChannelParticipant>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(count);
        buff.writeTLObject(participants);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}