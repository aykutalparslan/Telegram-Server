package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputMessageEntityMentionName extends TLMessageEntity {

    public static final int ID = 0x208e68c9;

    public int offset;
    public int length;
    public TLInputUser user_id;

    public InputMessageEntityMentionName() {
    }

    public InputMessageEntityMentionName(int offset, int length, TLInputUser user_id) {
        this.offset = offset;
        this.length = length;
        this.user_id = user_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offset = buffer.readInt();
        length = buffer.readInt();
        user_id = (TLInputUser) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(user_id);
    }


    public int getConstructor() {
        return ID;
    }
}