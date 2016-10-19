package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageActionChatJoinedByLink extends org.telegram.tl.TLMessageAction {

    public static final int ID = 0xf89cf5e8;

    public int inviter_id;

    public MessageActionChatJoinedByLink() {
    }

    public MessageActionChatJoinedByLink(int inviter_id) {
        this.inviter_id = inviter_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        inviter_id = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(8);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(inviter_id);
    }


    public int getConstructor() {
        return ID;
    }
}