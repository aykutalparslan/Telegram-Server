package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DhConfig extends TLDhConfig {

    public static final int ID = 0x2c221edd;

    public int g;
    public byte[] p;
    public int version;
    public byte[] random;

    public DhConfig() {
    }

    public DhConfig(int g, byte[] p, int version, byte[] random) {
        this.g = g;
        this.p = p;
        this.version = version;
        this.random = random;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        g = buffer.readInt();
        p = buffer.readBytes();
        version = buffer.readInt();
        random = buffer.readBytes();
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
        buff.writeInt(g);
        buff.writeBytes(p);
        buff.writeInt(version);
        buff.writeBytes(random);
    }


    public int getConstructor() {
        return ID;
    }
}