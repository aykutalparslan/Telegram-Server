package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaUploadedDocument extends TLInputMedia {

    public static final int ID = 0xffe76b78;

    public TLInputFile file;
    public String mime_type;
    public TLVector<TLDocumentAttribute> attributes;

    public InputMediaUploadedDocument() {
        this.attributes = new TLVector<>();
    }

    public InputMediaUploadedDocument(TLInputFile file, String mime_type, TLVector<TLDocumentAttribute> attributes){
        this.file = file;
        this.mime_type = mime_type;
        this.attributes = attributes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeString(mime_type);
        buff.writeTLObject(attributes);
    }

    public int getConstructor() {
        return ID;
    }
}