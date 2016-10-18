package org.telegram.tl.L57.messages;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EditChatPhoto extends TLObject {

    public static final int ID = 0xca4c79d8;

    public int chat_id;
    public TLInputChatPhoto photo;

    public EditChatPhoto() {
    }

    public EditChatPhoto(int chat_id, TLInputChatPhoto photo) {
        this.chat_id = chat_id;
        this.photo = photo;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        chat_id = buffer.readInt();
        photo = (TLInputChatPhoto) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeInt(chat_id);
        buff.writeTLObject(photo);
    }


    public int getConstructor() {
        return ID;
    }
}