using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace calender
{
    internal class networkHandler
    {
        private readonly string host;
        private readonly int port;
        private SslStream secureStream;
        private readonly Socket socket;
        private readonly CustomSocketStream customStream;

        public networkHandler(string host, int port)
        {
            this.host = host;
            this.port = port;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.socket.ReceiveTimeout = 5000; // Set receive timeout to 5 seconds
            this.socket.SendTimeout = 5000; // Set send timeout to 5 seconds
            this.socket.Connect(this.host, this.port);

            // Pass custom stream wrapper to SslStream
            this.customStream = new CustomSocketStream(socket, ownsSocket: true);
            this.secureStream = new SslStream(
                customStream,
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, cert, chain, errors) => true // Disable SSL verification (ssl.CERT_NONE)
            );

            // Perform TLS Handshake
            this.secureStream.AuthenticateAsClient(this.host);
        }

        public void SendRequest(byte[] requestBytes)
        {
            this.secureStream.Write(requestBytes, 0, requestBytes.Length);
            this.secureStream.Flush();
        }

        public async Task SendRequestAsync(byte[] requestBytes)
        {
            await this.secureStream.WriteAsync(requestBytes, 0, requestBytes.Length);
            await this.secureStream.FlushAsync();
        }

        public byte[] ReceiveResponse()
        {
            List<byte> responseBytes = new List<byte>();
            byte[] buffer = new byte[4096];
            int bytesRead;
            // Read until the stream is closed by the server
            while ((bytesRead = this.secureStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                responseBytes.AddRange(new ArraySegment<byte>(buffer, 0, bytesRead));
            }
            return responseBytes.ToArray();
        }

        public async Task<byte[]> ReceiveResponseAsync()
        {
            List<byte> responseBytes = new List<byte>();
            byte[] buffer = new byte[4096];
            int bytesRead;
            // Read until the stream is closed by the server
            while ((bytesRead = await this.secureStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                responseBytes.AddRange(new ArraySegment<byte>(buffer, 0, bytesRead));
            }
            return responseBytes.ToArray();
        }

        public void Close()
        {
            this.secureStream.Close();
            this.socket.Close();
        }
    }
}
