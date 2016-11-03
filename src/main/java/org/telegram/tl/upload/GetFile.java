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

package org.telegram.tl.upload;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.data.DatabaseConnection;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.service.rpc_error;
import org.telegram.tl.storage.FileUnknown;

public class GetFile extends TLObject implements TLMethod {

    public static final int ID = -475607115;

    public TLInputFileLocation location;
    public int offset;
    public int limit;

    public GetFile() {
    }

    public GetFile(TLInputFileLocation location, int offset, int limit){
        this.location = location;
        this.offset = offset;
        this.limit = limit;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        location = (TLInputFileLocation) buffer.readTLObject(APIContext.getInstance());
        offset = buffer.readInt();
        limit = buffer.readInt();
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
        buff.writeTLObject(location);
        buff.writeInt(offset);
        buff.writeInt(limit);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            long file_id = 0;
            if (location instanceof InputFileLocation) {
                file_id = ((InputFileLocation) location).secret;
            } else if (location instanceof InputVideoFileLocation) {
                file_id = ((InputVideoFileLocation) location).id;
            } else if (location instanceof InputEncryptedFileLocation) {
                file_id = ((InputEncryptedFileLocation) location).id;
            } else if (location instanceof InputAudioFileLocation) {
                file_id = ((InputAudioFileLocation) location).id;
            } else if (location instanceof InputDocumentFileLocation) {
                file_id = ((InputDocumentFileLocation) location).id;
            } else if (location instanceof InputEncryptedFileLocation) {
                file_id = ((InputEncryptedFileLocation) location).id;
            }
            if (file_id == 0) {
                return rpc_error.BAD_REQUEST();
            }

            int file_size = DatabaseConnection.getInstance().getFileSize(file_id);
            int remaining = file_size - offset;
            limit = Math.min(limit, remaining);

            int part_size = DatabaseConnection.getInstance().getPartSizeForFile(file_id);
            int first_part_num = offset / part_size;
            int last_part_num = first_part_num + limit / part_size - 1;
            if (limit % part_size != 0) {
                last_part_num++;
            }
            ProtocolBuffer buffer = new ProtocolBuffer(limit);
            if (first_part_num == 0) {
                byte[] bytes = DatabaseConnection.getInstance().getFilePart(file_id, 0);
                buffer.write(bytes, 0, bytes.length);
            } else {
                for (int i = first_part_num; i <= last_part_num; i++) {
                    int offset_for_part = 0;
                    if (i == first_part_num && offset % part_size != 0) {
                        offset_for_part = offset % part_size;
                    }
                    int limit_for_part = part_size;
                    if (i == last_part_num && limit % part_size != 0) {
                        limit_for_part = limit % part_size;
                    }

                    byte[] bytes = DatabaseConnection.getInstance().getFilePart(file_id, i);
                    limit_for_part = Math.min(limit_for_part, bytes.length);
                    buffer.write(bytes, offset_for_part, limit_for_part - offset_for_part);
                }
            }

            return new File(new FileUnknown(), 0, buffer.getBytes());
        }
        return rpc_error.UNAUTHORIZED();
    }
}