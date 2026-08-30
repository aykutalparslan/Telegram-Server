// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Data.Models;

namespace Ferrite.Data.Repositories;

public class PrivacyRulesRepository : IPrivacyRulesRepository
{
    private readonly IKVStore _store;
    public PrivacyRulesRepository(IKVStore store)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "privacy_rules",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "privacy_key", Type = DataType.Int },
                new DataColumn { Name = "privacy_rule_type", Type = DataType.Int })));
    }
    public bool PutPrivacyRules(long userId, InputPrivacyKey key, Vector rules)
    {
        _store.Delete(userId, (int)key);
        int count = rules.Count;
        for(int i = 0 ; i < count; i++)
        {
            var rule = rules.ReadTLObject();
            var ruleBytes = rule.ToArray();

            _store.Put(ruleBytes, userId, (int)key,
                (int)GetPrivacyValueType(((PrivacyRuleView)rule).Constructor));
        }

        return true;
    }

    private PrivacyRuleType GetPrivacyValueType(int constructor) => constructor switch
    {
        Constructors.baseLayer_PrivacyValueAllowContacts => PrivacyRuleType.AllowContacts,
        Constructors.baseLayer_PrivacyValueAllowAll => PrivacyRuleType.AllowAll,
        Constructors.baseLayer_PrivacyValueAllowUsers => PrivacyRuleType.AllowUsers,
        Constructors.baseLayer_PrivacyValueDisallowContacts => PrivacyRuleType.DisallowContacts,
        Constructors.baseLayer_PrivacyValueDisallowAll => PrivacyRuleType.DisallowAll,
        Constructors.baseLayer_PrivacyValueDisallowUsers => PrivacyRuleType.DisallowUsers,
        Constructors.baseLayer_PrivacyValueAllowChatParticipants => PrivacyRuleType.AllowChatParticipants,
        Constructors.baseLayer_PrivacyValueDisallowChatParticipants => PrivacyRuleType.DisallowChatParticipants,
        Constructors.baseLayer_PrivacyValueAllowCloseFriends => PrivacyRuleType.AllowCloseFriends,
        Constructors.baseLayer_PrivacyValueAllowPremium => PrivacyRuleType.AllowPremium,
        Constructors.baseLayer_PrivacyValueAllowBots => PrivacyRuleType.AllowBots,
        Constructors.baseLayer_PrivacyValueDisallowBots => PrivacyRuleType.DisallowBots,
        _ => throw new ArgumentException($"Unknown privacy rule constructor: {constructor}")
    };

    public ValueTask<ICollection<TLPrivacyRule>> GetPrivacyRulesAsync(long userId, InputPrivacyKey key)
    {
        List<TLPrivacyRule> rules = new();
        var iter = _store.Iterate(userId, (int)key);
        foreach (var ruleBytes in iter)
        {
            rules.Add(new TLPrivacyRule(ruleBytes, 0, ruleBytes.Length));
        }

        return new ValueTask<ICollection<TLPrivacyRule>>(rules);
    }

    public bool DeletePrivacyRules(long userId, InputPrivacyKey key)
    {
        return _store.Delete(userId, (int)key);
    }

    public bool DeletePrivacyRules(long userId)
    {
        return _store.Delete(userId);
    }
}
