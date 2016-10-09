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

package org.telegram.tl.photos;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.core.UserStore;
import org.telegram.data.DatabaseConnection;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.tl.*;
import org.telegram.tl.Photo;
import org.telegram.tl.service.rpc_error;

public class UploadProfilePhoto extends TLObject implements TLMethod {

    public static final int ID = -720397176;

    public TLInputFile file;
    public String caption;
    public TLInputGeoPoint geo_point;
    public TLInputPhotoCrop crop;

    public UploadProfilePhoto() {
    }

    public UploadProfilePhoto(TLInputFile file, String caption, TLInputGeoPoint geo_point, TLInputPhotoCrop crop){
        this.file = file;
        this.caption = caption;
        this.geo_point = geo_point;
        this.crop = crop;
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
        file = (TLInputFile) buffer.readTLObject(APIContext.getInstance());
        caption = buffer.readString();
        geo_point = (TLInputGeoPoint) buffer.readTLObject(APIContext.getInstance());
        crop = (TLInputPhotoCrop) buffer.readTLObject(APIContext.getInstance());
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
        buff.writeTLObject(file);
        buff.writeString(caption);
        buff.writeTLObject(geo_point);
        buff.writeTLObject(crop);
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {
        if (context.isAuthorized()) {
            int date = (int) (System.currentTimeMillis() / 1000L);
            DatabaseConnection.getInstance().saveProfilePhoto(context.getUserId(), ((InputFile) file).id, caption,
                    0, 0, 0, 0, 0, date);

            Photo photo = new org.telegram.tl.Photo(((InputFile) file).id, ((InputFile) file).id, date, new TLVector<TLPhotoSize>());
            TLVector<TLUser> users = new TLVector<>();
            users.add(UserStore.getInstance().getUser(context.getUserId()).toUser(context.getApiLayer()));

            return new org.telegram.tl.photos.Photo(photo, users);
        }
        return rpc_error.UNAUTHORIZED();
    }
}