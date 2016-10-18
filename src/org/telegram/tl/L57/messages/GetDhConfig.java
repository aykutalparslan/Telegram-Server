package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GetDhConfig extends TLObject {

    public static final int ID = 0x26cf8950;

    public int version;
    public int random_length;

    public GetDhConfig() {
    }

    public GetDhConfig(int version, int random_length) {
        this.version = version;
        this.random_length = random_length;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        version = buffer.readInt();
        random_length = buffer.readInt();
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
        buff.writeInt(version);
        buff.writeInt(random_length);
    }


    public int getConstructor() {
        return ID;
    }
}