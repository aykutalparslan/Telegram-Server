package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateNewEncryptedMessage extends TLUpdate {

    public static final int ID = 0x12bcbd9a;

    public TLEncryptedMessage message;
    public int qts;

    public UpdateNewEncryptedMessage() {
    }

    public UpdateNewEncryptedMessage(TLEncryptedMessage message, int qts) {
        this.message = message;
        this.qts = qts;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        message = (TLEncryptedMessage) buffer.readTLObject(APIContext.getInstance());
        qts = buffer.readInt();
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
        buff.writeTLObject(message);
        buff.writeInt(qts);
    }


    public int getConstructor() {
        return ID;
    }
}