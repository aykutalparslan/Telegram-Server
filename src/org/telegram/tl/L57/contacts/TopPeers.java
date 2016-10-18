package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class TopPeers extends TLTopPeers {

    public static final int ID = 0x70b772a8;

    public TLVector<TLTopPeerCategoryPeers> categories;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;

    public TopPeers() {
        this.categories = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public TopPeers(TLVector<TLTopPeerCategoryPeers> categories, TLVector<TLChat> chats, TLVector<TLUser> users) {
        this.categories = categories;
        this.chats = chats;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        categories = (TLVector<TLTopPeerCategoryPeers>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(categories);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}