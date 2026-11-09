using System;
using System.Collections.Generic;
using System.Text;

namespace calender
{
    internal class httpHandler
    {
        public static byte[] BuildHttpRequest(string method, string path, Dictionary<string, string> headers, byte[]? body = null)
        {
            StringBuilder requestBuilder = new StringBuilder();
            // Start with the request line
            requestBuilder.AppendLine($"{method} {path} HTTP/1.1");
            // Add headers
            foreach (var header in headers)
            {
                requestBuilder.AppendLine($"{header.Key}: {header.Value}");
            }
            // Add an empty line to separate headers from the body
            requestBuilder.AppendLine();
            // Convert the header part to bytes
            byte[] headerBytes = Encoding.UTF8.GetBytes(requestBuilder.ToString());
            // Combine header and body into a single byte array
            byte[] requestBytes = new byte[headerBytes.Length + (body?.Length ?? 0)];
            Buffer.BlockCopy(headerBytes, 0, requestBytes, 0, headerBytes.Length);
            if (body != null)
            {
                Buffer.BlockCopy(body, 0, requestBytes, headerBytes.Length, body.Length);
            }
            return requestBytes;
        }
    }
}
