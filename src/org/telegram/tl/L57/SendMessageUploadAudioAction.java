package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendMessageUploadAudioAction extends TLSendMessageAction {

    public static final int ID = 0xf351d7ab;

    public int progress;

    public SendMessageUploadAudioAction() {
    }

    public SendMessageUploadAudioAction(int progress) {
        this.progress = progress;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        progress = buffer.readInt();
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
        buff.writeInt(progress);
    }


    public int getConstructor() {
        return ID;
    }
}