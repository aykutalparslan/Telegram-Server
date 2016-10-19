package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class PeerChannel extends org.telegram.tl.TLPeer {

    public static final int ID = 0xbddde532;

    public int channel_id;

    public PeerChannel() {
    }

    public PeerChannel(int channel_id) {
        this.channel_id = channel_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel_id = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(8);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(channel_id);
    }


    public int getConstructor() {
        return ID;
    }
}