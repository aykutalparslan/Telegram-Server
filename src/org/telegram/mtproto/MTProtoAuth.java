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

package org.telegram.mtproto;

import org.bouncycastle.crypto.params.DHParameters;
import org.telegram.data.DatabaseConnection;
import org.telegram.mtproto.secure.CryptoUtils;
import org.telegram.tl.APIContext;
import org.telegram.tl.TLObject;
import org.telegram.tl.pq.*;
import org.telegram.tl.service.msgs_ack;

import java.math.BigInteger;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.security.*;
import java.util.Date;
import java.util.Random;

/**
 * Created by aykut on 27/09/15.
 */
public class MTProtoAuth {
    public static KeyPair RSAKeyPair;

    private byte[] authNonce;
    private byte[] authServerNonce;
    private byte[] authNewNonce;
    private byte[] authNewNonceHash;
    private BigInteger p;
    private BigInteger q;
    private BigInteger pq;
    private byte[] pqBytes;
    private BigInteger g;
    private BigInteger dh_prime;
    private BigInteger a;
    private BigInteger g_a;
    private BigInteger g_b;
    byte[] tmp_aes_key;
    byte[] tmp_aes_iv;

    private byte[] authKey;
    private long authKeyId;
    private byte[] authKeyAuxHash;

    private boolean processedPQRes;

    private ProtocolBuffer reqPQMsgData;
    private ProtocolBuffer resPQMsgData;
    private ProtocolBuffer reqDHMsgData;
    private ProtocolBuffer setClientDHParamsMsgData;
    private boolean wasDisconnect = false;

    private long lastOutgoingMessageId;

    int timeDifference;
    ServerSalt serverSalt;

    public MTProtoAuth() {

    }

    public long getAuthKeyId(){
        return authKeyId;
    }

    public ProtocolBuffer resPQ(req_pq reqPq){
        authNonce = reqPq.nonce;
        authServerNonce = null;
        authNewNonce = null;
        authKey = null;
        authKeyId = 0;
        processedPQRes = false;

        resPQ resPq = new resPQ();
        resPq.nonce= authNonce;
        byte[] serverNonceBytes = new byte[16];
        Utilities.random.nextBytes(serverNonceBytes);
        authServerNonce = resPq.server_nonce = serverNonceBytes;

        calculatePq();
        resPq.pq = pqBytes;

        if(RSAKeyPair == null){
            RSAKeyPair = Utilities.loadRSAKeyPair();
        }

        PublicKey pk = RSAKeyPair.getPublic();
        BigInteger modulus = ((java.security.interfaces.RSAPublicKey)pk).getModulus();
        BigInteger exponent = ((java.security.interfaces.RSAPublicKey)pk).getPublicExponent();

        rsa_public_key rsaPublicKey = new rsa_public_key();
        rsaPublicKey.n = modulus.toByteArray();
        rsaPublicKey.e = exponent.toByteArray();
        String mod = Utilities.bytesToHex(rsaPublicKey.n);
        String exp = Utilities.bytesToHex(rsaPublicKey.e);

        byte[] rsaBytes = rsaPublicKey.serialize().getBytes();

        byte[] sha1 = Utilities.computeSHA1(rsaBytes);

        byte[] fingerprint = new byte[8];
        for (int i = 0; i < 8;i++){
            fingerprint[i] = sha1[sha1.length - (8-i)];
        }

        long fplong = Utilities.bytesToLong(fingerprint);

        resPq.server_public_key_fingerprints.add(fplong);

        resPQMsgData = prepareMessageData(resPq, generateMessageId());

        return  resPQMsgData;
    }

