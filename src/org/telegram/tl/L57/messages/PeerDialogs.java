package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.updates.*;

public class PeerDialogs extends TLPeerDialogs {

    public static final int ID = 0x3371c354;

    public TLVector<TLDialog> dialogs;
    public TLVector<TLMessage> messages;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;
    public TLState state;

    public PeerDialogs() {
        this.dialogs = new TLVector<>();
        this.messages = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public PeerDialogs(TLVector<TLDialog> dialogs, TLVector<TLMessage> messages, TLVector<TLChat> chats, TLVector<TLUser> users, TLState state) {
        this.dialogs = dialogs;
        this.messages = messages;
        this.chats = chats;
        this.users = users;
        this.state = state;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        dialogs = (TLVector<TLDialog>) buffer.readTLObject(APIContext.getInstance());
        messages = (TLVector<TLMessage>) buffer.readTLObject(APIContext.getInstance());
        chats = (TLVector<TLChat>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<TLUser>) buffer.readTLObject(APIContext.getInstance());
        state = (TLState) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(dialogs);
        buff.writeTLObject(messages);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
        buff.writeTLObject(state);
    }


    public int getConstructor() {
        return ID;
    }
}