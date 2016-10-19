package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DhConfigNotModified extends org.telegram.tl.messages.TLDhConfig {

    public static final int ID = 0xc0e24635;

    public byte[] random;

    public DhConfigNotModified() {
    }

    public DhConfigNotModified(byte[] random) {
        this.random = random;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        random = buffer.readBytes();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeBytes(random);
    }


    public int getConstructor() {
        return ID;
    }
}