package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineMessageMediaAuto extends org.telegram.tl.TLInputBotInlineMessage {

    public static final int ID = 0x292fed13;

    public int flags;
    public String caption;
    public org.telegram.tl.TLReplyMarkup reply_markup;

    public InputBotInlineMessageMediaAuto() {
    }

    public InputBotInlineMessageMediaAuto(int flags, String caption, org.telegram.tl.TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.caption = caption;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        caption = buffer.readString();
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
        buff.writeString(caption);
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }


    public int getConstructor() {
        return ID;
    }
}