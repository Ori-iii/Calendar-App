import socket
import ssl
import json
import DatabaseHandler
import HttpHandler
"""
HOST = "127.0.0.1"
PORT = 8443  # Match your server's listening port

# Prepare login payload
payload = {
    "username": "rolltherick",
    "password": "y@C12345",
    "display_name": "rick"
}
body_bytes = json.dumps(payload).encode("utf-8")

# Build raw HTTP request with CRLF (\r\n) delimiters
http_headers = (
    f"POST /signup HTTP/1.1\r\n"
    f"Host: {HOST}:{PORT}\r\n"
    f"Content-Type: application/json\r\n"
    f"Content-Length: {len(body_bytes)}\r\n"
    f"Connection: close\r\n"
    f"\r\n"
).encode("utf-8")

raw_request = http_headers + body_bytes

# Configure SSL context for self-signed certificate handling
context = ssl.create_default_context()
context.check_hostname = False
context.verify_mode = ssl.CERT_NONE

# Establish TLS socket connection and send request
raw_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
with context.wrap_socket(raw_socket, server_hostname=HOST) as secure_socket:
    secure_socket.connect((HOST, PORT))
    secure_socket.sendall(raw_request)

    # Read server response
    response_bytes = b""
    while True:
        chunk = secure_socket.recv(4096)
        if not chunk:
            break
        response_bytes += chunk

print("--- Raw Server Response ---")
print(response_bytes.decode("utf-8", errors="ignore"))
"""
DRIVER_NAME = 'ODBC Driver 17 for SQL Server'
SERVER_NAME = 'localhost'
DATABASE_NAME = 'calenderDB'

connectionString = (
    f"DRIVER={{{DRIVER_NAME}}};"
    f"SERVER={SERVER_NAME};"
    f"DATABASE={DATABASE_NAME};"
    f"Trusted_Connection=yes;"
    f"TrustServerCertificate=yes;"
)

database = DatabaseHandler.DatabaseHandler(connectionString)

print(DatabaseHandler.hash_password("yuval"))
