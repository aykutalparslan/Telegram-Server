package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendMessageUploadVideoAction extends TLSendMessageAction {

    public static final int ID = 0xe9763aec;

    public int progress;

    public SendMessageUploadVideoAction() {
    }

    public SendMessageUploadVideoAction(int progress) {
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