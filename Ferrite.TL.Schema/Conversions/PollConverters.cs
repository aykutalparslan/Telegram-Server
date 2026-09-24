// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

internal static class PollConverters
{
    internal static LayerObjectValue UpgradeCorrectAnswers(LayerConversionContext context,
        LayerObjectValue source)
    {
        LayerObjectValue poll = source.Get<LayerObjectValue>("poll");
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["correct_answers"] = CorrectAnswers(source, poll)
        };
        return context.ConvertCompatible(source, "inputMediaPoll", replacements);
    }

    internal static LayerObjectValue UpgradeQuestion(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["question"] = TextConverters.WithEntities(context, source, "question")
        };
        return context.ConvertCompatible(source, "poll", replacements);
    }

    internal static LayerObjectValue UpgradeHash(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["hash"] = 0L
        };
        return context.ConvertCompatible(source, "poll", replacements);
    }

    internal static LayerObjectValue UpgradeAnswer(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["text"] = TextConverters.WithEntities(context, source, "text")
        };
        return context.ConvertCompatible(source, "pollAnswer", replacements);
    }

    private static object? CorrectAnswers(LayerObjectValue source,
        LayerObjectValue poll)
    {
        if (!source.TryGetValue("correct_answers", out object? value) ||
            value is not IReadOnlyList<object?> answers)
        {
            return null;
        }
        if (!poll.TryGetValue("answers", out object? options) ||
            options is not IReadOnlyList<object?> pollAnswers)
        {
            throw new InvalidOperationException("The poll has no answers.");
        }
        var indexes = new object?[answers.Count];
        for (int index = 0; index < answers.Count; index++)
        {
            if (answers[index] is not byte[] option)
            {
                throw new InvalidOperationException("A correct answer is malformed.");
            }
            indexes[index] = IndexOfOption(pollAnswers, option);
        }
        return Array.AsReadOnly(indexes);
    }

    private static int IndexOfOption(IReadOnlyList<object?> answers, byte[] option)
    {
        for (int index = 0; index < answers.Count; index++)
        {
            if (answers[index] is LayerObjectValue answer &&
                answer.TryGetValue("option", out object? value) &&
                value is byte[] bytes && bytes.SequenceEqual(option))
            {
                return index;
            }
        }
        return -1;
    }

    internal static LayerObjectValue DowngradeVotesList(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["votes"] = context.ConvertElements(source, "votes", DowngradeVote)
        };
        return context.ConvertCompatible(source, "messages.votesList",
            replacements);
    }

    private static LayerObjectValue DowngradeVote(LayerConversionContext context,
        LayerObjectValue source)
    {
        string target = source.Constructor.Name switch
        {
            "messagePeerVote" => "messageUserVote",
            "messagePeerVoteInputOption" => "messageUserVoteInputOption",
            "messagePeerVoteMultiple" => "messageUserVoteMultiple",
            _ => throw new InvalidOperationException("The poll vote " +
                source.Constructor.Name + " has no published form.")
        };
        source.TryGetValue("peer", out object? peer);
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = UserIdentifier(peer)
        };
        return context.ConvertCompatible(source, target, replacements);
    }

    internal static LayerObjectValue DowngradePoll(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["question"] = TextConverters.PlainText(source, "question")
        };
        return context.ConvertCompatible(source, "poll", replacements);
    }

    internal static LayerObjectValue DowngradeAnswer(LayerConversionContext context,
        LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["text"] = TextConverters.PlainText(source, "text")
        };
        return context.ConvertCompatible(source, "pollAnswer", replacements);
    }

    internal static LayerObjectValue DowngradeResults(
        LayerConversionContext context, LayerObjectValue source)
    {
        var replacements = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source.TryGetValue("recent_voters", out object? value))
        {
            if (value is not IReadOnlyList<object?> peers)
            {
                throw new InvalidOperationException(
                    "The recent poll voters are malformed.");
            }
            var voters = new object?[peers.Count];
            for (int index = 0; index < peers.Count; index++)
            {
                voters[index] = UserIdentifier(peers[index]);
            }
            replacements.Add("recent_voters", Array.AsReadOnly(voters));
        }
        return context.ConvertCompatible(source, "pollResults", replacements);
    }

    private static long UserIdentifier(object? value)
    {
        if (value is not LayerObjectValue peer ||
            peer.Constructor.Name != "peerUser" ||
            !peer.TryGetValue("user_id", out object? identifier) ||
            identifier is not long userId)
        {
            throw new InvalidOperationException(
                "The peer has no published user identifier.");
        }
        return userId;
    }
}
