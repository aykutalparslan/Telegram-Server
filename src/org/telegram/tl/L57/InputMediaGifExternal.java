package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputMediaGifExternal extends org.telegram.tl.TLInputMedia {

    public static final int ID = 0x4843b0fd;

    public String url;
    public String q;

    public InputMediaGifExternal() {
    }

    public InputMediaGifExternal(String url, String q) {
        this.url = url;
        this.q = q;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        url = buffer.readString();
        q = buffer.readString();
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
        buff.writeString(q);
    }


    public int getConstructor() {
        return ID;
    }
}