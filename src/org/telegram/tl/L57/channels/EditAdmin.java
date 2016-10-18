package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EditAdmin extends TLObject {

    public static final int ID = 0xeb7611d0;

    public TLInputChannel channel;
    public TLInputUser user_id;
    public TLChannelParticipantRole role;

    public EditAdmin() {
    }

    public EditAdmin(TLInputChannel channel, TLInputUser user_id, TLChannelParticipantRole role) {
        this.channel = channel;
        this.user_id = user_id;
        this.role = role;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
        role = (TLChannelParticipantRole) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(channel);
        buff.writeTLObject(user_id);
        buff.writeTLObject(role);
    }


    public int getConstructor() {
        return ID;
    }
}