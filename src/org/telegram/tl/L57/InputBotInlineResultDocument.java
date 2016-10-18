package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineResultDocument extends TLInputBotInlineResult {

    public static final int ID = 0xfff8fdc4;

    public int flags;
    public String id;
    public String type;
    public String title;
    public String description;
    public TLInputDocument document;
    public TLInputBotInlineMessage send_message;

    public InputBotInlineResultDocument() {
    }

    public InputBotInlineResultDocument(int flags, String id, String type, String title, String description, TLInputDocument document, TLInputBotInlineMessage send_message) {
        this.flags = flags;
        this.id = id;
        this.type = type;
        this.title = title;
        this.description = description;
        this.document = document;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        id = buffer.readString();
        type = buffer.readString();
        if ((flags & (1 << 1)) != 0) {
            title = buffer.readString();
        }
        if ((flags & (1 << 2)) != 0) {
            description = buffer.readString();
        }
        document = (TLInputDocument) buffer.readTLObject(APIContext.getInstance());
        send_message = (TLInputBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(32);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (title != null && !title.isEmpty()) {
            flags |= (1 << 1);
        }
        if (description != null && !description.isEmpty()) {
            flags |= (1 << 2);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(id);
        buff.writeString(type);
        if ((flags & (1 << 1)) != 0) {
            buff.writeString(title);
        }
        if ((flags & (1 << 2)) != 0) {
            buff.writeString(description);
        }
        buff.writeTLObject(document);
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}