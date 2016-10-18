package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageEntityPre extends TLMessageEntity {

    public static final int ID = 0x73924be0;

    public int offset;
    public int length;
    public String language;

    public MessageEntityPre() {
    }

    public MessageEntityPre(int offset, int length, String language) {
        this.offset = offset;
        this.length = length;
        this.language = language;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset = buffer.readInt();
        length = buffer.readInt();
        language = buffer.readString();
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
        buff.writeInt(offset);
        buff.writeInt(length);
        buff.writeString(language);
    }


    public int getConstructor() {
        return ID;
    }
}