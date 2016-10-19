package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ReplyInlineMarkup extends org.telegram.tl.TLReplyMarkup {

    public static final int ID = 0x48a30254;

    public TLVector<org.telegram.tl.TLKeyboardButtonRow> rows;

    public ReplyInlineMarkup() {
        this.rows = new TLVector<>();
    }

    public ReplyInlineMarkup(TLVector<org.telegram.tl.TLKeyboardButtonRow> rows) {
        this.rows = rows;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        rows = (TLVector<org.telegram.tl.TLKeyboardButtonRow>) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(rows);
    }


    public int getConstructor() {
        return ID;
    }
}