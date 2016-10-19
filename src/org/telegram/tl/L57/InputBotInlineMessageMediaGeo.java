package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineMessageMediaGeo extends org.telegram.tl.TLInputBotInlineMessage {

    public static final int ID = 0xf4a59de1;

    public int flags;
    public org.telegram.tl.TLInputGeoPoint geo_point;
    public org.telegram.tl.TLReplyMarkup reply_markup;

    public InputBotInlineMessageMediaGeo() {
    }

    public InputBotInlineMessageMediaGeo(int flags, org.telegram.tl.TLInputGeoPoint geo_point, org.telegram.tl.TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.geo_point = geo_point;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        geo_point = (org.telegram.tl.TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (org.telegram.tl.TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(24);
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
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }


    public int getConstructor() {
        return ID;
    }
}