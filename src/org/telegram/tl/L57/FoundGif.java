package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class FoundGif extends org.telegram.tl.TLFoundGif {

    public static final int ID = 0x162ecc1f;

    public String url;
    public String thumb_url;
    public String content_url;
    public String content_type;
    public int w;
    public int h;

    public FoundGif() {
    }

    public FoundGif(String url, String thumb_url, String content_url, String content_type, int w, int h) {
        this.url = url;
        this.thumb_url = thumb_url;
        this.content_url = content_url;
        this.content_type = content_type;
        this.w = w;
        this.h = h;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        url = buffer.readString();
        thumb_url = buffer.readString();
        content_url = buffer.readString();
        content_type = buffer.readString();
        w = buffer.readInt();
        h = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeString(url);
        buff.writeString(thumb_url);
        buff.writeString(content_url);
        buff.writeString(content_type);
        buff.writeInt(w);
        buff.writeInt(h);
    }


    public int getConstructor() {
        return ID;
    }
}