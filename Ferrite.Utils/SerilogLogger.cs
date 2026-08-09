// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using Serilog;
using Serilog.Core;

namespace Ferrite.Utils;

public class SerilogLogger : ILogger
{
    private Logger log;
    public SerilogLogger()
    {
        //clear the log at the start of the application
        File.WriteAllText("ferrite.log", string.Empty);
        log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("ferrite.log")
            .CreateLogger();
    }

    public void Debug(string message)
    {
        Console.WriteLine(message);
        log.Debug(message);
    }

    public void Debug(Exception exception, string message)
    {
        Console.WriteLine(message);
        log.Debug(exception, message);
    }

    public void Error(string message)
    {
        Console.WriteLine(message);
        log.Error(message);
    }

    public void Error(Exception exception, string message)
    {
        Console.WriteLine(message);
        log.Error(exception, message);
    }

    public void Fatal(string message)
    {
        Console.WriteLine(message);
        log.Fatal(message);
    }

    public void Fatal(Exception exception, string message)
    {
        Console.WriteLine(message);
        log.Fatal(exception, message);
    }

    public void Information(string message)
    {
        Console.WriteLine(message);
        log.Information(message);
    }

    public void Information(Exception exception, string message)
    {
        Console.WriteLine(message);
        log.Information(exception, message);
    }

    public void Verbose(string message)
    {
        Console.WriteLine(message);
        log.Verbose(message);
    }

    public void Verbose(Exception exception, string message)
    {
        Console.WriteLine(message);
        log.Verbose(exception, message);
    }

    public void Warning(string message)
    {
        Console.WriteLine(message);
        log.Warning(message);
    }

    public void Warning(Exception exception, string message)
    {
        Console.WriteLine(message);
        log.Warning(exception, message);
    }
}


