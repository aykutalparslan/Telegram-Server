// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;

namespace Ferrite.Crypto
{
    public class KeyProvider :IKeyProvider
    {
        private Dictionary<long, IRSAKey> keyPairs = new();
        public KeyProvider()
        {
            RSAKey keyPair = new RSAKey();
            keyPair.Init("default");
            keyPairs.Add(keyPair.Fingerprint, keyPair);
        }

        public IList<long> GetRSAFingerprints()
        {
            return keyPairs.Keys.ToList();
        }

        public IRSAKey? GetKey(long fingerprint)
        {
            if(keyPairs.TryGetValue(fingerprint, out var keyPair))
            {
                return keyPair;
            }
            return null;
        }
    }
}

