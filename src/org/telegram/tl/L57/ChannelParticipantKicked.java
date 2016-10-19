package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChannelParticipantKicked extends org.telegram.tl.TLChannelParticipant {

    public static final int ID = 0x8cc5e69a;

    public int user_id;
    public int kicked_by;
    public int date;

    public ChannelParticipantKicked() {
    }

    public ChannelParticipantKicked(int user_id, int kicked_by, int date) {
        this.user_id = user_id;
        this.kicked_by = kicked_by;
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        kicked_by = buffer.readInt();
        date = buffer.readInt();
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
        buff.writeInt(user_id);
        buff.writeInt(kicked_by);
        buff.writeInt(date);
    }


    public int getConstructor() {
        return ID;
    }
}