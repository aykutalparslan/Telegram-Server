package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputMediaVenue extends TLInputMedia {

    public static final int ID = 0x2827a81a;

    public TLInputGeoPoint geo_point;
    public String title;
    public String address;
    public String provider;
    public String venue_id;

    public InputMediaVenue() {
    }

    public InputMediaVenue(TLInputGeoPoint geo_point, String title, String address, String provider, String venue_id) {
        this.geo_point = geo_point;
        this.title = title;
        this.address = address;
        this.provider = provider;
        this.venue_id = venue_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        geo_point = (TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
        title = buffer.readString();
        address = buffer.readString();
        provider = buffer.readString();
        venue_id = buffer.readString();
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
        buff.writeString(title);
        buff.writeString(address);
        buff.writeString(provider);
        buff.writeString(venue_id);
    }


    public int getConstructor() {
        return ID;
    }
}