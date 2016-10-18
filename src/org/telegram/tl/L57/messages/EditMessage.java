package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EditMessage extends TLObject {

    public static final int ID = 0xce91e4ca;

    public int flags;
    public TLInputPeer peer;
    public int id;
    public String message;
    public TLReplyMarkup reply_markup;
    public TLVector<TLMessageEntity> entities;

    public EditMessage() {
        this.entities = new TLVector<>();
    }

    public EditMessage(int flags, TLInputPeer peer, int id, String message, TLReplyMarkup reply_markup, TLVector<TLMessageEntity> entities) {
        this.flags = flags;
        this.peer = peer;
        this.id = id;
        this.message = message;
        this.reply_markup = reply_markup;
        this.entities = entities;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        peer = (TLInputPeer) buffer.readTLObject(APIContext.getInstance());
        id = buffer.readInt();
        if ((flags & (1 << 11)) != 0) {
            message = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            reply_markup = (TLReplyMarkup) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 3)) != 0) {
            entities = (TLVector<TLMessageEntity>) buffer.readTLVector(TLMessageEntity.class);
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
        if (message != null && !message.isEmpty()) {
            flags |= (1 << 11);
        }
        if (reply_markup != null) {
            flags |= (1 << 2);
        }
        if (entities != null) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(peer);
        buff.writeInt(id);
        if ((flags & (1 << 11)) != 0) {
            buff.writeString(message);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeTLObject(reply_markup);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeTLObject(entities);
        }
    }

    public boolean is_no_webpage() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_no_webpage(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public int getConstructor() {
        return ID;
    }
}