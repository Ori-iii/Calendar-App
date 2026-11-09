import http


def response_phrase(response_code: int, version: str, headers: dict = None, data=None):
    response = f"""{version.upper()} {response_code} {http.HTTPStatus(response_code).phrase}\r\n"""
    if headers:
        for key, value in headers.items():
            response += f"{key}: {value}\r\n"
    response += "\r\n"
    if isinstance(data, bytes):
        return response.encode() + data
    else:
        data_str = data if data is not None else ""
        return (response + data_str).encode()


class HttpHandler:
    def __init__(self, raw_request: bytes | str):
        if isinstance(raw_request, str):
            raw_request = raw_request.encode()
        self.raw_request = raw_request
        self.method = ""
        self.path = ""
        self.http_version = ""
        self.headers = {}
        self.data = b""

        self._parse()

    def _parse(self):
        separator = b"\r\n\r\n"
        alt_separator = b"\n\n"

        if separator in self.raw_request:
            header_part, self.data = self.raw_request.split(separator, 1)
            lines = header_part.split(b"\r\n")
        elif alt_separator in self.raw_request:
            header_part, self.data = self.raw_request.split(alt_separator, 1)
            lines = header_part.split(b"\n")
        else:
            lines = self.raw_request.splitlines()
            self.data = b""

        if not lines:
            return

        request_line = lines[0].decode("utf-8", errors="ignore").strip()
        parts = request_line.split()
        if len(parts) >= 1:
            self.method = parts[0]
        if len(parts) >= 2:
            self.path = parts[1]
        if len(parts) >= 3:
            self.http_version = parts[2]

        for encode_line in lines[1:]:
            line = encode_line.decode("utf-8", errors="ignore").strip()
            if not line:
                continue
            if ":" in line:
                key, value = line.split(":", 1)
                self.headers[key.strip().lower()] = value.strip()

    def __repr__(self):
        class_str = f"{self.method} {self.path} {self.http_version}\r\n"
        for i in self.headers.items():
            class_str += f"    {i[0]}: {i[1]}\r\n"
        return class_str
