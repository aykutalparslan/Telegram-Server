package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateEncryption extends org.telegram.tl.TLUpdate {

    public static final int ID = 0xb4a2e88d;

    public org.telegram.tl.TLEncryptedChat chat;
    public int date;

    public UpdateEncryption() {
    }

    public UpdateEncryption(org.telegram.tl.TLEncryptedChat chat, int date) {
        this.chat = chat;
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat = (org.telegram.tl.TLEncryptedChat) buffer.readTLObject(APIContext.getInstance());
        date = buffer.readInt();
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
        buff.writeTLObject(chat);
        buff.writeInt(date);
    }


    public int getConstructor() {
        return ID;
    }
}