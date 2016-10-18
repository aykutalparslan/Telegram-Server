package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class UpdateServiceNotification extends TLUpdate {

    public static final int ID = 0x382dd3e4;

    public String type;
    public String message;
    public TLMessageMedia media;
    public boolean popup;

    public UpdateServiceNotification() {
    }

    public UpdateServiceNotification(String type, String message, TLMessageMedia media, boolean popup) {
        this.type = type;
        this.message = message;
        this.media = media;
        this.popup = popup;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        type = buffer.readString();
        message = buffer.readString();
        media = (TLMessageMedia) buffer.readTLObject(APIContext.getInstance());
        popup = buffer.readBool();
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
        buff.writeString(type);
        buff.writeString(message);
        buff.writeTLObject(media);
        buff.writeBool(popup);
    }


    public int getConstructor() {
        return ID;
    }
}