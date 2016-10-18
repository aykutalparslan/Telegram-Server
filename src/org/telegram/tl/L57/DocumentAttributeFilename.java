package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DocumentAttributeFilename extends TLDocumentAttribute {

    public static final int ID = 0x15590068;

    public String file_name;

    public DocumentAttributeFilename() {
    }

    public DocumentAttributeFilename(String file_name) {
        this.file_name = file_name;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file_name = buffer.readString();
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
        buff.writeString(file_name);
    }


    public int getConstructor() {
        return ID;
    }
}