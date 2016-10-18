package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineMessageMediaVenue extends TLInputBotInlineMessage {

    public static final int ID = 0xaaafadc8;

    public int flags;
    public TLInputGeoPoint geo_point;
    public String title;
    public String address;
    public String provider;
    public String venue_id;
    public TLReplyMarkup reply_markup;

    public InputBotInlineMessageMediaVenue() {
    }

    public InputBotInlineMessageMediaVenue(int flags, TLInputGeoPoint geo_point, String title, String address, String provider, String venue_id, TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.geo_point = geo_point;
        this.title = title;
        this.address = address;
        this.provider = provider;
        this.venue_id = venue_id;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        geo_point = (TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
        title = buffer.readString();
        address = buffer.readString();
        provider = buffer.readString();
        venue_id = buffer.readString();
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (reply_markup != null) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(geo_point);
        buff.writeString(title);
        buff.writeString(address);
        buff.writeString(provider);
        buff.writeString(venue_id);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }


    public int getConstructor() {
        return ID;
    }
}