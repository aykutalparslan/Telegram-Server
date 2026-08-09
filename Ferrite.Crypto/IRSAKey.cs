// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Security.Cryptography;

namespace Ferrite.Crypto
{
    public interface IRSAKey
    {
        public RSA? Key { get; }
        public RSAParameters PublicKeyParameters { get; }
        public RSAParameters PrivateKeyParameters { get; }
        public long Fingerprint { get; }
        public void Init(string alias);
        public byte[] EncryptBlock(byte[] data, bool usePublicKey = true);
        public byte[] DecryptBlock(byte[] data, bool usePrivateKey = true);
        public string ExportPublicKey();
        public string ExportPrivateKey();
    }
}

