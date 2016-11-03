package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputChannel extends TLInputChannel {

    public static final int ID = 0xafeb712e;

    public int channel_id;
    public long access_hash;

    public InputChannel() {
    }

    public InputChannel(int channel_id, long access_hash) {
        this.channel_id = channel_id;
        this.access_hash = access_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
        access_hash = buffer.readLong();
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
        buff.writeLong(access_hash);
    }


    public int getConstructor() {
        return ID;
    }
}