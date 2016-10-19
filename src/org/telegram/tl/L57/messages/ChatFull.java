package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatFull extends org.telegram.tl.messages.TLChatFull {

    public static final int ID = 0xe5d7d19c;

    public org.telegram.tl.messages.TLChatFull full_chat;
    public TLVector<org.telegram.tl.TLChat> chats;
    public TLVector<org.telegram.tl.TLUser> users;

    public ChatFull() {
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public ChatFull(org.telegram.tl.messages.TLChatFull full_chat, TLVector<org.telegram.tl.TLChat> chats, TLVector<org.telegram.tl.TLUser> users) {
        this.full_chat = full_chat;
        this.chats = chats;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        full_chat = (org.telegram.tl.messages.TLChatFull) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<org.telegram.tl.TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(28);
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