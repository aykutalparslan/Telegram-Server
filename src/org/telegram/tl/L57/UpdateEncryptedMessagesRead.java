package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateEncryptedMessagesRead extends org.telegram.tl.TLUpdate {

    public static final int ID = 0x38fe25b7;

    public int chat_id;
    public int max_date;
    public int date;

    public UpdateEncryptedMessagesRead() {
    }

    public UpdateEncryptedMessagesRead(int chat_id, int max_date, int date) {
        this.chat_id = chat_id;
        this.max_date = max_date;
        this.date = date;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        max_date = buffer.readInt();
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
        buff.writeInt(chat_id);
        buff.writeInt(max_date);
        buff.writeInt(date);
    }


    public int getConstructor() {
        return ID;
    }
}