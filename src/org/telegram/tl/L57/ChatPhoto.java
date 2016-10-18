package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class ChatPhoto extends TLChatPhoto {

    public static final int ID = 0x6153276a;

    public TLFileLocation photo_small;
    public TLFileLocation photo_big;

    public ChatPhoto() {
    }

    public ChatPhoto(TLFileLocation photo_small, TLFileLocation photo_big) {
        this.photo_small = photo_small;
        this.photo_big = photo_big;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        photo_small = (TLFileLocation) buffer.readTLObject(APIContext.getInstance());
        photo_big = (TLFileLocation) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(photo_small);
        buff.writeTLObject(photo_big);
    }


    public int getConstructor() {
        return ID;
    }
}