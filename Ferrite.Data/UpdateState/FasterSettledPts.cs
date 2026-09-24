// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using FASTER.core;
using Ferrite.Data.Primitives;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.UpdateState;

internal sealed class FasterSettledPts(FasterContext<string, byte[]> context, long userId)
{
    public async ValueTask Settle(int first, int last)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        await Update(new Mutation(first, last, 1));
    }

    public ValueTask<int> Extend(int committed) => Update(new Mutation(0, 0, committed));

    private async ValueTask<int> Update(Mutation mutation)
    {
        using var session = context.Store.NewSession(new Functions());
        var result = await session.RMWAsync($"updates:settled-pts:{userId}", mutation);
        while (result.Status.IsPending)
        {
            result = await result.CompleteAsync();
        }
        if (result.Status.IsFaulted)
        {
            throw new InvalidOperationException("Could not update settled pts");
        }
        return result.Output;
    }

    private readonly record struct Mutation(int First, int Last, int Committed);

    private sealed class Functions : FunctionsBase<string, byte[], Mutation, int, Empty>
    {
        private static byte[] Apply(byte[]? bytes, Mutation input, out int committed)
        {
            committed = Math.Max(1, input.Committed);
            var ranges = new List<(int First, int Last)>();
            if (bytes != null)
            {
                var state = new SettledPts(bytes);
                committed = Math.Max(committed, state.Committed);
                var stored = state.Ranges;
                for (int i = 0; i < stored.Count; i++)
                {
                    var range = new PtsRange(stored.ReadTLObject());
                    ranges.Add((range.First, range.Last));
                }
            }
            if (input.First > 0)
            {
                ranges.Add((input.First, input.Last));
            }
            ranges.Sort();
            var pending = new List<(int First, int Last)>();
            foreach (var range in ranges)
            {
                if (range.First <= (long)committed + 1)
                {
                    committed = Math.Max(committed, range.Last);
                }
                else if (pending.Count > 0 && range.First <= (long)pending[^1].Last + 1)
                {
                    pending[^1] = (pending[^1].First, Math.Max(pending[^1].Last, range.Last));
                }
                else
                {
                    pending.Add(range);
                }
            }
            var vector = new Vector();
            foreach (var range in pending)
            {
                using TLPtsRange row = PtsRange.Builder().First(range.First).Last(range.Last).Build();
                vector.AppendTLObject(row.AsSpan());
            }
            using TLSettledPts updated = SettledPts.Builder().Committed(committed).Ranges(vector).Build();
            return updated.AsSpan().ToArray();
        }

        public override bool InitialUpdater(ref string key, ref Mutation input,
            ref byte[] value, ref int output, ref RMWInfo rmwInfo)
        {
            value = Apply(null, input, out output);
            return true;
        }

        public override bool CopyUpdater(ref string key, ref Mutation input,
            ref byte[] oldValue, ref byte[] newValue, ref int output, ref RMWInfo rmwInfo)
        {
            newValue = Apply(oldValue, input, out output);
            return true;
        }

        public override bool InPlaceUpdater(ref string key, ref Mutation input,
            ref byte[] value, ref int output, ref RMWInfo rmwInfo)
        {
            value = Apply(value, input, out output);
            return true;
        }
    }
}
