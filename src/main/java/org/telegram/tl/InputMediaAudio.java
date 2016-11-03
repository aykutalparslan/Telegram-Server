package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputMediaAudio extends TLInputMedia {

    public static final int ID = 0x89938781;

    public TLInputAudio audio_id;

    public InputMediaAudio() {
    }

    public InputMediaAudio(TLInputAudio audio_id) {
        this.audio_id = audio_id;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        audio_id = (TLInputAudio) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(audio_id);
    }

    public int getConstructor() {
        return ID;
    }
}