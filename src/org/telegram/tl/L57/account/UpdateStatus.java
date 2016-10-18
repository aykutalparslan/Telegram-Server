package org.telegram.tl.L57.account;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateStatus extends TLObject {

    public static final int ID = 0x6628562c;

    public boolean offline;

    public UpdateStatus() {
    }

    public UpdateStatus(boolean offline) {
        this.offline = offline;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        offline = buffer.readBool();
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
        buff.writeBool(offline);
    }


    public int getConstructor() {
        return ID;
    }
}