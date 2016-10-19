package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageMediaVenue extends org.telegram.tl.TLMessageMedia {

    public static final int ID = 0x7912b71f;

    public org.telegram.tl.TLGeoPoint geo;
    public String title;
    public String address;
    public String provider;
    public String venue_id;

    public MessageMediaVenue() {
    }

    public MessageMediaVenue(org.telegram.tl.TLGeoPoint geo, String title, String address, String provider, String venue_id) {
        this.geo = geo;
        this.title = title;
        this.address = address;
        this.provider = provider;
        this.venue_id = venue_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        geo = (org.telegram.tl.TLGeoPoint) buffer.readTLObject(APIContext.getInstance());
        title = buffer.readString();
        address = buffer.readString();
        provider = buffer.readString();
        venue_id = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(geo);
        buff.writeString(title);
        buff.writeString(address);
        buff.writeString(provider);
        buff.writeString(venue_id);
    }


    public int getConstructor() {
        return ID;
    }
}