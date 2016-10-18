package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class CreateChat extends TLObject {

    public static final int ID = 0x9cb126e;

    public TLVector<TLInputUser> users;
    public String title;

    public CreateChat() {
        this.users = new TLVector<>();
    }

    public CreateChat(TLVector<TLInputUser> users, String title) {
        this.users = users;
        this.title = title;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        users = (TLVector<TLInputUser>) buffer.readTLObject(APIContext.getInstance());
        title = buffer.readString();
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
        buff.writeTLObject(users);
        buff.writeString(title);
    }


    public int getConstructor() {
        return ID;
    }
}