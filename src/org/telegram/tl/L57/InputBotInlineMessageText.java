package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineMessageText extends TLInputBotInlineMessage {

    public static final int ID = 0x3dcd7a87;

    public int flags;
    public String message;
    public TLVector<TLMessageEntity> entities;
    public TLReplyMarkup reply_markup;

    public InputBotInlineMessageText() {
        this.entities = new TLVector<>();
    }

    public InputBotInlineMessageText(int flags, String message, TLVector<TLMessageEntity> entities, TLReplyMarkup reply_markup) {
        this.flags = flags;
        this.message = message;
        this.entities = entities;
        this.reply_markup = reply_markup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        message = buffer.readString();
        if ((flags & (1 << 1)) != 0) {
            entities = (TLVector<TLMessageEntity>) buffer.readTLVector(TLMessageEntity.class);
        }
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
        if (entities != null) {
            flags |= (1 << 1);
        }
        if (reply_markup != null) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(message);
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(entities);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
    }

    public boolean is_no_webpage() {
        return (flags & (1 << 0)) != 0;
    }

    public void set_no_webpage(boolean v) {
        if (v) {
            flags |= (1 << 0);
        } else {
            flags &= ~(1 << 0);
        }
    }

    public int getConstructor() {
        return ID;
    }
}