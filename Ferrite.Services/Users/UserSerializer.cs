// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Users;

public sealed class UserSerializer
{
    private readonly IUserRepository _users;
    private readonly IUserStatusRepository _statuses;
    private readonly IContactsRepository _contacts;

    public UserSerializer(IUserRepository users, IUserStatusRepository statuses, IContactsRepository contacts)
    {
        _users = users;
        _statuses = statuses;
        _contacts = contacts;
    }

    public TLUser WithStatus(long viewerUserId, TLUser user)
    {
        using var status = _statuses.GetUserStatus(user.AsUser().Id);
        return Attach(viewerUserId, user, status);
    }

    public async ValueTask<TLUser> WithStatusAsync(long viewerUserId, TLUser user)
    {
        using var status = await _statuses.GetUserStatusAsync(user.AsUser().Id);
        return Attach(viewerUserId, user, status);
    }

    private TLUser Attach(long viewerUserId, TLUser user, TLUserStatus status)
    {
        long userId = user.AsUser().Id;

        var builder = user.AsUser().Clone()
            .Self(viewerUserId == userId);
        if (status.AsSpan().Length != 0)
        {
            builder = builder.Status(status.AsSpan());
        }

        if (viewerUserId != userId)
        {
            bool contact = _contacts.HasContact(viewerUserId, userId);
            builder = builder
                .Contact(contact)
                .MutualContact(contact && _contacts.HasContact(userId, viewerUserId));
        }

        return builder.Build();
    }

    public TLUser? Get(long viewerUserId, long userId)
    {
        using var user = _users.GetUser(userId);
        return user == null ? null : WithStatus(viewerUserId, user.Value);
    }

    public async ValueTask<TLUser?> GetAsync(long viewerUserId, long userId)
    {
        using var user = _users.GetUser(userId);
        return user == null ? null : await WithStatusAsync(viewerUserId, user.Value);
    }

    public byte[]? Bytes(long viewerUserId, long userId)
    {
        using var user = Get(viewerUserId, userId);
        return user?.AsSpan().ToArray();
    }

    public byte[] Bytes(long viewerUserId, TLUser user)
    {
        using var prepared = WithStatus(viewerUserId, user);
        return prepared.AsSpan().ToArray();
    }

    public bool Append(long viewerUserId, ref Vector users, long userId)
    {
        using var user = Get(viewerUserId, userId);
        if (user == null)
        {
            return false;
        }

        users.AppendTLObject(user.Value.AsSpan());
        return true;
    }

    public void Append(long viewerUserId, ref Vector users, TLUser user)
    {
        using var prepared = WithStatus(viewerUserId, user);
        users.AppendTLObject(prepared.AsSpan());
    }
}
