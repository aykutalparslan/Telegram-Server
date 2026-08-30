// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public enum CallProtocolError
{
    None,
    Malformed,
    FlagsInvalid,
    LayerInvalid,
    VersionOutdated,
}

public static class CallProtocolNegotiator
{
    public const int ConnectionLayer = 92;

    public static readonly IReadOnlyList<string> ServerVersionOrder =
        new[] { "5.0.0", "2.7.7" };

    public static CallProtocolError ValidateOffer(CallProtocol offer,
        CallRegistryOptions options)
    {
        if (offer.LibraryVersions.Count == 0 ||
            offer.LibraryVersions.Count > options.MaxProtocolVersions)
        {
            return CallProtocolError.Malformed;
        }

        foreach (string version in offer.LibraryVersions)
        {
            if (string.IsNullOrEmpty(version) ||
                version.Length > options.MaxVersionStringLength)
            {
                return CallProtocolError.Malformed;
            }
        }

        if (!offer.UdpReflector)
        {
            return CallProtocolError.FlagsInvalid;
        }

        if (offer.MinLayer > offer.MaxLayer || offer.MinLayer > ConnectionLayer ||
            offer.MaxLayer < ConnectionLayer)
        {
            return CallProtocolError.LayerInvalid;
        }

        foreach (string version in ServerVersionOrder)
        {
            if (offer.LibraryVersions.Contains(version))
            {
                return CallProtocolError.None;
            }
        }

        return CallProtocolError.VersionOutdated;
    }

    public static (CallProtocol? Protocol, CallProtocolError Error) Negotiate(
        CallProtocol callerOffer, CallProtocol calleeOffer,
        CallRegistryOptions options)
    {
        CallProtocolError callerError = ValidateOffer(callerOffer, options);
        if (callerError != CallProtocolError.None)
        {
            return (null, callerError);
        }

        CallProtocolError calleeError = ValidateOffer(calleeOffer, options);
        if (calleeError != CallProtocolError.None)
        {
            return (null, calleeError);
        }

        int minLayer = Math.Max(callerOffer.MinLayer, calleeOffer.MinLayer);
        int maxLayer = Math.Min(callerOffer.MaxLayer, calleeOffer.MaxLayer);
        if (minLayer > maxLayer || minLayer > ConnectionLayer ||
            maxLayer < ConnectionLayer)
        {
            return (null, CallProtocolError.LayerInvalid);
        }

        foreach (string version in ServerVersionOrder)
        {
            if (callerOffer.LibraryVersions.Contains(version) &&
                calleeOffer.LibraryVersions.Contains(version))
            {
                return (new CallProtocol(
                    callerOffer.UdpP2p && calleeOffer.UdpP2p,
                    UdpReflector: true, minLayer, maxLayer,
                    new[] { version }), CallProtocolError.None);
            }
        }

        return (null, CallProtocolError.VersionOutdated);
    }
}
