package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DisabledFeature extends TLDisabledFeature {

    public static final int ID = 0xae636f24;

    public String feature;
    public String description;

    public DisabledFeature() {
    }

    public DisabledFeature(String feature, String description) {
        this.feature = feature;
        this.description = description;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        feature = buffer.readString();
        description = buffer.readString();
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
        buff.writeString(feature);
        buff.writeString(description);
    }


    public int getConstructor() {
        return ID;
    }
}