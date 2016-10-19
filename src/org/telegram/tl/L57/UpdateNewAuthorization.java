package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateNewAuthorization extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x8f06529a;

    public long auth_key_id;
    public int date;
    public String device;
    public String location;

    public UpdateNewAuthorization() {
    }

    public UpdateNewAuthorization(long auth_key_id, int date, String device, String location) {
        this.auth_key_id = auth_key_id;
        this.date = date;
        this.device = device;
        this.location = location;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        auth_key_id = buffer.readLong();
        date = buffer.readInt();
        device = buffer.readString();
        location = buffer.readString();
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
        buff.writeLong(auth_key_id);
        buff.writeInt(date);
        buff.writeString(device);
        buff.writeString(location);
    }


    public int getConstructor() {
        return ID;
    }
}