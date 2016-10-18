package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class KeyboardButtonCallback extends TLKeyboardButton {

    public static final int ID = 0x683a5e46;

    public String text;
    public byte[] data;

    public KeyboardButtonCallback() {
    }

    public KeyboardButtonCallback(String text, byte[] data) {
        this.text = text;
        this.data = data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        text = buffer.readString();
        data = buffer.readBytes();
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
        buff.writeString(text);
        buff.writeBytes(data);
    }


    public int getConstructor() {
        return ID;
    }
}