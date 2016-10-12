package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class UpdateStickerSets extends TLUpdate {

    public static final int ID = 0x43ae3dec;


    public UpdateStickerSets() {
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