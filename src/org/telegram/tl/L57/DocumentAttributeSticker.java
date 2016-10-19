package org.telegram.tl.L57;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.TLObject;
import org.telegram.tl.TLVector;
import org.telegram.tl.APIContext;
import org.telegram.tl.L57.*;

public class DocumentAttributeSticker extends org.telegram.tl.TLDocumentAttribute {

    public static final int ID = 0x6319d612;

    public int flags;
    public String alt;
    public org.telegram.tl.TLInputStickerSet stickerset;
    public org.telegram.tl.TLMaskCoords mask_coords;

    public DocumentAttributeSticker() {
    }

    public DocumentAttributeSticker(int flags, String alt, org.telegram.tl.TLInputStickerSet stickerset, org.telegram.tl.TLMaskCoords mask_coords) {
        this.flags = flags;
        this.alt = alt;
        this.stickerset = stickerset;
        this.mask_coords = mask_coords;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        flags = buffer.readInt();
        alt = buffer.readString();
        stickerset = (org.telegram.tl.TLInputStickerSet) buffer.readTLObject(APIContext.getInstance());
        if ((flags & (1 << 0)) != 0) {
            mask_coords = (org.telegram.tl.TLMaskCoords) buffer.readTLObject(APIContext.getInstance());
        }
    }

    @Override
    public ProtocolBuffer serialize() {
        ProtocolBuffer buffer = new ProtocolBuffer(40);
        setFlags();
        serializeTo(buffer);
        return buffer;
    }

    public void setFlags() {
        if (mask_coords != null) {
            flags |= (1 << 0);
        }
    }

    @Override
    public void serializeTo(ProtocolBuffer buff) {
        buff.writeInt(getConstructor());
        buff.writeInt(flags);
        buff.writeString(alt);
        buff.writeTLObject(stickerset);
        if ((flags & (1 << 0)) != 0) {
            buff.writeTLObject(mask_coords);
        }
    }

    public boolean is_mask() {
        return (flags & (1 << 1)) != 0;
    }

    public void set_mask(boolean v) {
        if (v) {
            flags |= (1 << 1);
        } else {
            flags &= ~(1 << 1);
        }
    }

    public int getConstructor() {
        return ID;
    }
}