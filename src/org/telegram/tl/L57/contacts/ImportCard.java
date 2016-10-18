package org.telegram.tl.L57.contacts;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ImportCard extends TLObject {

    public static final int ID = 0x4fe196fe;

    public TLVector<Integer> export_card;

    public ImportCard() {
        this.export_card = new TLVector<>();
    }

    public ImportCard(TLVector<Integer> export_card) {
        this.export_card = export_card;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        export_card = (TLVector<Integer>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(export_card);
    }


    public int getConstructor() {
        return ID;
    }
}