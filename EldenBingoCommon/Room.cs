﻿using System.Collections.Concurrent;

namespace EldenBingoCommon
{
    public class Room<T> where T : UserInRoom
    {
        public Room(string name)
        {
            Name = name;
            UsersDict = new ConcurrentDictionary<Guid, T>();
            Match = new Match();
        }

        public ICollection<T> Users => UsersDict.Values;
        public Match Match { get; init; }
        public string Name { get; init; }
        public int NumUsers => UsersDict.Count;
        protected ConcurrentDictionary<Guid, T> UsersDict { get; init; }

        public static IList<(int, string)> GetPlayerTeams(IEnumerable<T> players)
        {
            var teams = players.ToLookup(p => p.Team);
            var list = new List<(int, string)>();
            foreach (var team in teams)
            {
                if (team.Key == -1)
                    continue;
                var teamPlayers = team.ToList();
                if (teamPlayers.Count == 1)
                    list.Add(new(team.Key, teamPlayers[0].Nick));
                else if (teamPlayers.Count > 1)
                    list.Add(new(team.Key, getUnifiedName(team.Key, teamPlayers)));
            }

            return list.OrderBy(pt => pt.Item1).ToList();
        }

        public virtual void AddUser(T user)
        {
            UsersDict[user.Guid] = user;
        }

        /// <summary>
        /// Get number of checked squares per team
        /// </summary>
        /// <returns>Team, TeamName, Count</returns>
        public IList<CheckPerTeam> GetCheckedSquaresPerTeam()
        {
            var list = new List<CheckPerTeam>();
            foreach (var pt in GetPlayerTeams())
            {
                list.Add(new CheckPerTeam(pt.Item1, pt.Item2, 0, 0));
            }
            if (Match?.Board == null)
            {
                return list;
            }
            var squaresCountPerTeam = getSquaresPerTeam();
            var bingosPerTeam = getBingosPerTeam();
            for (int i = 0; i < list.Count; ++i)
            {
                if (squaresCountPerTeam.TryGetValue(list[i].Team, out int c))
                {
                    list[i].Squares = c;
                }
                if (bingosPerTeam.TryGetValue(list[i].Team, out int b))
                {
                    list[i].Bingos = b;
                }
            }
            return list;
        }

        private Dictionary<int, int> getSquaresPerTeam()
        {
            var squaresCountPerTeam = new Dictionary<int, int>();
            if (Match?.Board == null)
            {
                return squaresCountPerTeam;
            }
            foreach (var square in Match.Board.Squares)
            {
                if (!square.Team.HasValue)
                    continue;

                if (squaresCountPerTeam.TryGetValue(square.Team.Value, out int c))
                {
                    squaresCountPerTeam[square.Team.Value] = c + 1;
                }
                else
                {
                    squaresCountPerTeam[square.Team.Value] = 1;
                }
            }
            return squaresCountPerTeam;
        }

        private Dictionary<int, int> getBingosPerTeam()
        {
            var bingosPerTeam = new Dictionary<int, int>();
            if (Match?.Board?.Squares == null || Match.Board.Squares.Length != 25)
                return bingosPerTeam;

            for(int x = 0; x < 5; ++x)
            {
                findBingo(bingosPerTeam, x, 0, 0, 1);
            }

            for (int y = 0; y < 5; ++y)
            {
                findBingo(bingosPerTeam, 0, y, 1, 0);
            }
            //Top-left to bottom-right
            findBingo(bingosPerTeam, 0, 0, 1, 1);
            //Bottom-left to top-right
            findBingo(bingosPerTeam, 0, 4, 1, -1);

            return bingosPerTeam;
        }

        private void findBingo(Dictionary<int, int> bingos, int startx, int starty, int dx, int dy)
        {
            if (Match?.Board?.Squares == null || Match.Board.Squares.Length != 25)
                return;

            int index(int x, int y) { return x + y * 5; }
            int x = startx;
            int y = starty;
            int? team = null;
            for(int i = 0; i < 5; ++i)
            {
                var s = Match.Board.Squares[index(x, y)];
                if (!s.Team.HasValue)
                    return;
                if (team.HasValue && team.Value != s.Team.Value)
                    return;
                team = s.Team.Value;
                x += dx;
                y += dy;
            }
            if(team.HasValue)
            {
                if(bingos.TryGetValue(team.Value, out var b) )
                {
                    bingos[team.Value] = b + 1;
                } 
                else
                {
                    bingos[team.Value] = 1;
                }
            }
        }

        public T? GetUser(Guid userGuid)
        {
            return UsersDict.TryGetValue(userGuid, out var user) ? user : null;
        }

        public virtual IEnumerable<T> GetClientsSorted()
        {
            var cmp = new UserComparer<T>();
            return UsersDict.Values.OrderBy(u => u, cmp).ToList();
        }

        public IList<(int, string)> GetPlayerTeams()
        {
            return GetPlayerTeams(Users);
        }

        public virtual bool RemoveUser(T client)
        {
            return UsersDict.Remove(client.Guid, out _);
        }

        public T? RemoveUser(Guid guid)
        {
            UsersDict.Remove(guid, out var obj);
            return obj ?? null;
        }

        private static string getUnifiedName(int team, IList<T> teamPlayers)
        {
            string shortestName = string.Empty;
            for (int i = 0; i < teamPlayers.Count; ++i)
            {
                if (i == 0 || teamPlayers[i].Nick.Length < shortestName.Length)
                    shortestName = teamPlayers[i].Nick;
            }
            //If all names starts with the same sequence (CptDomo, CptDomo2, CptDomo-Spec etc..),
            //use the shortest of these as the team name
            if (teamPlayers.All(p => p.Nick.StartsWith(shortestName)))
                return shortestName;
            return BingoConstants.GetTeamName(team);
        }
    }

    public class CheckPerTeam
    {
        public int Team;
        public string Name;
        public int Squares;
        public int Bingos;

        public CheckPerTeam(int team, string name, int squares, int bingos)
        {
            Team = team;
            Name = name;
            Squares = squares;
            Bingos = bingos;
        }
    }
}