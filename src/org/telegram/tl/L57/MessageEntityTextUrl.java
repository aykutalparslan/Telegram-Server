package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageEntityTextUrl extends org.telegram.tl.TLMessageEntity {

    public static final int ID = 0x76a6d327;

    public int offset;
    public int length;
    public String url;

    public MessageEntityTextUrl() {
    }

    public MessageEntityTextUrl(int offset, int length, String url) {
        this.offset = offset;
        this.length = length;
        this.url = url;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset = buffer.readInt();
        length = buffer.readInt();
        url = buffer.readString();
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
        buff.writeInt(offset);
        buff.writeInt(length);
        buff.writeString(url);
    }


    public int getConstructor() {
        return ID;
    }
}