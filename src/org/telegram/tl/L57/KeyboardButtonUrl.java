package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class KeyboardButtonUrl extends TLKeyboardButton {

    public static final int ID = 0x258aff05;

    public String text;
    public String url;

    public KeyboardButtonUrl() {
    }

    public KeyboardButtonUrl(String text, String url) {
        this.text = text;
        this.url = url;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        text = buffer.readString();
        url = buffer.readString();
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
        buff.writeString(url);
    }


    public int getConstructor() {
        return ID;
    }
}