package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class BotInlineMediaResult extends TLBotInlineResult {

    public static final int ID = 0x17db940b;

    public int flags;
    public String id;
    public String type;
    public TLPhoto photo;
    public TLDocument document;
    public String title;
    public String description;
    public TLBotInlineMessage send_message;

    public BotInlineMediaResult() {
    }

    public BotInlineMediaResult(int flags, String id, String type, TLPhoto photo, TLDocument document, String title, String description, TLBotInlineMessage send_message) {
        this.flags = flags;
        this.id = id;
        this.type = type;
        this.photo = photo;
        this.document = document;
        this.title = title;
        this.description = description;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readString();
        type = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            photo = (TLPhoto) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 1)) != 0) {
            document = (TLDocument) buffer.readTLObject(APIContext.getInstance());
        }
        if ((flags & (1 << 2)) != 0) {
            title = buffer.readString();
        }
        if ((flags & (1 << 3)) != 0) {
            description = buffer.readString();
        }
        send_message = (TLBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (photo != null) {
            flags |= (1 << 0);
        }
        if (document != null) {
            flags |= (1 << 1);
        }
        if (title != null && !title.isEmpty()) {
            flags |= (1 << 2);
        }
        if (description != null && !description.isEmpty()) {
            flags |= (1 << 3);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(id);
        buff.writeString(type);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(photo);
        }
        if ((flags & (1 << 1)) != 0) {
            buff.writeTLObject(document);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(title);
        }
        if ((flags & (1 << 3)) != 0) {
            buff.writeString(description);
        }
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}