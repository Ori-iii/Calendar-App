import ssl
import socket
import NetworkHandler
from DatabaseHandler import DatabaseHandler
import threading

server = "localhost"
db = "calenderDB"
driver = "ODBC Driver 18 for SQL Server"

# Connection string formatted with an f-string
# Note: {{ and }} escape the curly braces needed around the driver name in ODBC
connectionString = (f"Driver={driver};"
                     f"Server={server};"
                     f"Database={db};"
                     f"Trusted_Connection=yes;"
                     f"TrustServerCertificate=yes;"
                     f"")


def handle_client(client_sock, client_addr, database):
    try:
        # 3. Initialize your NetworkHandler class wrapping the client socket
        handler = NetworkHandler.NetworkHandler(
            server_socket=client_sock,
            database=database,
            certfile="cert.pem",
            keyfile="key.pem"
        )

        # 4. Handle the secure communication
        handler.handle_connection()

    except ssl.SSLError as e:
        print(f"SSL Handshake failed with {client_addr}: {e}")
    except Exception as e:
        print(f"Error handling client {client_addr}: {e}")
    finally:
        # Always ensure the socket closes when the thread finishes
        client_sock.close()


def main():
    HOST = "0.0.0.0"
    PORT = 8443

    database = DatabaseHandler(connectionString)

    # 1. Create a raw TCP listener socket
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_socket.bind((HOST, PORT))
    server_socket.listen(5)

    print(f"Server listening on https://{HOST}:{PORT}...")

    try:
        while True:
            # 2. Wait for a client connection
            client_sock, client_addr = server_socket.accept()
            print(f"Accepted connection from {client_addr}")

            # Note: For production use, you'd typically want a database cursor
            # initialized per connection or thread. Passing dummy cursor for now.

            try:
                client_thread = threading.Thread(
                    target=handle_client,
                    args=(client_sock, client_addr, database)
                )
                client_thread.daemon = True
                client_thread.start()

            except ssl.SSLError as e:
                print(f"SSL Handshake failed with {client_addr}: {e}")
                client_sock.close()
            except Exception as e:
                print(f"Error handling client {client_addr}: {e}")
                client_sock.close()

    except KeyboardInterrupt:
        print("\nShutting down server...")
    finally:
        server_socket.close()


if __name__ == '__main__':
    main()
