package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaGeoPoint extends TLInputMedia {

    public static final int ID = 0xf9c44144;

    public TLInputGeoPoint geo_point;

    public InputMediaGeoPoint() {
    }

    public InputMediaGeoPoint(TLInputGeoPoint geo_point){
        this.geo_point = geo_point;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        geo_point = (TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(geo_point);
    }

    public int getConstructor() {
        return ID;
    }
}