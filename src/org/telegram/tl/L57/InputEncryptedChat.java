package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputEncryptedChat extends org.telegram.tl.TLInputEncryptedChat {

    public static final int ID = 0xf141b5e1;

    public int chat_id;
    public long access_hash;

    public InputEncryptedChat() {
    }

    public InputEncryptedChat(int chat_id, long access_hash) {
        this.chat_id = chat_id;
        this.access_hash = access_hash;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        access_hash = buffer.readLong();
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
        buff.writeLong(access_hash);
    }


    public int getConstructor() {
        return ID;
    }
}