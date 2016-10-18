package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SentCodeTypeSms extends TLSentCodeType {

    public static final int ID = 0xc000bba2;

    public int length;

    public SentCodeTypeSms() {
    }

    public SentCodeTypeSms(int length) {
        this.length = length;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        length = buffer.readInt();
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
        buff.writeInt(length);
    }


    public int getConstructor() {
        return ID;
    }
}