    public ProtocolBuffer server_DH_params(req_DH_params req_dh_params){

        byte[] data = Utilities.decryptWithRSA(RSAKeyPair.getPrivate(), req_dh_params.encrypted_data);

        ProtocolBuffer p_q_innner_buff = new ProtocolBuffer(235);
        p_q_innner_buff.write(data, 21, 235);

        p_q_inner_data p_q_inner_data = (p_q_inner_data)APIContext.getInstance().deserialize(p_q_innner_buff);
        authNewNonce = p_q_inner_data.new_nonce;
        authNewNonceHash = new byte[16];
        byte[] newNonceSHA1 = Utilities.computeSHA1(authNewNonce);
        for (int i = 0; i < 16; i++) {
            authNewNonceHash[i] = newNonceSHA1[i+4];
        }
        server_DH_params_ok server_dh_params_ok = new server_DH_params_ok();
        server_dh_params_ok.nonce = authNonce;
        server_dh_params_ok.server_nonce = authServerNonce;

        server_DH_inner_data server_dh_inner_data = new server_DH_inner_data();
        server_dh_inner_data.nonce = authNonce;
        server_dh_inner_data.server_nonce = authServerNonce;
        int[] gs= new int[]{2,3,4,5,6,7};
        Random rnd = new Random();
        server_dh_inner_data.g = gs[rnd.nextInt(5)];
        g = BigInteger.valueOf(server_dh_inner_data.g);

        SecureRandom srnd = new SecureRandom();
        //String prime = "C7 1C AE B9 C6 B1 C9 04 8E 6C 52 2F 70 F1 3F 73 98 0D 40 23 8E 3E 21 C1 49 34 D0 37 56 3D 93 0F 48 19 8A 0A A7 C1 40 58 22 94 93 D2 25 30 F4 DB FA 33 6F 6E 0A C9 25 13 95 43 AE D4 4C CE 7C 37 20 FD 51 F6 94 58 70 5A C6 8C D4 FE 6B 6B 13 AB DC 97 46 51 29 69 32 84 54 F1 8F AF 8C 59 5F 64 24 77 FE 96 BB 2A 94 1D 5B CD 1D 4A C8 CC 49 88 07 08 FA 9B 37 8E 3C 4F 3A 90 60 BE E6 7C F9 A4 A4 A6 95 81 10 51 90 7E 16 27 53 B5 6B 0F 6B 41 0D BA 74 D8 A8 4B 2A 14 B3 14 4E 0E F1 28 47 54 FD 17 ED 95 0D 59 65 B4 B9 DD 46 58 2D B1 17 8D 16 9C 6B C4 65 B0 D6 FF 9C A3 92 8F EF 5B 9A E4 E4 18 FC 15 E8 3E BE A0 F8 7F A9 FF 5E ED 70 05 0D ED 28 49 F4 7B F9 59 D9 56 85 0C E9 29 85 1F 0D 81 15 F6 35 B1 05 EE 2E 4E 15 D0 4B 24 54 BF 6F 4F AD F0 34 B1 04 03 11 9C D8 E3 B9 2F CC 5B";
        //String prime_hex = prime.replace(" ", "");

        dh_prime = BigInteger.probablePrime(2048, srnd);
        byte[] dh_primeBytes = dh_prime.toByteArray();
        if(dh_primeBytes[0] == 0 && dh_primeBytes.length == 257){
            server_dh_inner_data.dh_prime = subStr(dh_primeBytes, 1, 256);
        } else {
            server_dh_inner_data.dh_prime = dh_primeBytes;
        }

        do {
            a = new BigInteger(2048, srnd);
        } while (a.compareTo(dh_prime) >= 0);

        g_a = g.modPow(a, dh_prime);
        server_dh_inner_data.g_a = g_a.toByteArray();
        server_dh_inner_data.server_time = (int) (new Date().getTime()/1000);
        byte[] innerDataBytes = server_dh_inner_data.serialize().getBytes();

        ProtocolBuffer answerWithHash = new ProtocolBuffer(innerDataBytes.length + 48);
        answerWithHash.write(Utilities.computeSHA1(innerDataBytes));
        answerWithHash.write(innerDataBytes);
        int extraLen = 0;
        while ((answerWithHash.length() + extraLen) % 16 != 0) {
            extraLen++;
        }

        byte[] b = new byte[extraLen];
        Utilities.random.nextBytes(b);
        answerWithHash.write(b);

        tmp_aes_key = concat(Utilities.computeSHA1(concat(authNewNonce, authServerNonce)),
                subStr(Utilities.computeSHA1(concat(authServerNonce, authNewNonce)), 0, 12));

        tmp_aes_iv = concat(subStr(Utilities.computeSHA1(concat(authServerNonce, authNewNonce)), 12, 8),
                Utilities.computeSHA1(concat(authNewNonce, authNewNonce)),
                subStr(authNewNonce, 0, 4));

        byte[] encryptedAnswer = CryptoUtils.AES256IGEEncrypt(answerWithHash.getBytes(), tmp_aes_iv, tmp_aes_key);

        server_dh_params_ok.encrypted_answer = encryptedAnswer;

        return prepareMessageData(server_dh_params_ok, generateMessageId());
    }

