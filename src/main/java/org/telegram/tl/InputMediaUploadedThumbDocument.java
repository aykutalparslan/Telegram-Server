package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaUploadedThumbDocument extends TLInputMedia {

    public static final int ID = 0x41481486;

    public TLInputFile file;
    public TLInputFile thumb;
    public String mime_type;
    public TLVector<TLDocumentAttribute> attributes;

    public InputMediaUploadedThumbDocument() {
        this.attributes = new TLVector<>();
    }

    public InputMediaUploadedThumbDocument(TLInputFile file, TLInputFile thumb, String mime_type, TLVector<TLDocumentAttribute> attributes){
        this.file = file;
        this.thumb = thumb;
        this.mime_type = mime_type;
        this.attributes = attributes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        thumb = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        mime_type = buffer.readString();
        attributes = (TLVector<TLDocumentAttribute>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(file);
        buff.writeTLObject(thumb);
        buff.writeString(mime_type);
        buff.writeTLObject(attributes);
    }

    public int getConstructor() {
        return ID;
    }
}