package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InlineBotSwitchPM extends org.telegram.tl.TLInlineBotSwitchPM {

    public static final int ID = 0x3c20629f;

    public String text;
    public String start_param;

    public InlineBotSwitchPM() {
    }

    public InlineBotSwitchPM(String text, String start_param) {
        this.text = text;
        this.start_param = start_param;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        text = buffer.readString();
        start_param = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeString(text);
        buff.writeString(start_param);
    }


    public int getConstructor() {
        return ID;
    }
}