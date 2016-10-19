package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DocumentAttributeVideo extends org.telegram.tl.TLDocumentAttribute {

    public static final int ID = 0x5910cccb;

    public int duration;
    public int w;
    public int h;

    public DocumentAttributeVideo() {
    }

    public DocumentAttributeVideo(int duration, int w, int h) {
        this.duration = duration;
        this.w = w;
        this.h = h;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        duration = buffer.readInt();
        w = buffer.readInt();
        h = buffer.readInt();
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(16);
        serializeTo(buffer);
        return buffer;
    }


    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(duration);
        buff.writeInt(w);
        buff.writeInt(h);
    }


    public int getConstructor() {
        return ID;
    }
}