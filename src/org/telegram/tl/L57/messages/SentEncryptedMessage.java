package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SentEncryptedMessage extends org.telegram.tl.messages.TLSentEncryptedMessage {

    public static final int ID = 0x560f8935;

    public int date;

    public SentEncryptedMessage() {
    }

    public SentEncryptedMessage(int date) {
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        date = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(8);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(date);
    }


    public int getConstructor() {
        return ID;
    }
}