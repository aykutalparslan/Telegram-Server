package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateReadChannelOutbox extends TLUpdate {

    public static final int ID = 0x25d6c9c7;

    public int channel_id;
    public int max_id;

    public UpdateReadChannelOutbox() {
    }

    public UpdateReadChannelOutbox(int channel_id, int max_id) {
        this.channel_id = channel_id;
        this.max_id = max_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
        max_id = buffer.readInt();
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
        buff.writeInt(channel_id);
        buff.writeInt(max_id);
    }


    public int getConstructor() {
        return ID;
    }
}