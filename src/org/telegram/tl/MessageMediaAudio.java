package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaAudio extends TLMessageMedia {

    public static final int ID = 0xc6b68300;

    public TLAudio audio;

    public MessageMediaAudio() {
    }

    public MessageMediaAudio(TLAudio audio){
        this.audio = audio;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        audio = (TLAudio) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(audio);
    }

    public int getConstructor() {
        return ID;
    }
}