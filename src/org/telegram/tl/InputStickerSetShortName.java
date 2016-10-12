package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputStickerSetShortName extends TLInputStickerSet {

    public static final int ID = 0x861cc8a0;

    public String short_name;

    public InputStickerSetShortName() {
    }

    public InputStickerSetShortName(String short_name) {
        this.short_name = short_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        short_name = buffer.readString();
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
        buff.writeString(short_name);
    }


    public int getConstructor() {
        return ID;
    }
}