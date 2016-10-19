package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateDcOptions extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x8e5e9873;

    public TLVector<org.telegram.tl.TLDcOption> dc_options;

    public UpdateDcOptions() {
        this.dc_options = new TLVector<>();
    }

    public UpdateDcOptions(TLVector<org.telegram.tl.TLDcOption> dc_options) {
        this.dc_options = dc_options;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        dc_options = (TLVector<org.telegram.tl.TLDcOption>) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(12);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeTLObject(dc_options);
    }


    public int getConstructor() {
        return ID;
    }
}