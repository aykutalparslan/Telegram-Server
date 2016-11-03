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

package org.telegram.tl.service;

import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;

public class bad_server_salt extends TLObject {

    public static final int ID = 0xedab447b;

    public long bad_msg_id;
    public int bad_msg_seqno;
    public int error_code;
    public long new_server_salt;

    public bad_server_salt() {

    }

    public bad_server_salt(long bad_msg_id, int bad_msg_seqno, int error_code, long new_server_salt){
        this.bad_msg_id = bad_msg_id;
        this.bad_msg_seqno = bad_msg_seqno;
        this.error_code = error_code;
        this.new_server_salt = new_server_salt;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        bad_msg_id = buffer.readLong();
        bad_msg_seqno = buffer.readInt();
        error_code = buffer.readInt();
        new_server_salt = buffer.readLong();
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
        buff.writeLong(bad_msg_id);
        buff.writeInt(bad_msg_seqno);
        buff.writeInt(error_code);
        buff.writeLong(new_server_salt);
    }

    public int getConstructor() {
        return ID;
    }
}