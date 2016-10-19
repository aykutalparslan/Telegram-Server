package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class KeyboardButtonRequestPhone extends org.telegram.tl.TLKeyboardButton {

    public static final int ID = 0xb16a6c29;

    public String text;

    public KeyboardButtonRequestPhone() {
    }

    public KeyboardButtonRequestPhone(String text) {
        this.text = text;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        text = buffer.readString();
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
        buff.writeString(text);
    }


    public int getConstructor() {
        return ID;
    }
}