package org.telegram.tl.L57.updates;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;
import org.telegram.tl.updates.*;

public class Difference extends TLDifference {

    public static final int ID = 0xf49ca0;

    public TLVector<TLMessage> new_messages;
    public TLVector<TLEncryptedMessage> new_encrypted_messages;
    public TLVector<TLUpdate> other_updates;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;
    public TLState state;

    public Difference() {
        this.new_messages = new TLVector<>();
        this.new_encrypted_messages = new TLVector<>();
        this.other_updates = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public Difference(TLVector<TLMessage> new_messages, TLVector<TLEncryptedMessage> new_encrypted_messages, TLVector<TLUpdate> other_updates, TLVector<TLChat> chats, TLVector<TLUser> users, TLState state) {
        this.new_messages = new_messages;
        this.new_encrypted_messages = new_encrypted_messages;
        this.other_updates = other_updates;
        this.chats = chats;
        this.users = users;
        this.state = state;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        new_messages = (TLVector<TLMessage>) buffer.readTLObject(APIContext.getInstance());
        new_encrypted_messages = (TLVector<TLEncryptedMessage>) buffer.readTLObject(APIContext.getInstance());
        other_updates = (TLVector<TLUpdate>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(new_messages);
        buff.writeTLObject(new_encrypted_messages);
        buff.writeTLObject(other_updates);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
        buff.writeTLObject(state);
    }


    public int getConstructor() {
        return ID;
    }
}