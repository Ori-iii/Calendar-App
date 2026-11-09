using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

// 1. Inherit from System.IO.Stream
namespace calender
{
    public class CustomSocketStream : Stream
    {
        private readonly Socket _socket;
        private bool _ownsSocket;

        public CustomSocketStream(Socket socket, bool ownsSocket = true)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _ownsSocket = ownsSocket;
        }

        // --- Core Transport Methods required by SslStream ---

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Read directly from the raw socket into the buffer provided by SslStream
            return _socket.Receive(buffer, offset, count, SocketFlags.None);
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            // Read directly from the raw socket into the buffer provided by SslStream
            return await _socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), SocketFlags.None);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Send directly from the buffer provided by SslStream to the raw socket
            int sent = 0;
            while (sent < count)
            {
                sent += _socket.Send(buffer, offset + sent, count - sent, SocketFlags.None);
            }
        }

        public async override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            // Send directly from the buffer provided by SslStream to the raw socket
            int sent = 0;
            while (sent < count)
            {
                sent += await _socket.SendAsync(new ArraySegment<byte>(buffer, offset + sent, count - sent), SocketFlags.None);
            }
        }

        public override void Flush()
        {
            // Socket operations are immediate; no buffering needed.
        }

        // --- Stream Properties required by SslStream ---

        public override bool CanRead => _socket.Connected;
        public override bool CanWrite => _socket.Connected;
        public override bool CanSeek => false; // Sockets are non-seekable streams

        // --- Non-Seekable Stream Defaults ---

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        // --- Cleanup ---

        protected override void Dispose(bool disposing)
        {
            if (disposing && _ownsSocket)
            {
                _socket.Close();
                _socket.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
