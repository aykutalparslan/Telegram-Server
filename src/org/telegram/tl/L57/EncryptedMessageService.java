package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EncryptedMessageService extends org.telegram.tl.TLEncryptedMessage {

    public static final int ID = 0x23734b06;

    public long random_id;
    public int chat_id;
    public int date;
    public byte[] bytes;

    public EncryptedMessageService() {
    }

    public EncryptedMessageService(long random_id, int chat_id, int date, byte[] bytes) {
        this.random_id = random_id;
        this.chat_id = chat_id;
        this.date = date;
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        random_id = buffer.readLong();
        chat_id = buffer.readInt();
        date = buffer.readInt();
        bytes = buffer.readBytes();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(44);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeLong(random_id);
        buff.writeInt(chat_id);
        buff.writeInt(date);
        buff.writeBytes(bytes);
    }


    public int getConstructor() {
        return ID;
    }
}