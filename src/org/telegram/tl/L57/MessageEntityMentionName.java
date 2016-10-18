package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageEntityMentionName extends TLMessageEntity {

    public static final int ID = 0x352dca58;

    public int offset;
    public int length;
    public int user_id;

    public MessageEntityMentionName() {
    }

    public MessageEntityMentionName(int offset, int length, int user_id) {
        this.offset = offset;
        this.length = length;
        this.user_id = user_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset = buffer.readInt();
        length = buffer.readInt();
        user_id = buffer.readInt();
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
        buff.writeInt(user_id);
    }


    public int getConstructor() {
        return ID;
    }
}