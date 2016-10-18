package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ImportContacts extends TLObject {

    public static final int ID = 0xda30b32d;

    public TLVector<TLInputContact> contacts;
    public boolean replace;

    public ImportContacts() {
        this.contacts = new TLVector<>();
    }

    public ImportContacts(TLVector<TLInputContact> contacts, boolean replace) {
        this.contacts = contacts;
        this.replace = replace;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        contacts = (TLVector<TLInputContact>) buffer.readTLObject(APIContext.getInstance());
        replace = buffer.readBool();
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
        buff.writeTLObject(contacts);
        buff.writeBool(replace);
    }


    public int getConstructor() {
        return ID;
    }
}