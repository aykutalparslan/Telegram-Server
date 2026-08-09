// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
namespace Ferrite.Utils;

public interface ILogger
{
    public void Verbose(string message);
    public void Debug(string message);
    public void Information(string message);
    public void Warning(string message);
    public void Error(string message);
    public void Fatal(string message);
    public void Verbose(Exception exception, string message);
    public void Debug(Exception exception, string message);
    public void Information(Exception exception, string message);
    public void Warning(Exception exception, string message);
    public void Error(Exception exception, string message);
    public void Fatal(Exception exception, string message);
}


