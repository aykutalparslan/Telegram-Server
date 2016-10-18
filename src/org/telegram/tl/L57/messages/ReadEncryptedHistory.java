package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ReadEncryptedHistory extends TLObject {

    public static final int ID = 0x7f4b690a;

    public TLInputEncryptedChat peer;
    public int max_date;

    public ReadEncryptedHistory() {
    }

    public ReadEncryptedHistory(TLInputEncryptedChat peer, int max_date) {
        this.peer = peer;
        this.max_date = max_date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        peer = (TLInputEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        max_date = buffer.readInt();
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
        buff.writeInt(max_date);
    }


    public int getConstructor() {
        return ID;
    }
}