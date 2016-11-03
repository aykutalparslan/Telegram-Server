/*
 *     This file is part of Telegram Server
 *     Copyright (C) 2015  Aykut Alparslan KOÇ
 *
 *     Telegram Server is free software: you can redistribute it and/or modify
 *     it under the terms of the GNU General Public License as published by
 *     the Free Software Foundation, either version 3 of the License, or
 *     (at your option) any later version.
 *
 *     Telegram Server is distributed in the hope that it will be useful,
 *     but WITHOUT ANY WARRANTY; without even the implied warranty of
 *     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *     GNU General Public License for more details.
 *
 *     You should have received a copy of the GNU General Public License
 *     along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

package org.telegram.tl;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class InputEncryptedFileUploaded extends TLInputEncryptedFile {

    public static final int ID = 1690108678;

    public long id;
    public int parts;
    public String md5_checksum;
    public int key_fingerprint;

    public InputEncryptedFileUploaded() {
    }

    public InputEncryptedFileUploaded(long id, int parts, String md5_checksum, int key_fingerprint){
        this.id = id;
        this.parts = parts;
        this.md5_checksum = md5_checksum;
        this.key_fingerprint = key_fingerprint;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        id = buffer.readLong();
        parts = buffer.readInt();
        md5_checksum = buffer.readString();
        key_fingerprint = buffer.readInt();
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
        buff.writeLong(id);
        buff.writeInt(parts);
        buff.writeString(md5_checksum);
        buff.writeInt(key_fingerprint);
    }

    public int getConstructor() {
        return ID;
    }
}