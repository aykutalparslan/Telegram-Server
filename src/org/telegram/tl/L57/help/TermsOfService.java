package org.telegram.tl.L57.help;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class TermsOfService extends org.telegram.tl.help.TLTermsOfService {

    public static final int ID = 0xf1ee3e90;

    public String text;

    public TermsOfService() {
    }

    public TermsOfService(String text) {
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