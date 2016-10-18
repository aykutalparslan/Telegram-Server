package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DialogsSlice extends TLDialogs {

    public static final int ID = 0x71e094f3;

    public int count;
    public TLVector<TLDialog> dialogs;
    public TLVector<TLMessage> messages;
    public TLVector<TLChat> chats;
    public TLVector<TLUser> users;

    public DialogsSlice() {
        this.dialogs = new TLVector<>();
        this.messages = new TLVector<>();
        this.chats = new TLVector<>();
        this.users = new TLVector<>();
    }

    public DialogsSlice(int count, TLVector<TLDialog> dialogs, TLVector<TLMessage> messages, TLVector<TLChat> chats, TLVector<TLUser> users) {
        this.count = count;
        this.dialogs = dialogs;
        this.messages = messages;
        this.chats = chats;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        count = buffer.readInt();
        dialogs = (TLVector<TLDialog>) buffer.readTLObject(APIContext.getInstance());
        messages = (TLVector<TLMessage>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(count);
        buff.writeTLObject(dialogs);
        buff.writeTLObject(messages);
        buff.writeTLObject(chats);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}