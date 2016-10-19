package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputMediaDocumentExternal extends org.telegram.tl.TLInputMedia {

    public static final int ID = 0xe5e9607c;

    public String url;
    public String caption;

    public InputMediaDocumentExternal() {
    }

    public InputMediaDocumentExternal(String url, String caption) {
        this.url = url;
        this.caption = caption;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        url = buffer.readString();
        caption = buffer.readString();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeString(url);
        buff.writeString(caption);
    }


    public int getConstructor() {
        return ID;
    }
}