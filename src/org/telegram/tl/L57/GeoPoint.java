package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class GeoPoint extends TLGeoPoint {

    public static final int ID = 0x2049d70c;

    public double lon;
    public double lat;

    public GeoPoint() {
    }

    public GeoPoint(double lon, double lat) {
        this.lon = lon;
        this.lat = lat;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        lon = buffer.readDouble();
        lat = buffer.readDouble();
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
        buff.writeDouble(lon);
        buff.writeDouble(lat);
    }


    public int getConstructor() {
        return ID;
    }
}