package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatFull extends TLChatFull {

    public static final int ID = 0xe5d7d19c;

    public TLChatFull full_chat;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;

    public ChatFull() {
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public ChatFull(TLChatFull full_chat, TLVector<TLChat> chats, TLVector<TLUser> users) {
        this.full_chat = full_chat;
        this.chats = chats;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        full_chat = (TLChatFull) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(full_chat);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}