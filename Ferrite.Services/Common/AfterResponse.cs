// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Common;

public sealed class AfterResponse
{
    private static readonly AsyncLocal<AfterResponse?> Current = new();

    private readonly List<Func<ValueTask>> _work = new();
    private bool _completed;

    public static AfterResponse Begin()
    {
        var scope = new AfterResponse();
        Current.Value = scope;
        return scope;
    }

    public static ValueTask Run(Func<ValueTask> work)
    {
        if (Current.Value is { } scope)
        {
            lock (scope._work)
            {
                if (!scope._completed)
                {
                    scope._work.Add(work);
                    return ValueTask.CompletedTask;
                }
            }
        }
        return work();
    }

    public async ValueTask CompleteAsync()
    {
        List<Exception>? failures = null;
        while (true)
        {
            Func<ValueTask>[] work;
            lock (_work)
            {
                if (_work.Count == 0)
                {
                    _completed = true;
                    break;
                }
                work = _work.ToArray();
                _work.Clear();
            }
            foreach (Func<ValueTask> item in work)
            {
                try
                {
                    await item();
                }
                catch (Exception exception)
                {
                    (failures ??= new()).Add(exception);
                }
            }
        }
        if (failures is not null)
        {
            throw new AggregateException(failures);
        }
    }
}
