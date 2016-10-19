package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputAppEvent extends org.telegram.tl.TLInputAppEvent {

    public static final int ID = 0x770656a8;

    public double time;
    public String type;
    public long peer;
    public String data;

    public InputAppEvent() {
    }

    public InputAppEvent(double time, String type, long peer, String data) {
        this.time = time;
        this.type = type;
        this.peer = peer;
        this.data = data;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        time = buffer.readDouble();
        type = buffer.readString();
        peer = buffer.readLong();
        data = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(36);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeDouble(time);
        buff.writeString(type);
        buff.writeLong(peer);
        buff.writeString(data);
    }


    public int getConstructor() {
        return ID;
    }
}