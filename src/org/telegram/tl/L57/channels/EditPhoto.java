package org.telegram.tl.L57.channels;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class EditPhoto extends TLObject {

    public static final int ID = 0xf12e57c9;

    public TLInputChannel channel;
    public TLInputChatPhoto photo;

    public EditPhoto() {
    }

    public EditPhoto(TLInputChannel channel, TLInputChatPhoto photo) {
        this.channel = channel;
        this.photo = photo;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        channel = (TLInputChannel) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(channel);
        buff.writeTLObject(photo);
    }


    public int getConstructor() {
        return ID;
    }
}