// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class UserRepository : IUserRepository
{
    private const string NoUsernameKey = "\0";
    private readonly IKVStore _store;
    private readonly IKVStore _storeTtl;
    private readonly IKVStore _storeAbout;

    public UserRepository(IKVStore store, IKVStore storeTtl, IKVStore storeAbout)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "users",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "phone", Type = DataType.String },
                new DataColumn { Name = "username", Type = DataType.String }),
            new KeyDefinition("by_phone",
                new DataColumn { Name = "phone", Type = DataType.String }),
            new KeyDefinition("by_username",
                new DataColumn { Name = "username", Type = DataType.String })));
        _storeTtl = storeTtl;
        _storeTtl.SetSchema(new TableDefinition("ferrite", "account_ttls_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        _storeAbout = storeAbout;
        _storeAbout.SetSchema(new TableDefinition("ferrite", "users_about_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutUser(TLUser user)
    {
        var u = user.AsUser();
        return _store.Put(user.AsSpan().ToArray(),
            u.Id, PhoneKey(u.Phone),
            UsernameKey(u.Username));
    }

    public bool UpdateUsername(long userId, string username)
    {
        var userBytes = _store.Get(userId);
        if (userBytes != null)
        {
            var user = new User(userBytes);
            string oldUsername = user.Username.Length == 0
                ? ""
                : Encoding.UTF8.GetString(user.Username);
            if (username == oldUsername) return false;
            string userPhone = PhoneKey(user.Phone);
            using var userNew = user.Clone().Username(Encoding.UTF8.GetBytes(username)).Build();
            _store.Delete(user.Id, userPhone, UsernameKey(oldUsername));
            _store.Put(userNew.TLBytes!.Value.AsSpan().ToArray(),
                user.Id, userPhone, UsernameKey(username));
            return true;
        }

        return false;
    }

    public bool UpdateUserPhone(long userId, string phone)
    {
        var userBytes = _store.Get(userId);
        if (userBytes != null)
        {
            var user = new User(userBytes);
            string oldPhone = user.Phone.Length > 0 ? Encoding.UTF8.GetString(user.Phone) : "";
            if (oldPhone == phone) return false;
            using var userNew = user.Clone().Phone(Encoding.UTF8.GetBytes(phone)).Build();
            string username = UsernameKey(user.Username);
            _store.Delete(user.Id, PhoneKey(oldPhone), username);
            _store.Put(userNew.TLBytes!.Value.AsSpan().ToArray(),
                user.Id, PhoneKey(phone), username);
            return true;
        }

        return false;
    }

    public TLUser? GetUser(long userId)
    {
        var userBytes = _store.Get(userId);
        if (userBytes != null)
        {
            return new TLUser(userBytes, 0, userBytes.Length);
        }

        return null;
    }

    public TLUser? GetUser(string phone)
    {
        var userBytes = _store.GetBySecondaryIndex("by_phone", PhoneKey(phone));
        if (userBytes != null)
        {
            return new TLUser(userBytes, 0, userBytes.Length);
        }

        return null;
    }

    public long? GetUserId(string phone)
    {
        var userBytes = _store.GetBySecondaryIndex("by_phone", PhoneKey(phone));
        if (userBytes != null)
        {
            var user = new User(userBytes);
            return user.Id;
        }

        return null;
    }

    public TLUser? GetUserByUsername(string username)
    {
        if (username.Length == 0) return null;
        var userBytes = _store.GetBySecondaryIndex("by_username",
            UsernameKey(username));
        if (userBytes != null)
        {
            return new TLUser(userBytes, 0, userBytes.Length);
        }

        return null;
    }

    public bool DeleteUser(long userId)
    {
        return _store.Delete(userId);
    }

    public bool UpdateAccountTtl(long userId, int accountDaysTtl)
    {
        var expire = DateTimeOffset.Now.AddDays(accountDaysTtl).ToUnixTimeSeconds();
        using var row = AccountTtlState.Builder().ExpiresAt(expire).Build();
        return _storeTtl.Put(row.ToReadOnlySpan().ToArray(), userId);
    }

    public int GetAccountTtl(long userId)
    {
        var val = _storeTtl.Get(userId);
        if (val == null) return 365;
        var value = new TLBytes(val, 0, val.Length);
        if (value.Constructor != Constructors.baseLayer_AccountTtlState)
            throw new InvalidDataException("Account-TTL codec/version mismatch.");
        var expire = ((TLAccountTtlState)value).AsAccountTtlState().ExpiresAt;
        var expireDays = DateTimeOffset.FromUnixTimeSeconds(expire) - DateTimeOffset.Now;
        return expireDays.Days;
    }

    public bool PutAbout(long userId, string about)
    {
        using var row = UserAboutState.Builder().About(Encoding.UTF8.GetBytes(about)).Build();
        return _storeAbout.Put(row.ToReadOnlySpan().ToArray(), userId);
    }

    public string? GetAbout(long userId)
    {
        var about = _storeAbout.Get(userId);
        if (about == null) return null;
        var value = new TLBytes(about, 0, about.Length);
        if (value.Constructor != Constructors.baseLayer_UserAboutState)
            throw new InvalidDataException("User-about codec/version mismatch.");
        return Encoding.UTF8.GetString(((TLUserAboutState)value).AsUserAboutState().About);
    }

    private static string PhoneKey(string phone)
    {
        Span<char> digits = stackalloc char[phone.Length];
        int length = 0;
        foreach (char value in phone)
        {
            if (char.IsAsciiDigit(value)) digits[length++] = value;
        }

        return new string(digits[..length]);
    }

    private static string PhoneKey(ReadOnlySpan<byte> phone) =>
        PhoneKey(Encoding.UTF8.GetString(phone));

    private static string UsernameKey(ReadOnlySpan<byte> username) =>
        username.Length == 0 ? NoUsernameKey : Encoding.UTF8.GetString(username);

    private static string UsernameKey(string username) =>
        username.Length == 0 ? NoUsernameKey : username;
}
