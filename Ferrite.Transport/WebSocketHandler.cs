// SPDX-License-Identifier: MIT
// Copyright (C) 2022-2026 Aykut Alparslan KOÇ
//
// Portions derived from ASP.NET Core:
//   Licensed to the .NET Foundation under one or more agreements.
//   The .NET Foundation licenses this file to you under the MIT license.
//   See LICENSE.aspnetcore.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using DotNext.Buffers;

namespace Ferrite.Transport
{
    public class WebSocketHandler : IHttpRequestLineHandler, IHttpHeadersHandler
    {
        public static int Get => 542393671;
        public IHeaderDictionary RequestHeaders { get; } = new HeaderDictionary();
        public bool RequestLineComplete { get; private set; }
        public bool HeadersComplete { get; private set; }
        public HttpVersion Version { get; private set; }

        //---
        // https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/WebSockets/src/HandshakeHelpers.cs
        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        // This uses C# compiler's ability to refer to static data directly. For more information see https://vcsjones.dev/2019/02/01/csharp-readonly-span-bytes-static
        private static ReadOnlySpan<byte> EncodedWebSocketKey => "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"u8;

        private static ReadOnlySpan<byte> CRLF => "\r\n"u8;
        private static ReadOnlySpan<byte> Separator => ": "u8;
        private static ReadOnlySpan<byte> Http11 => "HTTP/1.1 "u8;
        private static ReadOnlySpan<byte> Http2 => "HTTP/2 "u8;
        private static ReadOnlySpan<byte> Http3 => "HTTP/3 "u8;
        private static ReadOnlySpan<byte> Resp101 => "101 Switching Protocols\r\n"u8;
        private static ReadOnlySpan<byte> ConnectionUpgrade => "Connection: upgrade\r\n"u8;
        private static ReadOnlySpan<byte> UpgradeWebsocket => "Upgrade: websocket\r\n"u8;
        private static ReadOnlySpan<byte> WebSocketAccept => "Sec-WebSocket-Accept"u8;

        private static ReadOnlySpan<byte> WebSocketProtocol => "Sec-WebSocket-Protocol"u8;

        /// <summary>
        /// Validates the Sec-WebSocket-Key request header
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsRequestKeyValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            Span<byte> temp = stackalloc byte[16];
            var success = Convert.TryFromBase64String(value, temp, out var written);
            return success && written == 16;
        }
        
        public static void WriteResponseKeyTo(string requestKey, Span<byte> buff)
        {
            // "The value of this header field is constructed by concatenating /key/, defined above in step 4
            // in Section 4.2.2, with the string "258EAFA5-E914-47DA-95CA-C5AB0DC85B11", taking the SHA-1 hash of
            // this concatenated value to obtain a 20-byte value and base64-encoding"
            // https://tools.ietf.org/html/rfc6455#section-4.2.2

            // requestKey is already verified to be small (24 bytes) by 'IsRequestKeyValid()' and everything is 1:1 mapping to UTF8 bytes
            // so this can be hardcoded to 60 bytes for the requestKey + static websocket string
            Span<byte> mergedBytes = stackalloc byte[60];
            Encoding.UTF8.GetBytes(requestKey, mergedBytes);
            EncodedWebSocketKey.CopyTo(mergedBytes[24..]);

            Span<byte> hashedBytes = stackalloc byte[20];
            var written = SHA1.HashData(mergedBytes, hashedBytes);
            if (written != 20)
            {
                throw new InvalidOperationException("Could not compute the hash for the 'Sec-WebSocket-Accept' header.");
            }
            Base64.EncodeToUtf8(hashedBytes, buff, out int c, out int w);
        }
        //---

