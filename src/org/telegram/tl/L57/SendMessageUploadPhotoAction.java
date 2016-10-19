package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class SendMessageUploadPhotoAction extends org.telegram.tl.TLSendMessageAction {

    public static final int ID = 0xd1d34a26;

    public int progress;

    public SendMessageUploadPhotoAction() {
    }

    public SendMessageUploadPhotoAction(int progress) {
        this.progress = progress;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        progress = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(8);
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