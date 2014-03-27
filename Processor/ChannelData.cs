//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published
//  by the Free Software Foundation; either version 2 of the License, or
//  (at your option) version 3.

//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Lesser General Public License for more details.

//  You should have received a copy of the GNU Lesser General Public License
//  along with this program; if not, write to the
//  Free Software Foundation, Inc.,
//  51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Collections.Generic;

namespace libirc
{
    public partial class ProcessorIRC
    {
        private bool ChannelInfo(string[] code, string command, string source, string parameters, string _value)
        {
            if (code.Length > 3)
            {
                Channel channel = _Network.GetChannel(code[3]);

            }
            return false;
        }

        private bool ParseUser(string[] code, string realname)
        {
            if (code.Length > 8)
            {
                Channel channel = _Network.GetChannel(code[3]);
                string ident = code[4];
                string host = code[5];
                string nick = code[7];
                string server = code[6];
                if (realname != null & realname.Length > 2)
                {
                    realname = realname.Substring(2);
                }
                else if (realname == "0 ")
                {
                    realname = "";
                }
                char mode = '\0';
                bool IsAway = false;
                if (code[8].Length > 0)
                {
                    // if user is away we flag him
                    if (code[8].StartsWith("G", StringComparison.Ordinal))
                    {
                        IsAway = true;
                    }
                    mode = code[8][code[8].Length - 1];
                    if (!_Network.UChars.Contains(mode))
                    {
                        mode = '\0';
                    }
                }
                if (channel != null)
                {
                    if (updated_text)
                    {
                        if (!channel.ContainsUser(nick))
                        {
                            User _user = null;
                            if (mode != '\0')
                            {
                                _user = new User(mode.ToString() + nick, host, _Network, ident, server);
                            }
                            else
                            {
                                _user = new User(nick, host, _Network, ident, server);
                            }
                            _user.LastAwayCheck = DateTime.Now;
                            _user.RealName = realname;
                            if (IsAway)
                            {
                                _user.AwayTime = DateTime.Now;
                            }
                            _user.Away = IsAway;
                            lock (channel.UserList)
                            {
                                channel.UserList.Add(_user.Nick.ToLower(), _user);
                            }
                            return true;
                        }
                        User user = null;
                        lock (channel.UserList)
                        {
                            channel.UserList.TryGetValue(nick.ToLower(), out user);
                        }
                        if (user != null)
                        {
                            user.Ident = ident;
                            user.Host = host;
                            user.Server = server;
                            user.RealName = realname;
                            user.LastAwayCheck = DateTime.Now;
                            if (!user.Away && IsAway)
                            {
                                user.AwayTime = DateTime.Now;
                            }
                            user.Away = IsAway;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        private bool ParseInfo(string[] code, string value)
        {
            if (code.Length > 3)
            {
                string name = code[4];
                if (!updated_text)
                {
                    return true;
                }
                Channel channel = _Network.GetChannel(name);
                if (channel != null)
                {
                    string[] _chan = value.Split(' ');
                    foreach (var user in _chan)
                    {
                        string _user = user;
                        char _UserMode = '\0';
                        if (_user.Length > 0)
                        {
                            foreach (char mode in _Network.UChars)
                            {
                                if (_user[0] == mode)
                                {
                                    _UserMode = user[0];
                                    _user = _user.Substring(1);
                                }
                            }

                            lock (channel.UserList)
                            {
                                User _u = channel.UserFromName(_user);
                                if (_u == null && !string.IsNullOrEmpty(_user))
                                {
                                    channel.UserList.Add(user.ToLower(), new User(user, "", _Network, ""));
                                }
                                else
                                {
                                    if (_u != null)
                                    {
                                        _u.SymbolMode(_UserMode);
                                    }
                                }
                            }
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        private bool ChannelTopic(string[] code, string command, string source, string parameters, string value)
        {
            if (code.Length > 3)
            {
                string name = "";
                if (parameters.Contains("#"))
                {
                    name = parameters.Substring(parameters.IndexOf("#", StringComparison.Ordinal)).Replace(" ", "");
                }
                string topic = value;
                Channel channel = _Network.GetChannel(name);
                if (channel != null)
                {
                    channel.Topic = topic;
                    return true;
                }
            }
            return false;
        }

        private bool FinishChan(string[] code)
        {
            if (code.Length > 2)
            {
                Channel channel = _Network.GetChannel(code[3]);
                if (channel != null)
                {

                    channel.IsParsingWhoData = false;
                }
            }
            return false;
        }

        private bool TopicInfo(string[] code, string parameters)
        {
            if (code.Length > 5)
            {
                string name = code[3];
                string user = code[4];
                string time = code[5];
                Channel channel = _Network.GetChannel(name);
                if (channel != null)
                {
                    channel.TopicDate = int.Parse(time);
                    channel.TopicUser = user;
                }
            }
            return false;
        }

        private bool Kick(string source, string parameters, string value)
        {
            string user = parameters.Substring(parameters.IndexOf(" ", StringComparison.Ordinal) + 1);
            // petan!pidgeon@petan.staff.tm-irc.org KICK #support HelpBot :Removed from the channel
            string chan = parameters.Substring(0, parameters.IndexOf(" ", StringComparison.Ordinal));
            Channel channel = _Network.GetChannel(chan);
            Network.NetworkArgs args = new Network.NetworkArgs();
            args.NetworkEvent = Network.NetworkArgs.Event.Kick;
            args.TargetChannel = channel;
            _Network.TriggerEvent(args);
            if (channel != null)
            {
                    lock (channel.UserList)
                    {
                        if (updated_text && channel.ContainsUser(user))
                        {
                            User delete = null;
                            delete = channel.UserFromName(user);
                            if (delete != null)
                            {
                                channel.UserList.Remove(user.ToLower());
                            }
                            if (delete.IsPidgeon)
                            {
                                channel.ChannelWork = false;
                            }
                        }
                    }
                return true;
            }
            return false;
        }

        private bool Join(string source, string parameters, string value)
        {
            string chan = parameters;
            chan = chan.Replace(" ", "");
            if (string.IsNullOrEmpty(chan))
            {
                chan = value;
            }
            string user = source.Substring(0, source.IndexOf("!", StringComparison.Ordinal));
            string _ident;
            string _host;
            _host = source.Substring(source.IndexOf("@", StringComparison.Ordinal) + 1);
            _ident = source.Substring(source.IndexOf("!", StringComparison.Ordinal) + 1);
            _ident = _ident.Substring(0, _ident.IndexOf("@", StringComparison.Ordinal));
            Channel channel = _Network.GetChannel(chan);
            if (channel != null)
            {
                if (updated_text)
                {
                    lock(channel.UserList)
                    {
                        if (!channel.ContainsUser(user))
                        {
                            channel.UserList.Add(user.ToLower(), new User(user, _host, _Network, _ident));
                        }
                    }
                }
                return true;
            }
            return false;
        }

        private bool ChannelBans2(string[] code)
        {
            if (code.Length > 4)
            {
                Channel channel = _Network.GetChannel(code[3]);
                if (channel != null)
                {
                    if (channel.IsParsingBanData)
                    {
                        channel.IsParsingBanData = false;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool ChannelBans(string[] code)
        {
            if (code.Length > 6)
            {
                Channel channel = _Network.GetChannel(code[3]);
                if (channel != null)
                {
                    if (channel.Bans == null)
                    {
                        channel.Bans = new List<SimpleBan>();
                    }
                    if (!channel.ContainsBan(code[4]))
                    {
                        channel.Bans.Add(new SimpleBan(code[5], code[4], code[6]));
                    }
                }
            }
            return false;
        }

        private bool Part(string source, string parameters, string value)
        {
            string chan = parameters;
            chan = chan.Replace(" ", "");
            string user = source.Substring(0, source.IndexOf("!", StringComparison.Ordinal));
            string _ident;
            string _host;
            _host = source.Substring(source.IndexOf("@", StringComparison.Ordinal) + 1);
            _ident = source.Substring(source.IndexOf("!", StringComparison.Ordinal) + 1);
            _ident = _ident.Substring(0, _ident.IndexOf("@", StringComparison.Ordinal));
            Channel channel = _Network.GetChannel(chan);
            if (channel != null)
            {
                User delete = null;
                    if (updated_text)
                    {
                        if (channel.ContainsUser(user))
                        {
                            delete = channel.UserFromName(user);

                            if (delete != null)
                            {
                                channel.UserList.Remove(user.ToLower());
                            }
                            return true;
                        }
                        return true;
                    }
                    return true;
            }
            return false;
        }

        private bool Topic(string source, string parameters, string value)
        {
            string chan = parameters;
            chan = chan.Replace(" ", "");
            Channel channel = _Network.GetChannel(chan);
            if (channel != null)
            {
                channel.Topic = value;
                if (updated_text)
                {
                    channel.TopicDate = (int)Defs.ConvertDateToUnix(DateTime.Now);
                    channel.TopicUser = source;
                }
                return true;
            }
            return false;
        }

        private bool ProcessNick(string source, string parameters, string value)
        {
            string nick = source.Substring(0, source.IndexOf("!", StringComparison.Ordinal));
            string _new = value;
            if (string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(parameters))
            {
                // server is fucked
                _new = parameters;
                // server is totally borked
                if (_new.Contains(" "))
                {
                    _new = _new.Substring(0, _new.IndexOf(" ", StringComparison.Ordinal));
                }
            }
            lock (_Network.Channels.Values)
            {
                foreach (Channel channel in _Network.Channels.Values)
                {
                    if (channel.ChannelWork)
                    {
                        User user = channel.UserFromName (nick);
                        if (user != null)
                        {
                            if (updated_text)
                            {
                                channel.RemoveUser(user);
                                user.SetNick(_new);
                                lock (channel.UserList)
                                {
                                    channel.UserList.Add(_new.ToLower (), user);
                                }
                            }
                        }
                    }
                }
            }
            return true;
        }

        private bool Mode(string source, string parameters, string value)
        {
            if (parameters.Contains(" "))
            {
                string chan = parameters.Substring(0, parameters.IndexOf(" ", StringComparison.Ordinal));
                chan = chan.Replace(" ", "");
                string user = source;
                if (chan.StartsWith(_Network.ChannelPrefix, StringComparison.Ordinal))
                {
                    Channel channel = _Network.GetChannel(chan);
                    if (channel != null)
                    {
                        string change = parameters.Substring(parameters.IndexOf(" ", StringComparison.Ordinal));
                        if (!updated_text)
                        {
                            return true;
                        }
                        while (change.StartsWith(" ", StringComparison.Ordinal))
                        {
                            change = change.Substring(1);
                        }

                        Formatter formatter = new Formatter();

                        while (change.EndsWith(" ", StringComparison.Ordinal) && change.Length > 1)
                        {
                            change = change.Substring(0, change.Length - 1);
                        }

                        // we get all the mode changes for this channel
                        formatter.RewriteBuffer(change, _Network);

                        channel.ChannelMode.ChangeMode("+" + formatter.channelModes);

                        foreach (SimpleMode m in formatter.getMode)
                        {
                            if (_Network.CUModes.Contains(m.Mode) && m.ContainsParameter)
                            {
                                User flagged_user = channel.UserFromName(m.Parameter);
                                if (flagged_user != null)
                                {
                                    flagged_user.ChannelMode.ChangeMode("+" + m.Mode);
                                    flagged_user.ResetMode();
                                }
                            }

                            if (m.ContainsParameter)
                            {
                                switch (m.Mode.ToString())
                                {
                                    case "b":
                                        if (channel.Bans == null)
                                        {
                                            channel.Bans = new List<SimpleBan>();
                                        }
                                        lock (channel.Bans)
                                        {
                                            channel.Bans.Add(new SimpleBan(user, m.Parameter, ""));
                                        }
                                        break;
                                }
                            }
                        }

                        foreach (SimpleMode m in formatter.getRemovingMode)
                        {
                            if (_Network.CUModes.Contains(m.Mode) && m.ContainsParameter)
                            {
                                User flagged_user = channel.UserFromName(m.Parameter);
                                if (flagged_user != null)
                                {
                                    flagged_user.ChannelMode.ChangeMode("-" + m.Mode);
                                    flagged_user.ResetMode();
                                }
                            }

                            if (m.ContainsParameter)
                            {
                                switch (m.Mode.ToString())
                                {
                                    case "b":
                                        if (channel.Bans == null)
                                        {
                                            channel.Bans = new List<SimpleBan>();
                                        }
                                        channel.RemoveBan(m.Parameter);
                                        break;
                                }
                            }
                        }
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
