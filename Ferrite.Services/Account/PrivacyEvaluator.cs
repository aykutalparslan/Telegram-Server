// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Account;

public sealed class PrivacyEvaluator
{
    private readonly IBlockedPeersRepository _blockedPeersRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IPrivacyRulesRepository _privacyRulesRepository;

    private readonly IUnitOfWork _unitOfWork;

    public PrivacyEvaluator(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IChatParticipantsRepository chatParticipantsRepository, IContactsRepository contactsRepository, IPrivacyRulesRepository privacyRulesRepository)
    {
        _blockedPeersRepository = blockedPeersRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _contactsRepository = contactsRepository;
        _privacyRulesRepository = privacyRulesRepository;

        _unitOfWork = unitOfWork;
    }

    public async Task<bool> IsChatInviteAllowed(long inviterUserId, long inviteeUserId)
    {
        var rules = await _privacyRulesRepository
            .GetPrivacyRulesAsync(inviteeUserId, InputPrivacyKey.ChatInvite);
        if (rules.Count == 0)
        {
            return true;
        }

        return await EvaluateStoredRules(rules, inviteeUserId, inviterUserId);
    }

    public async Task<CallPrivacyDecision> EvaluatePhoneCall(long callerUserId,
        long targetUserId)
    {
        if (IsBlockedBy(targetUserId, callerUserId) ||
            IsBlockedBy(callerUserId, targetUserId))
        {
            return CallPrivacyDecision.Blocked;
        }

        var rules = await _privacyRulesRepository
            .GetPrivacyRulesAsync(targetUserId, InputPrivacyKey.PhoneCall);
        if (rules.Count == 0)
        {
            return CallPrivacyDecision.Allowed;
        }

        return await EvaluateStoredRules(rules, targetUserId, callerUserId)
            ? CallPrivacyDecision.Allowed
            : CallPrivacyDecision.PrivacyRestricted;
    }

    public async Task<bool> IsPhoneP2PAllowed(long ownerUserId, long peerUserId)
    {
        var rules = await _privacyRulesRepository
            .GetPrivacyRulesAsync(ownerUserId, InputPrivacyKey.PhoneP2P);
        if (rules.Count == 0)
        {
            return _contactsRepository.HasContact(ownerUserId, peerUserId);
        }

        return await EvaluateStoredRules(rules, ownerUserId, peerUserId);
    }

    public async Task<bool> IsPhoneP2PAllowedBilateral(long userId, long peerUserId) =>
        await IsPhoneP2PAllowed(userId, peerUserId) &&
        await IsPhoneP2PAllowed(peerUserId, userId);

    private async Task<bool> EvaluateStoredRules(ICollection<TLPrivacyRule> rules,
        long ownerUserId, long otherUserId)
    {
        bool? userDecision = GetUserRuleDecision(rules, otherUserId);
        if (userDecision != null)
        {
            return userDecision.Value;
        }

        bool? chatDecision = await GetChatParticipantRuleDecision(rules, otherUserId);
        if (chatDecision != null)
        {
            return chatDecision.Value;
        }

        bool? contactDecision = GetContactRuleDecision(rules, ownerUserId, otherUserId);
        if (contactDecision != null)
        {
            return contactDecision.Value;
        }

        foreach (var rule in rules)
        {
            if (rule.Constructor == Constructors.baseLayer_PrivacyValueDisallowAll)
            {
                return false;
            }
        }

        foreach (var rule in rules)
        {
            if (rule.Constructor == Constructors.baseLayer_PrivacyValueAllowAll)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBlockedBy(long ownerUserId, long peerUserId)
    {
        foreach (var blockedValue in _blockedPeersRepository
                     .GetBlockedPeers(ownerUserId))
        {
            using (blockedValue)
            {
                var row = blockedValue.AsBlockedPeer();
                if (row.PeerType == (int)PeerType.User && row.PeerId == peerUserId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool? GetUserRuleDecision(ICollection<TLPrivacyRule> rules,
        long inviterUserId)
    {
        bool allowed = false;
        foreach (var rule in rules)
        {
            switch (rule.Constructor)
            {
                case Constructors.baseLayer_PrivacyValueDisallowUsers:
                {
                    var users = rule.AsPrivacyValueDisallowUsers().Users;
                    for (int i = 0; i < users.Count; i++)
                    {
                        if (users[i] == inviterUserId)
                        {
                            return false;
                        }
                    }

                    break;
                }
                case Constructors.baseLayer_PrivacyValueAllowUsers:
                {
                    var users = rule.AsPrivacyValueAllowUsers().Users;
                    for (int i = 0; i < users.Count; i++)
                    {
                        if (users[i] == inviterUserId)
                        {
                            allowed = true;
                            break;
                        }
                    }

                    break;
                }
            }
        }

        return allowed ? true : null;
    }

    private async Task<bool?> GetChatParticipantRuleDecision(
        ICollection<TLPrivacyRule> rules, long inviterUserId)
    {
        var disallowChatIds = new List<long>();
        var allowChatIds = new List<long>();
        foreach (var rule in rules)
        {
            switch (rule.Constructor)
            {
                case Constructors.baseLayer_PrivacyValueDisallowChatParticipants:
                {
                    var chats = rule.AsPrivacyValueDisallowChatParticipants().Chats;
                    for (int i = 0; i < chats.Count; i++)
                    {
                        disallowChatIds.Add(chats[i]);
                    }

                    break;
                }
                case Constructors.baseLayer_PrivacyValueAllowChatParticipants:
                {
                    var chats = rule.AsPrivacyValueAllowChatParticipants().Chats;
                    for (int i = 0; i < chats.Count; i++)
                    {
                        allowChatIds.Add(chats[i]);
                    }

                    break;
                }
            }
        }

        foreach (long chatId in disallowChatIds)
        {
            if (await IsActiveChatParticipantAsync(chatId, inviterUserId))
            {
                return false;
            }
        }

        foreach (long chatId in allowChatIds)
        {
            if (await IsActiveChatParticipantAsync(chatId, inviterUserId))
            {
                return true;
            }
        }

        return null;
    }

    private bool? GetContactRuleDecision(ICollection<TLPrivacyRule> rules,
        long inviteeUserId, long inviterUserId)
    {
        bool hasDisallowContacts = false;
        bool hasAllowContacts = false;
        foreach (var rule in rules)
        {
            if (rule.Constructor == Constructors.baseLayer_PrivacyValueDisallowContacts)
            {
                hasDisallowContacts = true;
            }
            else if (rule.Constructor == Constructors.baseLayer_PrivacyValueAllowContacts)
            {
                hasAllowContacts = true;
            }
        }

        if (!hasDisallowContacts && !hasAllowContacts)
        {
            return null;
        }

        if (!_contactsRepository.HasContact(inviteeUserId, inviterUserId))
        {
            return null;
        }

        return !hasDisallowContacts;
    }

    private async Task<bool> IsActiveChatParticipantAsync(long chatId, long userId)
    {
        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(chatId, userId);
        if (participant == null)
        {
            return false;
        }

        bool active = IsActiveParticipant(participant.Value);
        participant.Value.Dispose();
        return active;
    }

    private static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }
}
