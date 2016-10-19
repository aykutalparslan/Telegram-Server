package org.telegram.tl.L57.auth;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SentCodeTypeApp extends org.telegram.tl.auth.TLSentCodeType {

    public static final int ID = 0x3dbb5986;

    public int length;

    public SentCodeTypeApp() {
    }

    public SentCodeTypeApp(int length) {
        this.length = length;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        length = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(8);
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