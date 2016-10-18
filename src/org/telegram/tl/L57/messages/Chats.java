package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Chats extends TLChats {

    public static final int ID = 0x64ff9fd5;

    public TLVector<TLChat> chats;

    public Chats() {
        this.chats = new TLVector<>();
    }

    public Chats(TLVector<TLChat> chats) {
        this.chats = chats;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(chats);
    }


    public int getConstructor() {
        return ID;
    }
}