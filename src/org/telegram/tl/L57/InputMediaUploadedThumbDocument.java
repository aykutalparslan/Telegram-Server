package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputMediaUploadedThumbDocument extends TLInputMedia {

    public static final int ID = 0x50d88cae;

    public int flags;
    public TLInputFile file;
    public TLInputFile thumb;
    public String mime_type;
    public TLVector<TLDocumentAttribute> attributes;
    public String caption;
    public TLVector<TLInputDocument> stickers;

    public InputMediaUploadedThumbDocument() {
        this.attributes = new TLVector<>();
        this.stickers = new TLVector<>();
    }

    public InputMediaUploadedThumbDocument(int flags, TLInputFile file, TLInputFile thumb, String mime_type, TLVector<TLDocumentAttribute> attributes, String caption, TLVector<TLInputDocument> stickers) {
        this.flags = flags;
        this.file = file;
        this.thumb = thumb;
        this.mime_type = mime_type;
        this.attributes = attributes;
        this.caption = caption;
        this.stickers = stickers;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        thumb = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        mime_type = buffer.readString();
        attributes = (TLVector<TLDocumentAttribute>) buffer.readTLObject(APIContext.getInstance());
        caption = buffer.readString();
        if ((flags & (1 << 0)) != 0) {
            stickers = (TLVector<TLInputDocument>) buffer.readTLVector(TLInputDocument.class);
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
        if (stickers != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeTLObject(file);
        buff.writeTLObject(thumb);
        buff.writeString(mime_type);
        buff.writeTLObject(attributes);
        buff.writeString(caption);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(stickers);
        }
    }


    public int getConstructor() {
        return ID;
    }
}