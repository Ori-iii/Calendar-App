import json
import ssl
import DatabaseHandler
import HttpHandler
import string


class NetworkHandler:
    def __init__(self, server_socket, database, certfile="cert.pem", keyfile="key.pem"):
        context = ssl.create_default_context(ssl.Purpose.CLIENT_AUTH)
        context.load_cert_chain(certfile=certfile, keyfile=keyfile)
        self.secure_socket = context.wrap_socket(server_socket, server_side=True)
        self.database = database

    def __recv_http_request(self, buffer_size=4096):  # receives and returns a http msg from a client
        chunks = []
        received_length = 0
        header_end_index = -1

        while header_end_index == -1:
            chunk = self.secure_socket.recv(buffer_size)
            if not chunk:
                break
            chunks.append(chunk)
            received_length += len(chunk)

            header_end_index = b"".join(chunks).find(b"\r\n\r\n")

        temp = b"".join(chunks)

        if header_end_index == -1:
            return temp

        header_part = temp[:header_end_index]
        body_start_index = header_end_index + 4
        content_length = 0
        lines = header_part.split(b"\r\n")
        for line_bytes in lines[1:]:
            line = line_bytes.decode("utf-8", errors="ignore")
            if line.lower().startswith("content-length:"):
                content_length = int(line.split(":")[1].strip())
                break

        current_body_length = received_length - body_start_index
        while current_body_length < content_length:
            remaining_bytes = content_length - current_body_length
            chunk = self.secure_socket.recv(min(buffer_size, remaining_bytes))
            if not chunk:
                break
            chunks.append(chunk)
            current_body_length += len(chunk)

        return b"".join(chunks)

    # hanlde POST /login request from a client
    def handle_login(self, http_request):
        variables = http_request.data.decode("utf-8", errors="ignore")
        variables = json.loads(variables)
        user = self.database.get_user_by_username(variables["username"])
        if user and DatabaseHandler.verify_password(variables["password"], self.database.get_password_from_ID(user.ID)):
            user_dict = user.to_dict()
            user_json = json.dumps(user_dict)
            return HttpHandler.response_phrase(200, "HTTP/1.1", {"content-type": "application/json",
                                                                 "content-length": len(user_json.encode())}, user_json)
        else:
            msg = "Incorrect credentials"
            return HttpHandler.response_phrase(401, "HTTP/1.1", {"content-type": "text/html",
                                                                 "content-length": len(msg.encode())}, msg)

    def handle_signup(self, http_request):
        variables = http_request.data.decode("utf-8", errors="ignore")
        variables = json.loads(variables)
        password = variables["password"]
        username = variables["username"]
        display_name = variables["display_name"]
        rules = [
            len(password) >= 8,
            len(password) <= 24,
            any(c.islower() for c in password),
            any(c.isupper() for c in password),
            any(c.isdigit() for c in password),
            any(c in string.punctuation for c in password),
        ]

        if not all(rules):
            msg = ("Password does not meet requirements:\r\n1 special character\r\n1 Uppercase\r\n1 lowercase\r\n1 "
                   "digit\r\nlength of 8 to 24")
            return HttpHandler.response_phrase(422, "HTTP/1.1", {"content-type": "text/html",
                                                                 "content-length": len(msg.encode())}, msg)
        else:
            user = self.database.create_user(username, password, display_name)
            if user:
                user_dict = user.to_dict()
                user_json = json.dumps(user_dict)

                return HttpHandler.response_phrase(200, "HTTP/1.1", {"content-type": "application/json",
                                                                     "content-length": len(user_json.encode())},
                                                   user_json)
            else:
                msg = "username already exists"
                return HttpHandler.response_phrase(409, "HTTP/1.1", {"content-type": "text/html",
                                                                     "content-length": len(msg.encode())}, msg)

    def handle_add_event(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        event = self.database.add_event(variables["title"], variables["start_time"],
                                        variables["end_time"], variables["owner_id"], variables.get("group_id"),)
        print(variables.get("group_id"))
        if event:
            event_dict = event.to_dict()
            event_json = json.dumps(event_dict)
            return HttpHandler.response_phrase(200, "HTTP/1.1", {"content-type": "application/json",
                                                                 "content-length": len(event_json.encode())},
                                               event_json)
        else:
            return HttpHandler.response_phrase(400, "HTTP/1.1")

    def handle_share_event(self, http_request):
        print(http_request.data.decode("utf-8", errors="ignore"))
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        worked = self.database.share_event(variables["event_id"], variables["owner_id"],
                                           variables["username"], variables["access_level"])
        if worked:
            return HttpHandler.response_phrase(200, "HTTP/1.1")
        else:
            return HttpHandler.response_phrase(403, "HTTP/1.1")

    def handle_create_group(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        group = self.database.create_group(variables["group_name"], variables["creator_id"], variables["members"])
        if group == 0:
            msg = "no users found"
            return HttpHandler.response_phrase(404, "HTTP/1.1", {"content-type": "text/html",
                                                                 "content-length": len(msg.encode())}, msg)
        if group:
            group_dict = group.to_dict()
            group_json = json.dumps(group_dict)
            return HttpHandler.response_phrase(200, "HTTP/1.1",
                                               {"content-type": "application/json",
                                                "content-length": len(group_json.encode())}, group_json)
        else:
            return HttpHandler.response_phrase(400, "HTTP/1.1")

    def check_update(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        user_id = variables.get("ID")
        last_checked_time = variables.get("last_checked_time")

        # Call the database method to fetch new events and groups since the timestamp
        updates = self.database.check_user_updates(user_id, last_checked_time)

        if updates is not None:
            # Convert the result dictionary (events and groups) into a JSON response
            update_dict = {
                "events": [event.to_dict() for event in updates["events"]],
                "groups": [group.to_dict() for group in updates["groups"]]
            }
            update_json = json.dumps(update_dict)
            return HttpHandler.response_phrase(
                200, "HTTP/1.1",
                {"content-type": "application/json", "content-length": len(update_json.encode())},
                update_json
            )
        else:
            msg = "Failed to fetch updates"
            return HttpHandler.response_phrase(
                400, "HTTP/1.1",
                {"content-type": "text/html", "content-length": len(msg.encode())},
                msg
            )

    def delete_event(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        deleted = self.database.delete_event(variables["event_id"], variables["owner_id"])
        if deleted:
            return HttpHandler.response_phrase(200, "HTTP/1.1")
        else:
            return HttpHandler.response_phrase(403, "HTTP/1.1")

    def add_members(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        members = self.database.add_members(variables["members"], variables["admin_id"], variables["group_id"])
        if members:
            members_dict = [m.to_dict() for m in members]
            members_json = json.dumps(members_dict)
            return HttpHandler.response_phrase(200, "HTTP/1.1", {"content-type": "application/json",
                                                                 "content-length": len(members_json.encode())},
                                               members_json)
        else:
            return HttpHandler.response_phrase(403, "HTTP/1.1")

    def delete_group(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        deleted = self.database.delete_group(variables["group_id"], variables["owner_id"])
        if deleted:
            return HttpHandler.response_phrase(200, "HTTP/1.1")
        else:
            return HttpHandler.response_phrase(403, "HTTP/1.1")

    def delete_member(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        deleted = self.database.delete_member(variables["owner_id"], variables["group_id"], variables["member"])
        if deleted:
            return HttpHandler.response_phrase(200, "HTTP/1.1")
        else:
            return HttpHandler.response_phrase(404, "HTTP/1.1")

    def leave_group(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        deleted = self.database.delete_group_member(variables["group_id"], variables["member_id"])
        if deleted:
            return HttpHandler.response_phrase(200, "HTTP/1.1")
        else:
            return HttpHandler.response_phrase(400, "HTTP/1.1")

    def delete_user(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        deleted = self.database.delete_user(variables["user_id"])
        if deleted:
            return HttpHandler.response_phrase(200, "HTTP/1.1")
        else:
            return HttpHandler.response_phrase(404, "HTTP/1.1")

    def handle_add_members(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        result = self.database.add_members(variables["members"], variables["admin_id"], variables["group_id"])

        if result == "already_member":
            msg = "User is already a member of the group"
            return HttpHandler.response_phrase(400, "HTTP/1.1",
                                               {"content-type": "text/html", "content-length": len(msg.encode())}, msg)
        elif result == "user_not_found":
            msg = "User does not exist"
            return HttpHandler.response_phrase(400, "HTTP/1.1",
                                               {"content-type": "text/html", "content-length": len(msg.encode())}, msg)
        elif result:
            members_json = json.dumps([m.to_dict() for m in result])
            return HttpHandler.response_phrase(200, "HTTP/1.1", {"content-type": "application/json",
                                                                 "content-length": len(members_json.encode())},
                                               members_json)
        else:
            return HttpHandler.response_phrase(403, "HTTP/1.1")

    def handle_leave_group(self, http_request):
        variables = json.loads(http_request.data.decode("utf-8", errors="ignore"))
        # variables should contain: group_id, user_id
        worked = self.database.delete_group_member(variables["group_id"], variables["user_id"])
        if worked:
            return HttpHandler.response_phrase(200, "HTTP/1.1")
        else:
            return HttpHandler.response_phrase(400, "HTTP/1.1")

    def handle_POST_request(self, http_request):  # handle all POST request the server can manage
        if http_request.path.startswith("/login"):
            return self.handle_login(http_request)

        elif http_request.path.startswith("/signup"):
            return self.handle_signup(http_request)

        elif http_request.path.startswith("/add-event"):
            return self.handle_add_event(http_request)

        elif http_request.path.startswith("/share-event"):
            return self.handle_share_event(http_request)
        elif http_request.path.startswith("/create-group"):
            return self.handle_create_group(http_request)
        elif http_request.path.startswith("/delete-event"):
            return self.delete_event(http_request)
        elif http_request.path.startswith("/add-members"):
            return self.add_members(http_request)
        else:
            return HttpHandler.response_phrase(404, "HTTP/1.1")

    def handle_GET_request(self, http_request):
        if http_request.path.startswith("/check-update"):
            return self.check_update(http_request)
        else:
            return HttpHandler.response_phrase(400, "HTTP/1.1")

    def handle_DELETE_request(self, http_request):
        if http_request.path.startswith("/delete-event"):
            return self.delete_event(http_request)
        elif http_request.path.startswith("/delete-group"):
            return self.delete_group(http_request)
        elif http_request.path.startswith("/delete-member"):
            return self.delete_member(http_request)
        elif http_request.path.startswith("/leave-group"):
            return self.leave_group(http_request)
        elif http_request.path.startswith("/delete-user"):
            return self.delete_user(http_request)
        else:
            return HttpHandler.response_phrase(400, "HTTP/1.1")

    def handle_connection(self):
        request_data = self.__recv_http_request()
        print(request_data)
        http_request = HttpHandler.HttpHandler(request_data)
        if http_request.method.upper() == "POST":
            response = self.handle_POST_request(http_request)
        elif http_request.method.upper() == "GET":
            response = self.handle_GET_request(http_request)
        elif http_request.method.upper() == "DELETE":
            response = self.handle_DELETE_request(http_request)
        else:
            response = HttpHandler.response_phrase(400, "HTTP/1.1")
        self.secure_socket.sendall(response)
        self.secure_socket.close()
        print(response.decode())