        private static bool HeaderContainsToken(string value, string expected)
        {
            ReadOnlySpan<char> remaining = value.AsSpan();
            while (!remaining.IsEmpty)
            {
                int separator = remaining.IndexOf(',');
                ReadOnlySpan<char> token = separator < 0
                    ? remaining
                    : remaining[..separator];

                if (token.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (separator < 0)
                {
                    break;
                }

                remaining = remaining[(separator + 1)..];
            }

            return false;
        }

        public ReadOnlySequence<byte> GenerateHandshakeResponse()
        {
            ArrayBufferWriter<byte> output = new();
            if (HeadersComplete && RequestLineComplete &&
                HeaderContainsToken(RequestHeaders.Connection.ToString(), "upgrade") &&
                HeaderContainsToken(RequestHeaders.Upgrade.ToString(), "websocket"))
            {
                var websocketKey = RequestHeaders.SecWebSocketKey.ToString();
                var protocol = RequestHeaders.SecWebSocketProtocol.ToString();
                if (IsRequestKeyValid(websocketKey))
                {
                    
                    if (Version == HttpVersion.Http11)
                    {
                        output.Write(Http11);
                    }
                    else if (Version == HttpVersion.Http2)
                    {
                        output.Write(Http2);
                    }
                    else if (Version == HttpVersion.Http3)
                    {
                        output.Write(Http3);
                    }
                    output.Write(Resp101);
                    output.Write(ConnectionUpgrade);
                    output.Write(UpgradeWebsocket);
                    output.Write(WebSocketAccept);
                    output.Write(Separator);
                    Span<byte> responseKey = stackalloc byte[28];
                    WriteResponseKeyTo(websocketKey, responseKey);
                    output.Write(responseKey);
                    output.Write(CRLF);
                    if (!string.IsNullOrWhiteSpace(protocol))
                    {
                        output.Write(WebSocketProtocol);
                        output.Write(Separator);
                        output.Write(Encoding.UTF8.GetBytes(protocol));
                        output.Write(CRLF);
                    }
                    output.Write(CRLF);
                }
            }

            return new ReadOnlySequence<byte>(output.WrittenMemory);
        }

        public static byte[] GenerateHeader(long length)
        {
            if (length <= 125)
            {
                var header = new byte[2];
                header[0] = 0b10000010;
                header[1] = (byte)length;
                return header;
            }
            else if (length <= 65535)
            {
                var header = new byte[4];
                header[0] = 0b10000010;
                header[1] = (byte)126;
                BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan()[2..], (ushort)length);
                return header;
            }
            else
            {
                var header = new byte[10];
                header[0] = 0b10000010;
                header[1] = (byte)127;
                BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan()[2..], (ulong)length);
                return header;
            }
        }

        // https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server#decoding_messages
        public SequencePosition DecodeTo(in ReadOnlySequence<byte> buff, PipeWriter output)
        {
            SequenceReader<byte> reader = new SequenceReader<byte>(buff);
            Span<byte> bytes = stackalloc byte[10];
            if (reader.Remaining < 2)
            {
                return reader.Position;
            }
            for (int i = 0; i <2; i++)
            {
                reader.TryRead(out var b);
                bytes[i] = b;
            }
            bool fin = (bytes[0] & 0b10000000) != 0,
                mask = (bytes[1] & 0b10000000) != 0;

            int opcode = bytes[0] & 0b00001111,
                msglen = bytes[1] - 128,
                offset = 2;

            if (msglen == 126)
            {
                if (reader.Remaining < 2)
                {
                    reader.Rewind(offset);
                    return reader.Position;
                }
                for (int i = 2; i < 4; i++)
                {
                    reader.TryRead(out var b);
                    bytes[i] = b;
                }
                msglen = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(2));
                offset = 4;
            }
            else if (msglen == 127)
            {
                if (reader.Remaining < 8)
                {
                    reader.Rewind(offset);
                    return reader.Position;
                }
                for (int i = 2; i < 10; i++)
                {
                    reader.TryRead(out byte b);
                    bytes[i] = b;
                }
                msglen = (int)BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(2));
                offset = 10;
            }
            if (msglen > reader.Remaining)
            {
                reader.Rewind(offset);
                return reader.Position;
            }
            if (mask)
            {
                Span<byte> masks = stackalloc byte[4];
                Span<byte> d = stackalloc byte[1];
                for (int i = 0; i < 4; i++)
                {
                    reader.TryRead(out var b);
                    masks[i] = b;
                }
                offset += 4;

                for (int i = 0; i < msglen; ++i)
                {
                    reader.TryRead(out byte b);
                    d[0] = (byte)(b ^ masks[i % 4]);
                    output.Write(d);
                }
            }
            
            return reader.Position;
        }

        public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            string key = name.GetHeaderName();
            var valueStr = value.GetRequestHeaderString(key, _ => Encoding.ASCII, checkForNewlineChars: false);
            RequestHeaders.Append(key, valueStr);
        }

        public void OnHeadersComplete(bool endStream)
        {
            HeadersComplete = true;
        }

        public void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
        {
            if (versionAndMethod.Method == HttpMethod.Get &&
                versionAndMethod.Version >= HttpVersion.Http11)
            {
                Version = versionAndMethod.Version;
                RequestLineComplete = true;
            }
            else
            {
                throw new Exception("Invalid request.");
            }
        }

        public void OnStaticIndexedHeader(int index)
        {
            throw new NotImplementedException();
        }

        public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value)
        {
            throw new NotImplementedException();
        }
    }
}
