package org.telegram.tl.L57.help;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SaveAppLog extends TLObject {

    public static final int ID = 0x6f02f748;

    public TLVector<TLInputAppEvent> events;

    public SaveAppLog() {
        this.events = new TLVector<>();
    }

    public SaveAppLog(TLVector<TLInputAppEvent> events) {
        this.events = events;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        events = (TLVector<TLInputAppEvent>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(events);
    }


    public int getConstructor() {
        return ID;
    }
}