// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

public interface IFileOwner
{
    public byte[] TLObjectHeader { get; init; }
    public ValueTask<Stream> GetFileStream();
    public long ReqMsgId { get; }
}