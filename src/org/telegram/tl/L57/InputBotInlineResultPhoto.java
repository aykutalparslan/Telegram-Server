package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class InputBotInlineResultPhoto extends TLInputBotInlineResult {

    public static final int ID = 0xa8d864a7;

    public String id;
    public String type;
    public TLInputPhoto photo;
    public TLInputBotInlineMessage send_message;

    public InputBotInlineResultPhoto() {
    }

    public InputBotInlineResultPhoto(String id, String type, TLInputPhoto photo, TLInputBotInlineMessage send_message) {
        this.id = id;
        this.type = type;
        this.photo = photo;
        this.send_message = send_message;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readString();
        type = buffer.readString();
        photo = (TLInputPhoto) buffer.readTLObject(APIContext.getInstance());
        send_message = (TLInputBotInlineMessage) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeString(id);
        buff.writeString(type);
        buff.writeTLObject(photo);
        buff.writeTLObject(send_message);
    }


    public int getConstructor() {
        return ID;
    }
}