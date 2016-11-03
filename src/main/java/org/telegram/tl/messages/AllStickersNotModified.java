package org.telegram.tl.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class AllStickersNotModified extends TLAllStickers {

    public static final int ID = 0xe86602c3;


    public AllStickersNotModified(){
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
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
    }


    public int getConstructor() {
        return ID;
    }
}