package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Updates extends TLUpdates {

    public static final int ID = 0x74ae4240;

    public TLVector<TLUpdate> updates;
    public TLVector<TLUser> users;
    public TLVector<TLChat> chats;
    public int date;
    public int seq;

    public Updates() {
        this.updates = new TLVector<>();
        this.users = new TLVector<>();
        this.chats = new TLVector<>();
    }

    public Updates(TLVector<TLUpdate> updates, TLVector<TLUser> users, TLVector<TLChat> chats, int date, int seq) {
        this.updates = updates;
        this.users = users;
        this.chats = chats;
        this.date = date;
        this.seq = seq;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        updates = (TLVector<TLUpdate>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        date = buffer.readInt();
        seq = buffer.readInt();
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
        buff.writeTLObject(updates);
        buff.writeTLObject(users);
        buff.writeTLObject(chats);
        buff.writeInt(date);
        buff.writeInt(seq);
    }


    public int getConstructor() {
        return ID;
    }
}