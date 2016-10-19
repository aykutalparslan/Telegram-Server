package org.telegram.tl.L57.help;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InviteText extends org.telegram.tl.help.TLInviteText {

    public static final int ID = 0x18cb9f78;

    public String message;

    public InviteText() {
    }

    public InviteText(String message) {
        this.message = message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        message = buffer.readString();
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
        buff.writeString(message);
    }


    public int getConstructor() {
        return ID;
    }
}