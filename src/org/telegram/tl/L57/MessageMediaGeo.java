package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageMediaGeo extends TLMessageMedia {

    public static final int ID = 0x56e0d474;

    public TLGeoPoint geo;

    public MessageMediaGeo() {
    }

    public MessageMediaGeo(TLGeoPoint geo) {
        this.geo = geo;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        geo = (TLGeoPoint) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(geo);
    }


    public int getConstructor() {
        return ID;
    }
}