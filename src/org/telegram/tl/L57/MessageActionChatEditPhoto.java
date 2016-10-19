package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class MessageActionChatEditPhoto extends org.telegram.tl.TLMessageAction {

    public static final int ID = 0x7fcb13a8;

    public org.telegram.tl.photos.TLPhoto photo;

    public MessageActionChatEditPhoto() {
    }

    public MessageActionChatEditPhoto(org.telegram.tl.photos.TLPhoto photo) {
        this.photo = photo;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photo = (org.telegram.tl.photos.TLPhoto) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(photo);
    }


    public int getConstructor() {
        return ID;
    }
}