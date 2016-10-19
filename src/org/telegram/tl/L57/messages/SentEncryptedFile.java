package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SentEncryptedFile extends org.telegram.tl.messages.TLSentEncryptedMessage {

    public static final int ID = 0x9493ff32;

    public int date;
    public org.telegram.tl.TLEncryptedFile file;

    public SentEncryptedFile() {
    }

    public SentEncryptedFile(int date, org.telegram.tl.TLEncryptedFile file) {
        this.date = date;
        this.file = file;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        date = buffer.readInt();
        file = (org.telegram.tl.TLEncryptedFile) buffer.readTLObject(APIContext.getInstance());
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(date);
        buff.writeTLObject(file);
    }


    public int getConstructor() {
        return ID;
    }
}