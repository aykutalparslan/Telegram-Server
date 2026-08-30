// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Primitives;

namespace Ferrite.Transport;

/// <summary>
/// Represents HttpRequest and HttpResponse headers
/// </summary>
public interface IHeaderDictionary : IDictionary<string, StringValues>
{
    /// <summary>
    /// IHeaderDictionary has a different indexer contract than IDictionary, where it will return StringValues.Empty for missing entries.
    /// </summary>
    /// <param name="key"></param>
    /// <returns>The stored value, or StringValues.Empty if the key is not present.</returns>
    new StringValues this[string key] { get; set; }

    /// <summary>Gets or sets the <c>Connection</c> HTTP header.</summary>
    StringValues Connection { get => this[HeaderNames.Connection]; set => this[HeaderNames.Connection] = value; }

    /// <summary>Gets or sets the <c>Date</c> HTTP header.</summary>
    StringValues Date { get => this[HeaderNames.Date]; set => this[HeaderNames.Date] = value; }
  
    /// <summary>Gets or sets the <c>Origin</c> HTTP header.</summary>
    StringValues Origin { get => this[HeaderNames.Origin]; set => this[HeaderNames.Origin] = value; }
    
    /// <summary>Gets or sets the <c>Sec-WebSocket-Accept</c> HTTP header.</summary>
    StringValues SecWebSocketAccept { get => this[HeaderNames.SecWebSocketAccept]; set => this[HeaderNames.SecWebSocketAccept] = value; }

    /// <summary>Gets or sets the <c>Sec-WebSocket-Key</c> HTTP header.</summary>
    StringValues SecWebSocketKey { get => this[HeaderNames.SecWebSocketKey]; set => this[HeaderNames.SecWebSocketKey] = value; }

    /// <summary>Gets or sets the <c>Sec-WebSocket-Protocol</c> HTTP header.</summary>
    StringValues SecWebSocketProtocol { get => this[HeaderNames.SecWebSocketProtocol]; set => this[HeaderNames.SecWebSocketProtocol] = value; }

    /// <summary>Gets or sets the <c>Sec-WebSocket-Version</c> HTTP header.</summary>
    StringValues SecWebSocketVersion { get => this[HeaderNames.SecWebSocketVersion]; set => this[HeaderNames.SecWebSocketVersion] = value; }

    /// <summary>Gets or sets the <c>Sec-WebSocket-Extensions</c> HTTP header.</summary>
    StringValues SecWebSocketExtensions { get => this[HeaderNames.SecWebSocketExtensions]; set => this[HeaderNames.SecWebSocketExtensions] = value; }

    /// <summary>Gets or sets the <c>Server</c> HTTP header.</summary>
    StringValues Server { get => this[HeaderNames.Server]; set => this[HeaderNames.Server] = value; }

    /// <summary>Gets or sets the <c>Upgrade</c> HTTP header.</summary>
    StringValues Upgrade { get => this[HeaderNames.Upgrade]; set => this[HeaderNames.Upgrade] = value; }

    /// <summary>Gets or sets the <c>Upgrade-Insecure-Requests</c> HTTP header.</summary>
    StringValues UpgradeInsecureRequests { get => this[HeaderNames.UpgradeInsecureRequests]; set => this[HeaderNames.UpgradeInsecureRequests] = value; }

    /// <summary>Gets or sets the <c>User-Agent</c> HTTP header.</summary>
    StringValues UserAgent { get => this[HeaderNames.UserAgent]; set => this[HeaderNames.UserAgent] = value; }

    /// <summary>Gets or sets the <c>Sec-WebSocket-Protocol</c> HTTP header.</summary>
    StringValues WebSocketSubProtocols { get => this[HeaderNames.WebSocketSubProtocols]; set => this[HeaderNames.WebSocketSubProtocols] = value; }
}
