import pyodbc
import UserHandler
import bcrypt


def hash_password(password: str) -> str:
    return bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt()).decode("utf-8")


def verify_password(password: str, password_hash: str) -> bool:
    return bcrypt.checkpw(password.encode("utf-8"), password_hash.encode("utf-8"))


class DatabaseHandler:
    def __init__(self, connection_string):
        self.conn = pyodbc.connect(connection_string)
        self.cursor = self.conn.cursor()

    def get_password_from_ID(self, ID):
        user_query = """
        SELECT DISTINCT password_hash FROM USERS WHERE ID = ?
        """
        try:
            self.cursor.execute(user_query, (ID,))
            user_row = self.cursor.fetchone()
            if not user_row:
                print("User not found.")
                return None
            else:
                return user_row.password_hash
        except pyodbc.Error as e:
            print(e)
            return None

    def get_user(self, user_ID):
        user_query = """
                SELECT ID, username, password_hash, display_name, 
                FORMAT(created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
                FROM Users
                WHERE ID = ?
            """
        self.cursor.execute(user_query, (user_ID,))
        user_row = self.cursor.fetchone()

        if not user_row:
            print("User not found.")
            return None
        else:

            events_query = """
                SELECT DISTINCT 
                    e.ID, 
                    e.title, 
                    FORMAT(e.start_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS start_time, 
                    FORMAT(e.end_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS end_time, 
                    e.owner_id, 
                    e.group_id, 
                    e.payload, 
                    FORMAT(e.created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
                FROM Events e
                WHERE e.owner_id = ?
                   OR e.ID IN (SELECT event_id FROM EventAccess WHERE user_id = ?)
                   OR e.group_id IN (SELECT group_id FROM GroupMembers WHERE user_id = ?)
            """

            self.cursor.execute(events_query, (user_ID, user_ID, user_ID))
            event_rows = self.cursor.fetchall()

            user_events = []
            for row in event_rows:
                event = UserHandler.Event(
                    ID=row.ID,
                    title=row.title,
                    start_time=row.start_time,
                    end_time=row.end_time,
                    owner_id=row.owner_id,
                    group_id=row.group_id,
                    payload=row.payload,
                    created_at=row.created_at,
                )
                user_events.append(event)
            user = UserHandler.User(
                ID=user_row.ID,
                username=user_row.username,
                password="no password here:)",
                display_name=user_row.display_name,
                created_at=user_row.created_at,
                events=user_events,
                groups=self.get_user_gruops(user_row.ID),
            )

            return user

    def get_user_by_username(self, username):
        user_query = """
                        SELECT ID, username, password_hash, display_name, 
                        FORMAT(created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
                        FROM Users
                        WHERE username = ?
                    """
        self.cursor.execute(user_query, (username,))
        user_row = self.cursor.fetchone()

        if not user_row:
            print("User not found.")
            return None
        else:

            user = UserHandler.User(
                ID=user_row.ID,
                username=user_row.username,
                password="no password here:)",
                display_name=user_row.display_name,
                created_at=user_row.created_at,
                groups=self.get_user_gruops(user_row.ID)
            )

            events_query = """
                        SELECT DISTINCT 
                            e.ID, 
                            e.title, 
                            FORMAT(e.start_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS start_time, 
                            FORMAT(e.end_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS end_time, 
                            e.owner_id, 
                            e.group_id, 
                            e.payload, 
                            FORMAT(e.created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
                        FROM Events e
                        WHERE e.owner_id = ?
                           OR e.ID IN (SELECT event_id FROM EventAccess WHERE user_id = ?)
                           OR e.group_id IN (SELECT group_id FROM GroupMembers WHERE user_id = ?)
                    """

            self.cursor.execute(events_query, (user.ID, user.ID, user.ID))
            event_rows = self.cursor.fetchall()

            user_events = []
            for row in event_rows:
                event = UserHandler.Event(
                    ID=row.ID,
                    title=row.title,
                    start_time=row.start_time,
                    end_time=row.end_time,
                    owner_id=row.owner_id,
                    group_id=row.group_id,
                    payload=row.payload,
                    created_at=row.created_at,
                )
                user_events.append(event)
            user.events = user_events
            return user

    def create_user(self, username, password, display_name):
        password_hash = hash_password(password)
        query = """
        INSERT INTO USERS (username, password_hash, display_name)
        OUTPUT 
            INSERTED.ID, 
            INSERTED.username, 
            INSERTED.password_hash, 
            INSERTED.display_name, 
            FORMAT(INSERTED.created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
        VALUES (?, ?, ?)
        """
        try:
            self.cursor.execute(query, (username, password_hash, display_name))
            user_row = self.cursor.fetchone()
            if not user_row:
                print("User not found.")
                return None

            user = UserHandler.User(
                ID=user_row.ID,
                username=user_row.username,
                password="no password here:)",
                display_name=user_row.display_name,
                created_at=user_row.created_at,
                events=[],
                groups=[]
            )
            self.conn.commit()
            return user
        except pyodbc.IntegrityError as e:
            self.conn.rollback()

            if "2627" in str(e) or "2601" in str(e):
                print(f"Error: Username '{username}' already exists.")
                return None

            raise e

    def add_event(self, title: str, start_time, end_time, owner_id: str, group_id: str = None, payload: dict = None):
        query = """
                    INSERT INTO Events (title, start_time, end_time, owner_id, group_id, payload)
                    OUTPUT INSERTED.ID, INSERTED.title, 
                    FORMAT(INSERTED.start_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS start_time, 
                    FORMAT(INSERTED.end_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS end_time,
                    INSERTED.owner_id, INSERTED.group_id, INSERTED.payload, 
                    FORMAT(INSERTED.created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
                    VALUES (?, ?, ?, ?, ?, ?)
                    
        """
        try:
            self.cursor.execute(query, (title, start_time, end_time, owner_id, group_id, payload))
            event_row = self.cursor.fetchone()
            if not event_row:
                print("Event not found.")
                return None

            event = UserHandler.Event(
                ID=event_row.ID,
                title=event_row.title,
                start_time=event_row.start_time,
                end_time=event_row.end_time,
                owner_id=event_row.owner_id,
                group_id=event_row.group_id,
                payload=event_row.payload,
                created_at=event_row.created_at,
            )
            self.conn.commit()
            return event
        except pyodbc.IntegrityError as e:
            self.conn.rollback()
            if "2627" in str(e) or "2601" in str(e):
                print(f"Error: event '{title}' already exists.")
                return None

            # If it's a different integrity issue (e.g., FK constraint), re-raise it
            raise e

    def share_event(self, event_id, owner_id, share_username, access_level):
        #access_level is viewer or editor
        check_query = """
            SELECT ID
            FROM Events
            WHERE ID = ?
            AND owner_id = ?;
        """
        try:
            self.cursor.execute(check_query, (event_id, owner_id))
            event_row = self.cursor.fetchone()
            if not event_row:
                print("Event not found.")
                return False
            fetch_ID = """
                SELECT ID FROM USERS WHERE username = ?
            """

            self.cursor.execute(fetch_ID, (share_username,))
            share_id = self.cursor.fetchone()
            if not share_id:
                print("User not found.")
                return False
            share_id = share_id.ID
            share_query = """
            INSERT INTO EventAccess (event_id, user_id, access_level)
            VALUES (?, ?, ?)
            """
            self.cursor.execute(share_query, (event_id, share_id, access_level))
            self.conn.commit()
            return True
        except pyodbc.IntegrityError:
            self.conn.rollback()
            return False

    def create_group(self, group_name, creator_id, members: list = None):
        query = """
        INSERT INTO GROUPS (created_by, group_name)
        OUTPUT INSERTED.ID, INSERTED.created_by, INSERTED.group_name,
        FORMAT(INSERTED.created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
        VALUES (?, ?)
        """
        try:
            self.cursor.execute(query, (creator_id, group_name))
            group_row = self.cursor.fetchone()
            group = UserHandler.Group(
                ID=group_row.ID,
                created_by=group_row.created_by,
                group_name=group_row.group_name,
                created_at=group_row.created_at,
            )
            if members:
                placeholders = ",".join("?" for _ in members)
                select_query = f"SELECT ID FROM USERS WHERE username IN ({placeholders})"

                self.cursor.execute(select_query, members)
                user_rows = self.cursor.fetchall()

                if not user_rows:
                    print("No matching users found for the provided usernames.")
                    self.conn.rollback()
                    return 0

                data = [(group.ID, row.ID, "admin" if row.ID == creator_id else "member") for row in user_rows]

                members_query = """
                        INSERT INTO GroupMembers (group_id, user_id, role)
                        VALUES (?, ?, ?)
                    """
                self.cursor.executemany(members_query, data)
                fetch_members_query = """
                                    SELECT group_id, user_id, role,
                                           FORMAT(joined_at, 'yyyy-MM-dd HH:mm:ss.fffffffzzz') AS joined_at
                                    FROM GroupMembers
                                    WHERE group_id = ?
                                """
                self.cursor.execute(fetch_members_query, (group.ID,))
                member_rows = self.cursor.fetchall()
                members_lst = []
                for member in member_rows:
                    fetch_ID = """
                    SELECT username, display_name FROM USERS WHERE ID = ?
                    """
                    self.cursor.execute(fetch_ID, (member.user_id,))
                    username_row = self.cursor.fetchone()
                    members_lst.append(UserHandler.Member(
                        username=username_row.username,
                        group_id=member.group_id,
                        role=member.role,
                        joined_at=member.joined_at,
                        display_name=username_row.display_name
                    ))
                self.conn.commit()
                group.members = members_lst
            return group

        except pyodbc.IntegrityError as e:
            self.conn.rollback()
            if "2627" in str(e) or "2601" in str(e):
                print(f"Error: group '{group_name}' already exists.")
            else:
                print(f"Error: {e}")
                return None

    # get a group ID, return True if the group is deleted None if else
    def delete_group(self, group_id, user_id):
        query = """
        DELETE FROM GROUPS WHERE ID = ?
        AND EXISTS (SELECT 1 FROM GroupMembers WHERE user_id = ? AND role = 'admin')
        """
        try:
            self.cursor.execute(query, (group_id, user_id))

            if self.cursor.rowcount == 0:
                # No rows deleted (either no permission or group_id not found)
                self.conn.rollback()
                return False

            self.conn.commit()
            return True

        except pyodbc.IntegrityError as e:
            self.conn.rollback()
            print(f"Error: {e}")
            return None

    def delete_group_member(self, group_id, user_id):
        try:
            # 1. Check if the user being deleted is an admin
            self.cursor.execute(
                "SELECT role FROM GroupMembers WHERE group_id = ? AND user_id = ?",
                (group_id, user_id)
            )
            row = self.cursor.fetchone()
            if not row:
                self.conn.rollback()
                return False

            is_admin = (row.role == 'admin')

            # 2. If they are an admin, check how many admins are left
            if is_admin:
                self.cursor.execute(
                    "SELECT COUNT(*) as admin_count FROM GroupMembers WHERE group_id = ? AND role = 'admin'",
                    (group_id,)
                )
                admin_count = self.cursor.fetchone().admin_count

                # If this is the last admin, we need to reassign leadership
                if admin_count <= 1:
                    # Find the next best successor (e.g., the member who joined earliest)
                    self.cursor.execute(
                        """
                        SELECT TOP 1 user_id FROM GroupMembers 
                        WHERE group_id = ? AND user_id != ? 
                        ORDER BY joined_at ASC
                        """,
                        (group_id, user_id)
                    )
                    successor = self.cursor.fetchone()

                    if successor:
                        # Promote the successor to admin
                        self.cursor.execute(
                            "UPDATE GroupMembers SET role = 'admin' WHERE group_id = ? AND user_id = ?",
                            (group_id, successor.user_id)
                        )
                    else:
                        # No other members are left in the group. Delete the group entirely.
                        self.cursor.execute("DELETE FROM GROUPS WHERE ID = ?", (group_id,))
                        self.conn.commit()
                        return True

            # 3. Proceed with deleting the member record
            query = "DELETE FROM GroupMembers WHERE group_id = ? AND user_id = ?"
            self.cursor.execute(query, (group_id, user_id))
            self.conn.commit()
            return True

        except pyodbc.IntegrityError as e:
            self.conn.rollback()
            print(f"Error: {e}")
            return None

    def add_group_member(self, group_id, user_id, role):
        if role not in ["admin", "member", "viewer"]:
            print(f"Error: {role} is not a valid role.")
            return None

        query = """
        INSERT INTO GroupMembers (group_id, user_id, role)
        OUTPUT INSERTED.group_id, INSERTED.user_id, INSERTED.role,
        FORMAT(INSERTED.joined_at, 'yyyy-MM-dd HH:mm:ss.fffffffzzz') AS joined_at
        VALUES (?, ?, ?)
        """
        try:
            self.cursor.execute(query, (group_id, user_id, role))
            member_row = self.cursor.fetchone()

            # תיקון: שליפת גם ה-username וגם ה-display_name מהמשתמש
            fetch_ID = """
                SELECT username, display_name FROM USERS WHERE ID = ?
            """
            self.cursor.execute(fetch_ID, (member_row.user_id,))
            user_row = self.cursor.fetchone()

            member = UserHandler.Member(
                username=user_row.username,
                group_id=member_row.group_id,
                role=member_row.role,
                joined_at=member_row.joined_at,
                display_name=user_row.display_name  # הוספת השדה החסר!
            )
            self.conn.commit()
            return member
        except pyodbc.IntegrityError as e:
            self.conn.rollback()
            print(f"Error: {e}")
            return None

    def get_group(self, group_id):
        query = """
        SELECT DISTINCT ID,
        group_name,
        created_by,
        FORMAT(created_at, 'yyyy-MM-dd HH:mm:ss.fffffffzzz') AS created_at
        FROM GROUPS WHERE ID = ?
        """
        self.cursor.execute(query, (group_id,))
        group_row = self.cursor.fetchone()

        members_query = """
            SELECT group_id, user_id, role,
            FORMAT(joined_at, 'yyyy-MM-dd HH:mm:ss.fffffffzzz') AS joined_at
            FROM GroupMembers
            WHERE group_id = ?
        """
        self.cursor.execute(members_query, (group_id,))
        members_rows = self.cursor.fetchall()
        members_lst = []
        for member in members_rows:
            fetch_ID = """
                SELECT username, display_name FROM USERS WHERE ID = ?
            """
            self.cursor.execute(fetch_ID, (member.user_id,))
            username_row = self.cursor.fetchone()

            members_lst.append(UserHandler.Member(
                username=username_row.username,
                group_id=member.group_id,
                role=member.role,
                joined_at=member.joined_at,
                display_name=username_row.display_name
            ))
        group = UserHandler.Group(
            ID=group_row.ID,
            group_name=group_row.group_name,
            created_by=group_row.created_by,
            created_at=group_row.created_at,
            members=members_lst,
        )
        return group

    def get_user_gruops(self, user_id):
        query = """
        select group_id from GroupMembers WHERE user_id = ?
        """
        try:
            self.cursor.execute(query, (user_id,))
            groups_IDs = self.cursor.fetchall()
            gruops_lst = []
            for gruop in groups_IDs:
                ID = gruop.group_id
                gruops_lst.append(self.get_group(ID))

            return gruops_lst

        except pyodbc.IntegrityError as e:
            self.conn.rollback()
            print(f"Error: {e}")
            return None

    def check_user_updates(self, user_id, timestamp):
        events_query = """
            SELECT
                e.ID, 
                e.title, 
                FORMAT(e.start_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS start_time, 
                FORMAT(e.end_time, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS end_time, 
                e.owner_id, 
                e.group_id, 
                e.payload, 
                FORMAT(e.created_at, 'yyyy-MM-dd T HH:mm:ss.fffffffzzz') AS created_at
            FROM Events e
            WHERE (e.owner_id = ?
               OR e.ID IN (SELECT event_id FROM EventAccess WHERE user_id = ?)
               OR e.group_id IN (SELECT group_id FROM GroupMembers WHERE user_id = ?))
               AND created_at > ?
        """
        try:
            self.cursor.execute(events_query, (user_id, user_id, user_id, timestamp))
            events_rows = self.cursor.fetchall()
            events_lst = []
            for event in events_rows:
                events_lst.append(UserHandler.Event(
                    ID=event.ID,
                    title=event.title,
                    start_time=event.start_time,
                    end_time=event.end_time,
                    owner_id=event.owner_id,
                    group_id=event.group_id,
                    payload=event.payload,
                    created_at=event.created_at,
                ))

            # Fetch groups where the user is a member, OR groups where any membership change occurred since the timestamp
            groups_query = """
                SELECT DISTINCT g.ID
                FROM GROUPS g
                JOIN GroupMembers gm ON g.ID = gm.group_id
                WHERE gm.user_id = ?
                UNION
                SELECT DISTINCT group_id AS ID
                FROM GroupMembers
                WHERE group_id IN (SELECT group_id FROM GroupMembers WHERE user_id = ?)
                  AND joined_at > ?
            """
            self.cursor.execute(groups_query, (user_id, user_id, timestamp))
            group_rows = self.cursor.fetchall()

            groups_lst = []
            for group_row in group_rows:
                groups_lst.append(self.get_group(group_row.ID))

            return {"events": events_lst, "groups": groups_lst}
        except pyodbc.Error as e:
            self.conn.rollback()
            print(f"Error: {e}")
            return None

    def delete_event(self, event_id, user_id):
        query = """
            DELETE FROM Events 
            WHERE ID = ? 
              AND (
                  owner_id = ? 
                  OR group_id IN (
                      SELECT group_id 
                      FROM GroupMembers 
                      WHERE user_id = ? AND role = 'admin'
                  )
              )
        """
        try:
            self.cursor.execute(query, (event_id, user_id, user_id))
            if self.cursor.rowcount == 0:
                self.conn.rollback()
                return False

            self.conn.commit()
            return True

        except pyodbc.Error as e:
            self.conn.rollback()
            print(f"Error deleting event: {e}")
            return None

    def delete_member(self, admin_id, group_id, member_name):
        query = """
        select ID from USERS where username = ?
        """
        try:
            self.cursor.execute(query, (member_name,))
            member_id = self.cursor.fetchone()
            if member_id is None:
                self.conn.rollback()
                return False
            member_id = member_id.ID
            check_query = """
                SELECT 1 FROM GroupMembers WHERE user_id = ? AND role = 'admin'
            """
            self.cursor.execute(check_query, (admin_id,))
            if self.cursor.fetchone() is None:
                self.conn.rollback()
                return False
            deleted = self.delete_group_member(group_id, member_id)
            return deleted
        except pyodbc.Error as e:
            self.conn.rollback()
            print(f"Error deleting member: {e}")
            return None

    def add_members(self, members, admin_id, group_id):
        check_query = """
            SELECT 1 FROM GroupMembers WHERE user_id = ? AND role = 'admin'
        """
        try:
            self.cursor.execute(check_query, (admin_id,))
            if self.cursor.fetchone() is None:
                self.conn.rollback()
                return False

            members_lst = []
            for member in members:
                # Check if user exists
                query = "SELECT ID FROM USERS WHERE username = ?"
                self.cursor.execute(query, (member,))
                member_row = self.cursor.fetchone()
                if member_row is None:
                    self.conn.rollback()
                    return "user_not_found"

                member_id = member_row.ID

                # Check if already in group
                existing_check = "SELECT 1 FROM GroupMembers WHERE group_id = ? AND user_id = ?"
                self.cursor.execute(existing_check, (group_id, member_id))
                if self.cursor.fetchone() is not None:
                    self.conn.rollback()
                    return "already_member"  # Return specific indicator if user is already in the group

                new_member = self.add_group_member(group_id, member_id, "member")
                if new_member:
                    members_lst.append(new_member)

            return members_lst
        except pyodbc.Error as e:
            self.conn.rollback()
            print(f"Error adding member: {e}")
            return None

    def delete_user(self, user_id):
        query = """
        DELETE FROM USERS where ID = ?
        """
        try:
            self.cursor.execute(query, (user_id,))
            if self.cursor.rowcount == 0:
                print("Error deleting user")
                self.conn.rollback()
                return False
            self.conn.commit()
            return True
        except pyodbc.Error as e:
            self.conn.rollback()
            print(f"Error deleting user: {e}")
            return None

    def close(self):
        self.cursor.close()
