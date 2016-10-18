package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SetEncryptedTyping extends TLObject {

    public static final int ID = 0x791451ed;

    public TLInputEncryptedChat peer;
    public boolean typing;

    public SetEncryptedTyping() {
    }

    public SetEncryptedTyping(TLInputEncryptedChat peer, boolean typing) {
        this.peer = peer;
        this.typing = typing;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        typing = buffer.readBool();
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
        buff.writeTLObject(peer);
        buff.writeBool(typing);
    }


    public int getConstructor() {
        return ID;
    }
}