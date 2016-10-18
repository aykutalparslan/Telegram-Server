package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatInviteAlready extends TLChatInvite {

    public static final int ID = 0x5a686d7c;

    public TLChat chat;

    public ChatInviteAlready() {
    }

    public ChatInviteAlready(TLChat chat) {
        this.chat = chat;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat = (TLChat) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(chat);
    }


    public int getConstructor() {
        return ID;
    }
}