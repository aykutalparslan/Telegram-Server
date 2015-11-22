package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class MessageMediaUnsupportedL12 extends TLMessageMedia {

    public static final int ID = 0x29632a36;

    public byte[] bytes;

    public MessageMediaUnsupportedL12() {
    }

    public MessageMediaUnsupportedL12(byte[] bytes) {
        this.bytes = bytes;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        bytes = buffer.readBytes();
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
        buff.writeBytes(bytes);
    }

    public int getConstructor() {
        return ID;
    }
}