    public ProtocolBuffer set_client_DH_params(set_client_DH_params set_client_dh_params){
        byte[] encrypted_answer = set_client_dh_params.encrypted_data;

        byte[] answer = CryptoUtils.AES256IGEDecrypt(encrypted_answer, tmp_aes_iv, tmp_aes_key);
        ProtocolBuffer client_DH_inner_buff = new ProtocolBuffer(answer.length - 20);
        client_DH_inner_buff.write(answer, 20, answer.length - 20);

        client_DH_inner_data client_dh_inner_data = (client_DH_inner_data)APIContext.getInstance().deserialize(client_DH_inner_buff);

        g_b = CryptoUtils.loadBigInt(client_dh_inner_data.g_b);

        authKey = g_b.modPow(a, dh_prime).toByteArray();
        byte[] authKeyHash = Utilities.computeSHA1(authKey);
        byte[] authKeyArr = new byte[8];
        System.arraycopy(authKeyHash, authKeyHash.length - 8, authKeyArr, 0, 8);
        ByteBuffer buffer = ByteBuffer.wrap(authKeyArr);
        buffer.order(ByteOrder.LITTLE_ENDIAN);
        authKeyId = buffer.getLong();

        authKeyAuxHash = subStr(Utilities.computeSHA1(authKey), 0 ,8);
        byte[] b_t1 = new byte[1];
        b_t1[0] = 1;
        byte[] new_nonce_hash1 = subStr(Utilities.computeSHA1(concat(authNewNonce, b_t1, authKeyAuxHash)), 4, 16);
        b_t1[0] = 2;
        byte[] new_nonce_hash2 = subStr(Utilities.computeSHA1(concat(authNewNonce, b_t1, authKeyAuxHash)), 4, 16);
        b_t1[0] = 3;
        byte[] new_nonce_hash3 = subStr(Utilities.computeSHA1(concat(authNewNonce, b_t1, authKeyAuxHash)), 4, 16);

        serverSalt = new ServerSalt();
        byte[] serverSaltBytes = CryptoUtils.xor(subStr(authNewNonce, 0, 8), subStr(authServerNonce, 0, 8));
        serverSalt.value = Utilities.bytesToLong(serverSaltBytes);

        dh_gen_ok dh_gen_ok = new dh_gen_ok();
        dh_gen_ok.nonce = authNonce;
        dh_gen_ok.server_nonce = authServerNonce;
        dh_gen_ok.new_nonce_hash1 = new_nonce_hash1;

        DatabaseConnection.getInstance().saveAuthKey(authKeyId, authKey);
        long time_salt = System.currentTimeMillis();
        DatabaseConnection.getInstance().saveServerSalt(authKeyId, time_salt, serverSalt.value, 86400);

        SecureRandom srnd = new SecureRandom();
        for (int i = 0; i < 64; i++) {
            srnd.nextBytes(serverSaltBytes);
            DatabaseConnection.getInstance().saveServerSalt(authKeyId, time_salt + ((i+1) * 86400000L), Utilities.bytesToLong(serverSaltBytes), (i+1) * 86400);
        }

        return prepareMessageData(dh_gen_ok, generateMessageId());
    }

