package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputGeoPoint extends org.telegram.tl.TLInputGeoPoint {

    public static final int ID = 0xf3b7acc9;

    public double lat;
    public double lon;

    public InputGeoPoint() {
    }

    public InputGeoPoint(double lat, double lon) {
        this.lat = lat;
        this.lon = lon;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        lat = buffer.readDouble();
        lon = buffer.readDouble();
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
        buff.writeDouble(lat);
        buff.writeDouble(lon);
    }


    public int getConstructor() {
        return ID;
    }
}