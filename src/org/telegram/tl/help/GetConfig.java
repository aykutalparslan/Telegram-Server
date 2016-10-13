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

package org.telegram.tl.help;

import org.telegram.core.TLContext;
import org.telegram.core.TLMethod;
import org.telegram.mtproto.ProtocolBuffer;
import org.telegram.server.ServerConfig;
import org.telegram.tl.*;

public class GetConfig extends TLObject implements TLMethod {

    public static final int ID = -990308245;


    public GetConfig(){
    }

    @Override
    public void deserialize(ProtocolBuffer buffer) {
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
    }

    public int getConstructor() {
        return ID;
    }

    @Override
    public TLObject execute(TLContext context, long messageId, long reqMessageId) {


        TLVector<TLDisabledFeature> disabledFeatures = new TLVector<>();

        if (context.getApiLayer() >= 48) {
            DcOption dcOption = new DcOption(0, ServerConfig.SERVER_ID, "127.0.0.1", ServerConfig.SERVER_PORT);
            TLVector<TLDcOption> dcOptions = new TLVector<>();
            dcOptions.add(dcOption);

            return new ConfigL48((int) (System.currentTimeMillis() / 1000L), (int) (System.currentTimeMillis() / 1000L) + 860000,
                    false, 1, dcOptions, 200, 100, 100, 120000, 5000, 30000, 300000, 30000, 1500, 10, 60000, 2, 100, 6000, disabledFeatures);
        } else {
            DcOption dcOption = new DcOption(0, ServerConfig.SERVER_ID, "10.0.2.2", ServerConfig.SERVER_PORT);
            TLVector<TLDcOption> dcOptions = new TLVector<>();
            dcOptions.add(dcOption);

            return new Config((int) (System.currentTimeMillis() / 1000L), (int) (System.currentTimeMillis() / 1000L) + 860000,
                    false, 1, dcOptions, 200, 100, 100, 120000, 5000, 30000, 300000, 30000, 1500, 10, 60000, 2, disabledFeatures);
        }
    }
}