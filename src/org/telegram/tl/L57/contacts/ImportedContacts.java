package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ImportedContacts extends org.telegram.tl.contacts.TLImportedContacts {

    public static final int ID = 0xad524315;

    public TLVector<org.telegram.tl.TLImportedContact> imported;
    public TLVector<Long> retry_contacts;
    public TLVector<org.telegram.tl.TLUser> users;

    public ImportedContacts() {
        this.imported = new TLVector<>();
        this.retry_contacts = new TLVector<>();
        this.users = new TLVector<>();
    }

    public ImportedContacts(TLVector<org.telegram.tl.TLImportedContact> imported, TLVector<Long> retry_contacts, TLVector<org.telegram.tl.TLUser> users) {
        this.imported = imported;
        this.retry_contacts = retry_contacts;
        this.users = users;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        imported = (TLVector<org.telegram.tl.TLImportedContact>) buffer.readTLObject(APIContext.getInstance());
        retry_contacts = (TLVector<Long>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(imported);
        buff.writeTLObject(retry_contacts);
        buff.writeTLObject(users);
    }


    public int getConstructor() {
        return ID;
    }
}