    public ProtocolBuffer encryptRpc(TLObject rpc, int seqNo){
        long messageId = generateMessageId();
        ProtocolBuffer messageBody = rpc.serialize();
        int messageSeqNo = seqNo;

        ProtocolBuffer buffer = new ProtocolBuffer(8 + 8 + 8 + 4 + 4 + messageBody.length());
        buffer.writeLong(serverSalt.value);
        buffer.writeLong(messageId);
        buffer.writeInt(messageSeqNo);
        buffer.writeInt(messageBody.length());
        buffer.write(messageBody.getBytes());

        int extraLen = 0;
        while ((buffer.length() + extraLen) % 16 != 0) {
            extraLen++;
        }

        byte[] b = new byte[extraLen];
        Utilities.random.nextBytes(b);
        buffer.write(b);

        byte[] dataForEncryption = buffer.getBytes();

        byte[] messageKeyFull = Utilities.computeSHA1(dataForEncryption, 0, dataForEncryption.length);
        byte[] messageKey = new byte[16];
        System.arraycopy(messageKeyFull, messageKeyFull.length - 16, messageKey, 0, 16);
        MessageKeyData keyData = MessageKeyData.generateMessageKeyData(authKey, messageKey, false);

        byte[] encryptedData = CryptoUtils.AES256IGEEncrypt(dataForEncryption, keyData.aesIv, keyData.aesKey);

        ProtocolBuffer data = new ProtocolBuffer(8 + messageKey.length + encryptedData.length);
        data.writeLong(authKeyId);
        data.write(messageKey);
        data.write(encryptedData);

        return data;
    }

    public ProtocolBuffer decryptRpc(byte[] bytes, byte[] messageKey){
        MessageKeyData keyData = MessageKeyData.generateMessageKeyData(authKey, messageKey, false);
        byte[] decryptedData = CryptoUtils.AES256IGEDecrypt(bytes, keyData.aesIv, keyData.aesKey);
        ProtocolBuffer buff = new ProtocolBuffer(decryptedData);

        return buff;
    }

    public static byte[] concat(byte[]... v) {
        int len = 0;
        for (int i = 0; i < v.length; i++) {
            len += v[i].length;
        }
        byte[] res = new byte[len];
        int offset = 0;
        for (int i = 0; i < v.length; i++) {
            System.arraycopy(v[i], 0, res, offset, v[i].length);
            offset += v[i].length;
        }
        return res;
    }

    private static byte[] subStr(byte[] bytes, int position, int length) {
        byte[] c = new byte[length];
        System.arraycopy(bytes, position, c, 0, length);
        return c;
    }

    public ProtocolBuffer msgs_ack(long messageId) {
        msgs_ack msgsAck = new msgs_ack();
        msgsAck.msg_ids.add(messageId);
        return prepareMessageData(msgsAck, generateMessageId());
    }


    private void calculatePq() {
        BigInteger a = getRandomPrime();
        BigInteger b = getRandomPrime();
        int comparison = a.compareTo(b);
        if(comparison == -1){
            p = a;
            q = b;
        } else {
            p = b;
            q = a;
        }

        pq = p.multiply(q);

        pqBytes = pq.toByteArray();
    }

    BigInteger getRandomPrime(){
        Random rnd = new Random();
        int num = rnd. nextInt(999000000)+1000000000;
        BigInteger probablePrime = BigInteger.valueOf(num).nextProbablePrime();
        if(probablePrime.longValue() < 2000000000 && probablePrime.longValue() > 1000000000){
            return probablePrime;
        } else {
            return  getRandomPrime();
        }

    }

    long generateMessageId() {
        long messageId = (long) ((((double) System.currentTimeMillis()) * 4294967296.0) / 1000.0);
        if (messageId <= lastOutgoingMessageId) {
            messageId = lastOutgoingMessageId + 1;
        }
        while (messageId % 4 != 0) {
            messageId++;
        }

        lastOutgoingMessageId = messageId;
        return messageId;
    }

    ProtocolBuffer prepareMessageData(TLObject message, long messageId) {
        byte[] messageBytes = message.serialize().getBytes();
        ProtocolBuffer buff = new ProtocolBuffer(8 + 8 + 4 + messageBytes.length);
        buff.writeLong(0);
        buff.writeLong(messageId);
        buff.writeInt(messageBytes.length);
        buff.write(messageBytes);

        return buff;
    }

}
