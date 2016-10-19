package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SentCodeTypeFlashCall extends org.telegram.tl.auth.TLSentCodeType {

    public static final int ID = 0xab03c6d9;

    public String pattern;

    public SentCodeTypeFlashCall() {
    }

    public SentCodeTypeFlashCall(String pattern) {
        this.pattern = pattern;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        pattern = buffer.readString();
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
        buff.writeString(pattern);
    }


    public int getConstructor() {
        return ID;
    }
}