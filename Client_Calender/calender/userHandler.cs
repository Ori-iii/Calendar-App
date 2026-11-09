using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace calender
{
    public class User
    {
        public string ID { get; set; }
        public string username { get; set; }
        public string password { get; set; }
        public string display_name { get; set; }
        public DateTime created_at { get; set; }
        public List<Event> events { get; set; }
        public List<Group> groups { get; set; }

        public User(string ID, string username, string password, string display_name, DateTime created_at, List<Event>? events = null, List<Group>? groups = null)
        {
            this.ID = ID;
            this.username = username;
            this.password = password;
            this.display_name = display_name;
            this.created_at = created_at;
            if (events == null || events.Count == 0)
            {
                this.events = new List<Event>();
            }
            else
            {
                this.events = events;
            }
            if (groups == null || groups.Count == 0)
            {
                this.groups = new List<Group>();
            }
            else
            {
                this.groups = groups;
            }
        }

        public override string ToString()
        {
            return $"User(ID: {ID}, username: {username}, display_name: {display_name}, created_at: {created_at}, events: [{string.Join(", ", events)}],\r\n groups: [{string.Join(", ", groups)}])";
        }
    }

    public class Event
    {
        public string ID { get; set; }
        public DateTime created_at { get; set; }
        public string title { get; set; }
        public DateTime start_time { get; set; }
        public DateTime end_time { get; set; }
        public string owner_id { get; set; }
        public string group_id { get; set; }
        public string? payload { get; set; }
        public Group? groupPointer { get; set; }

        public Event(string ID, DateTime created_at, string title, DateTime start_time, DateTime end_time, string owner_id, string group_id, string? payload = null)
        {
            this.ID = ID;
            this.created_at = created_at;
            this.title = title;
            this.start_time = start_time;
            this.end_time = end_time;
            this.owner_id = owner_id;
            this.group_id = group_id;
            this.payload = payload;
        }

        public override string ToString()
        {
            return $"Event(ID: {ID}, title: {title}, start_time: {start_time}, end_time: {end_time}, owner_id: {owner_id}, group_id: {group_id})";
        }

        public string FormatTime(DateTime dt) => dt.ToString("h:mm tt");
    }

    public class Member
    {
        public string username { get; set; }
        public string display_name { get; set; }
        public DateTime joined_at { get; set; }
        public string role { get; set; }
        public Member(string username, string display_name, DateTime joined_at, string role)
        {
            this.username = username;
            this.display_name = display_name;
            this.joined_at = joined_at;
            this.role = role;
        }
        public override string ToString()
        {
            return $"Member(username: {username}, display_name: {display_name}, joined_at: {joined_at}, role: {role})";
        }
    }

    public class Group
    {
        public string ID { get; set; }
        public string group_name { get; set; }
        public DateTime created_at { get; set; }
        public List<Member> members { get; set; }
        public List<Event> eventPointers = new List<Event>();
        public Group(string ID, string group_name, DateTime created_at, List<Member>? members = null)
        {
            this.ID = ID;
            this.group_name = group_name;
            this.created_at = created_at;
            if (members == null || members.Count == 0)
            {
                this.members = new List<Member>();
            }
            else
            {
                this.members = members;
            }
        }
        public override string ToString()
        {
            return $"Group(ID: {ID}, group_name: {group_name}, created_at: {created_at}, members: [{string.Join(", ", members)}], eventPointers: [{string.Join(", ", eventPointers)}])";
        }
    }
    public class UpdateResponse
    {
        public List<Event> events { get; set; } = new();
        public List<Group> groups { get; set; } = new();
    }
}
