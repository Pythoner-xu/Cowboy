﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    // http://tools.ietf.org/html/rfc6455
    // This wire format for the data transfer part is described by the ABNF
    // [RFC5234] given in detail in this section.  (Note that, unlike in
    // other sections of this document, the ABNF in this section is
    // operating on groups of bits.  The length of each group of bits is
    // indicated in a comment.  When encoded on the wire, the most
    // significant bit is the leftmost in the ABNF).  A high-level overview
    // of the framing is given in the following figure.  In a case of
    // conflict between the figure below and the ABNF specified later in
    // this section, the figure is authoritative.
    //  0                   1                   2                   3
    //  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    // +-+-+-+-+-------+-+-------------+-------------------------------+
    // |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
    // |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
    // |N|V|V|V|       |S|             |   (if payload len==126/127)   |
    // | |1|2|3|       |K|             |                               |
    // +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
    // |     Extended payload length continued, if payload len == 127  |
    // + - - - - - - - - - - - - - - - +-------------------------------+
    // |                               |Masking-key, if MASK set to 1  |
    // +-------------------------------+-------------------------------+
    // | Masking-key (continued)       |          Payload Data         |
    // +-------------------------------- - - - - - - - - - - - - - - - +
    // :                     Payload Data continued ...                :
    // + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
    // |                     Payload Data continued ...                |
    // +---------------------------------------------------------------+
    public abstract class Frame
    {
        private static readonly Random _rng = new Random(DateTime.UtcNow.Millisecond);

        public static byte[] Encode(int opCode, byte[] playload, int offset, int count, bool isFinal = true)
        {
            byte[] fragment;
            int maskingKeyLength = 4;

            // Payload length:  7 bits, 7+16 bits, or 7+64 bits.
            // The length of the "Payload data", in bytes: if 0-125, that is the
            // payload length.  If 126, the following 2 bytes interpreted as a
            // 16-bit unsigned integer are the payload length.  If 127, the
            // following 8 bytes interpreted as a 64-bit unsigned integer (the
            // most significant bit MUST be 0) are the payload length.
            if (count < 126)
            {
                fragment = new byte[2 + maskingKeyLength + count];
                fragment[1] = (byte)count;
            }
            else if (count < 65536)
            {
                fragment = new byte[2 + 2 + maskingKeyLength + count];
                fragment[1] = (byte)126;
                fragment[2] = (byte)(count / 256);
                fragment[3] = (byte)(count % 256);
            }
            else
            {
                fragment = new byte[2 + 8 + maskingKeyLength + count];
                fragment[1] = (byte)127;

                int left = count;
                int unit = 256;

                for (int i = 9; i > 1; i--)
                {
                    fragment[i] = (byte)(left % unit);
                    left = left / unit;

                    if (left == 0)
                        break;
                }
            }

            // FIN:  1 bit
            // Indicates that this is the final fragment in a message.  The first
            // fragment MAY also be the final fragment.

            // Opcode:  4 bits
            // Defines the interpretation of the "Payload data".  If an unknown
            // opcode is received, the receiving endpoint MUST _Fail the
            // WebSocket Connection_.  The following values are defined.
            // *  %x0 denotes a continuation frame
            // *  %x1 denotes a text frame
            // *  %x2 denotes a binary frame
            // *  %x3-7 are reserved for further non-control frames
            // *  %x8 denotes a connection close
            // *  %x9 denotes a ping
            // *  %xA denotes a pong
            // *  %xB-F are reserved for further control frames
            if (isFinal)
                fragment[0] = (byte)(opCode | 0x80);
            else
                fragment[0] = (byte)(opCode);

            // Mask:  1 bit
            // Defines whether the "Payload data" is masked.  If set to 1, a
            // masking key is present in masking-key, and this is used to unmask
            // the "Payload data" as per Section 5.3.  All frames sent from
            // client to server have this bit set to 1.
            fragment[1] = (byte)(fragment[1] | 0x80);

            // Masking-key:  0 or 4 bytes
            // All frames sent from the client to the server are masked by a
            // 32-bit value that is contained within the frame.
            // The masking key is a 32-bit value chosen at random by the client.
            // When preparing a masked frame, the client MUST pick a fresh masking
            // key from the set of allowed 32-bit values.  The masking key needs to
            // be unpredictable; thus, the masking key MUST be derived from a strong
            // source of entropy, and the masking key for a given frame MUST NOT
            // make it simple for a server/proxy to predict the masking key for a
            // subsequent frame.  The unpredictability of the masking key is
            // essential to prevent authors of malicious applications from selecting
            // the bytes that appear on the wire.  RFC 4086 [RFC4086] discusses what
            // entails a suitable source of entropy for security-sensitive applications.
            int maskingKeyIndex = fragment.Length - (maskingKeyLength + count);
            for (var i = maskingKeyIndex; i < maskingKeyIndex + maskingKeyLength; i++)
            {
                fragment[i] = (byte)_rng.Next(0, 255);
            }
            if (count > 0)
            {
                int payloadIndex = fragment.Length - count;
                for (var i = 0; i < count; i++)
                {
                    fragment[payloadIndex + i] = (byte)(playload[offset + i] ^ fragment[maskingKeyIndex + i % 4]);
                }
            }

            return fragment;
        }
    }
}