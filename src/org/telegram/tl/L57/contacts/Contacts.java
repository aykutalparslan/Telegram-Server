package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class Contacts extends org.telegram.tl.contacts.TLContacts {

    public static final int ID = 0x6f8b8cb2;

    public TLVector<org.telegram.tl.TLContact> contacts;
    public TLVector<org.telegram.tl.TLUser> users;

    public Contacts() {
        this.contacts = new TLVector<>();
        this.users = new TLVector<>();
    }

    public Contacts(TLVector<org.telegram.tl.TLContact> contacts, TLVector<org.telegram.tl.TLUser> users) {
        this.contacts = contacts;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        contacts = (TLVector<org.telegram.tl.TLContact>) buffer.readTLObject(APIContext.getInstance());
        users = (TLVector<org.telegram.tl.TLUser>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(20);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(contacts);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}