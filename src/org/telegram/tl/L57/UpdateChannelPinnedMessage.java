package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateChannelPinnedMessage extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x98592475;

    public int channel_id;
    public int id;

    public UpdateChannelPinnedMessage() {
    }

    public UpdateChannelPinnedMessage(int channel_id, int id) {
        this.channel_id = channel_id;
        this.id = id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
        id = buffer.readInt();
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
        buff.writeInt(channel_id);
        buff.writeInt(id);
    }


    public int getConstructor() {
        return ID;
    }
}