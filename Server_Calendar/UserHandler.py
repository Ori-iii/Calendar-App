class User:

    def __init__(self, ID, username, password, display_name, created_at, events=None, groups=None):
        if events is None:
            events = []
        self.ID = ID
        self.username = username
        self.password = password
        self.display_name = display_name
        self.created_at = created_at
        self.events = events
        self.groups = groups

    def __repr__(self):
        return_str = (f"""
        <==========User==========>
            {self.username}
            {self.display_name}
            {self.created_at}
            {self.password}
            {self.ID}
        <========================>""")
        for event in self.events:
            return_str += f"\r\n  {event}"
        for group in self.groups:
            return_str += f"\r\n  {group}"
        return return_str

    def to_dict(self):
        user_dict = {
            "ID": self.ID,
            "username": self.username,
            "password": self.password,
            "display_name": self.display_name,
            "created_at": str(self.created_at) if self.created_at else None,
            # Recursively convert each Event object into a dictionary
            "events": [
                event.to_dict() if hasattr(event, "to_dict") else None
                for event in self.events
            ],
            "groups": [
                group.to_dict() if hasattr(group, "to_dict") else None
                for group in self.groups
            ],
        }
        return user_dict


class Event:

    def __init__(self, ID, title, start_time, end_time, owner_id, group_id, payload, created_at):
        self.ID = ID
        self.title = title
        self.start_time = start_time
        self.end_time = end_time
        self.owner_id = owner_id
        self.group_id = group_id
        self.payload = payload
        self.created_at = created_at

    def __repr__(self):
        return_str = (f"""
                <==========Event=========>
                    {self.title}
                    {self.start_time}
                    {self.end_time}
                    {self.created_at}
                    {self.owner_id}
                    {self.payload if self.payload else ''}
                <========================>
                """)
        return return_str

    def to_dict(self):
        return {
            "ID": self.ID,
            "title": self.title,
            "start_time": str(self.start_time) if self.start_time else None,
            "end_time": str(self.end_time) if self.end_time else None,
            "owner_id": self.owner_id,
            "group_id": self.group_id,
            "payload": self.payload,
            "created_at": self.created_at,
        }


class Group:
    def __init__(self, ID, group_name, created_by, created_at, members: list = None):
        self.ID = ID
        self.group_name = group_name
        self.created_by = created_by
        self.created_at = created_at
        self.members = members

    def __repr__(self):
        return_str = (f"""
        <==========Group=========>
            {self.ID}
            {self.group_name}
            {self.created_by}
            {self.created_at}
        <========================>""")
        for member in self.members:
            return_str += f"\r\n  {member}"
        return return_str

    def to_dict(self):
        return {
            "ID": self.ID,
            "group_name": self.group_name,
            "created_by": self.created_by,
            "created_at": self.created_at,
            "members": [
                member.to_dict() if hasattr(member, "to_dict") else None
                for member in self.members
            ],
        }


class Member:
    def __init__(self, username, group_id, role, joined_at, display_name):
        self.username = username
        self.display_name = display_name
        self.group_id = group_id
        self.role = role
        self.joined_at = joined_at

    def __repr__(self):
        return_str = (f"""
                <=========Member=========>
                    {self.username}
                    {self.group_id}
                    {self.role}
                    {self.joined_at}
                <========================>
                """)
        return return_str

    def to_dict(self):
        return {
            "username": self.username,
            "group_id": self.group_id,
            "role": self.role,
            "joined_at": self.joined_at,
            "display_name": self.display_name
        }
