package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MaskCoords extends TLMaskCoords {

    public static final int ID = 0xaed6dbb2;

    public int n;
    public double x;
    public double y;
    public double zoom;

    public MaskCoords() {
    }

    public MaskCoords(int n, double x, double y, double zoom) {
        this.n = n;
        this.x = x;
        this.y = y;
        this.zoom = zoom;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        n = buffer.readInt();
        x = buffer.readDouble();
        y = buffer.readDouble();
        zoom = buffer.readDouble();
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
        buff.writeInt(n);
        buff.writeDouble(x);
        buff.writeDouble(y);
        buff.writeDouble(zoom);
    }


    public int getConstructor() {
        return ID;
    }
}