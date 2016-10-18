package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ImportedContact extends TLImportedContact {

    public static final int ID = 0xd0028438;

    public int user_id;
    public long client_id;

    public ImportedContact() {
    }

    public ImportedContact(int user_id, long client_id) {
        this.user_id = user_id;
        this.client_id = client_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        user_id = buffer.readInt();
        client_id = buffer.readLong();
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
        buff.writeInt(user_id);
        buff.writeLong(client_id);
    }


    public int getConstructor() {
        return ID;
    }
}