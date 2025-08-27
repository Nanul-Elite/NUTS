using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NUTS.Program;

namespace NUTS
{
    public enum Rank
    {
        None,
        Rank1,
        Rank2,
        Rank3Candidate, // has the requirments to be rank 3 but someone else has more kills or more recent ones
        Rank3,
    }

    public class LeaderBoardsManager
    {
        public static async Task<string> CreateLeaderBoardText(int days, DateTime? timestamp)
        {
            StringBuilder sb = new StringBuilder();
            if (days >= 0)
                return await CreateCountLeaderboard(sb, days, timestamp);
            else
                return await CreateRanksLeaderboard(sb, timestamp);
        }

        private static async Task<string> CreateRanksLeaderboard(StringBuilder sb, DateTime? timestamp)
        {
            List<(ulong, Rank, int)> players = new List<(ulong, Rank, int)>();

            if (DB.KillRecords != null && DB.KillRecords.Count() > 0)
            {
                var playersList = DB.KillRecords.ToList()
                                .Select(k => k.KillerUserId)
                                .Distinct()
                                .ToList();

                foreach (var userId in playersList)
                {
                    var userRankData = GetRank(userId);
                    players.Add((userId, userRankData.Item1, userRankData.Item2));
                }

                // Demote TopGun candidates that are not the highest scorer
                var topGunCandidates = players
                                            .Where(p => p.Item2 == Rank.Rank3)
                                            .Select(p => new
                                            {
                                                Player = p,
                                                LastKill = DB.KillRecords.ToList()
                                                             .Where(k => k.KillerUserId == p.Item1)
                                                             .Max(k => k.Timestamp) // get their latest kill
                                            })
                                            .OrderByDescending(x => x.Player.Item3)   // first by kills
                                            .ThenByDescending(x => x.LastKill)        // then by latest kill
                                            .Select(x => x.Player)
                                            .ToList();

                if (topGunCandidates.Count > 1)
                {
                    var topgun = topGunCandidates[0];
                    for (int i = 1; i < topGunCandidates.Count; i++)
                    {
                        var userIdToDemote = topGunCandidates[i].Item1;
                        int idx = players.FindIndex(p => p.Item1 == userIdToDemote);
                        if (idx != -1)
                            players[idx] = (players[idx].Item1, Rank.Rank3Candidate, players[idx].Item3 - topgun.Item3);
                    }
                }
            }

            // Group by rank
            var grouped = players
                .GroupBy(p => p.Item2)
                .OrderByDescending(g => g.Key); // Top Gun First

            long updateUnixTime = new DateTimeOffset((timestamp == null ? DateTime.UtcNow : (DateTime)timestamp)).ToUnixTimeSeconds();
            string discordTime = $"<t:{updateUnixTime}:f>";

            sb.AppendLine("## __**Current Ranks**__");
            sb.AppendLine($"Last Updated: {discordTime}\n");

            foreach (var group in grouped)
            {
                if(group.Key == Rank.None) continue;

                string rankTitle = group.Key switch
                {
                    Rank.Rank1 => $"🥉 <@&{Config.rank1RoleId}>:",
                    Rank.Rank2 => $"🥈 <@&{Config.rank2RoleId}>:",
                    Rank.Rank3Candidate => $"🥇 <@&{Config.rank2RoleId}> (eligible for <@&{Config.rank3RoleId}>):",
                    Rank.Rank3 => $"🏅 <@&{Config.rank3RoleId}>:",
                    _ => "",
                };

                sb.AppendLine($"{rankTitle}");
                foreach (var player in group.OrderByDescending(p => p.Item3))
                {
                    // Calculate next demotion time
                    var lastKill = DB.KillRecords!.ToList()
                                     .Where(k => k.KillerUserId == player.Item1)
                                     .OrderByDescending(k => k.Timestamp)
                                     .FirstOrDefault();

                    string demotionText = "";
                    if (lastKill != null)
                    {
                        int demotionDays = player.Item2 switch
                        {
                            Rank.Rank1 => 7,
                            Rank.Rank2 => 14,
                            Rank.Rank3Candidate => 14,
                            Rank.Rank3 => 28,
                            _ => 0
                        };

                        if (demotionDays > 0)
                        {
                            var demotionTime = lastKill.Timestamp.AddDays(demotionDays);
                            long unixTime = ((DateTimeOffset)demotionTime).ToUnixTimeSeconds();
                            demotionText = $" - reset <t:{unixTime}:R>";
                        }
                    }

                    string kills = $"{player.Item3} kills";
                    if(player.Item2 == Rank.Rank3Candidate)
                        kills = $"{Math.Max(Math.Abs(player.Item3), 1)} more kills for <@&{Config.rank3RoleId}>";

                    sb.AppendLine($"- <@{player.Item1}> ({kills}) {demotionText}");
                }
                sb.AppendLine();
            }

            //await AssignRankRoles(players);

            return sb.ToString();
        }

        private static async Task<string> CreateCountLeaderboard(StringBuilder sb, int days, DateTime? timestamp)
        {
            string title;
            if (days == 0)
                title = "All Time Leaderboard";
            else if (days % 7 == 0)
                title = $"{days / 7} Week{(days / 7 > 1 ? "s" : "")} Leaderboard";
            else
                title = $"{days} Day{(days > 1 ? "s" : "")} Leaderboard";
            
            long unixTime = new DateTimeOffset((timestamp == null ? DateTime.UtcNow : (DateTime)timestamp)).ToUnixTimeSeconds();
            string discordTime = $"<t:{unixTime}:f>";

            sb.AppendLine($"## __**{title}**__");
            sb.AppendLine($"Last Updated: {discordTime}");

            if (days == 0) days = 365 * 20;

            if (DB.KillRecords != null && DB.KillRecords.Count() > 0)
            {
                var list = DB.KillRecords.ToList();

                var kills = list.Where(kr => kr.Timestamp >= DateTime.UtcNow.AddDays(-days))
                                .GroupBy(k => k.KillerUserId)
                                .Select(g => new { UserId = g.Key, Count = g.Count() })
                                .OrderByDescending(x => x.Count)
                                .ToList();

                if (kills.Count == 0)
                    sb.AppendLine("\nY'all are slacking around !");

                for (int i = 0; i < kills.Count; i++)
                {
                    string name = $"<@{kills[i].UserId}>";
                    string line = $"- {name}: {kills[i].Count}";
                    sb.AppendLine(line);
                }
            }
            else
            {
                sb.AppendLine("\nY'all are slacking around !");
            }

            return sb.ToString();
        }

        public static (Rank, int) GetRank(ulong playerId)
        {
            var records = DB.KillRecords.ToList();

            var kills = records
                .Where(k => k.KillerUserId == playerId)
                .OrderBy(k => k.Timestamp) // Oldes first
                .ToList();

            if (!kills.Any()) return (Rank.None, 0);

            var lastKillTimestamp = DateTime.MinValue;
            var killsSincePromo = 0;
            var currentRank = Rank.None;

            for (int i = 0; i < kills.Count; i++)   
            {
                var kill = kills[i];
                var timeSince = (kill.Timestamp - lastKillTimestamp).Days;

                if (currentRank == Rank.None)
                {
                    currentRank = Rank.Rank1;
                    killsSincePromo = 0;
                }
                else if (currentRank == Rank.Rank1)
                {
                    if (timeSince < 7)
                    {
                        killsSincePromo++;
                        if(killsSincePromo >= 2) 
                        {
                            currentRank = Rank.Rank2;
                            killsSincePromo = 0;
                        }
                    }
                    else
                    {
                        killsSincePromo = 0;
                    }
                }
                
                else if (currentRank == Rank.Rank2)
                {
                    if (timeSince < 14)
                    {
                        killsSincePromo++;
                        if (killsSincePromo >= 2)
                        {
                            currentRank = Rank.Rank3;
                            killsSincePromo = 0;
                        }
                    }
                    else
                    {
                        killsSincePromo = 0;

                        if (timeSince >= 14 + 7)
                            currentRank = Rank.None;
                        else if(timeSince >= 14)
                            currentRank = Rank.Rank1;
                    }
                }
                else if (currentRank == Rank.Rank3)
                {
                    if(timeSince < 28)
                    {
                        killsSincePromo++;
                    }
                    else if(timeSince >= 28 + 14 + 7)
                        currentRank = Rank.None;
                    else if(timeSince >= 28 + 14)
                        currentRank = Rank.Rank1;
                    else if(timeSince >= 28)
                        currentRank = Rank.Rank2;
                }

                lastKillTimestamp = kill.Timestamp;
            }

            var daysSinceLast = (lastKillTimestamp - DateTime.UtcNow).Days;
            if(currentRank == Rank.Rank1 && daysSinceLast >= 7)
            {
                killsSincePromo = 0;
                currentRank = Rank.None;
            }
            else if (currentRank == Rank.Rank2 && daysSinceLast >= 14)
            {
                killsSincePromo = 0;

                if (daysSinceLast >= 14 + 7)
                    currentRank = Rank.None;
                else if (daysSinceLast >= 14)
                    currentRank = Rank.Rank1;
            }
            else if (currentRank == Rank.Rank3 && daysSinceLast >= 28)
            {
                killsSincePromo = 0;

                if (daysSinceLast >= 28 + 14 + 7)
                    currentRank = Rank.None;
                else if (daysSinceLast >= 28 + 14)
                    currentRank = Rank.Rank1;
                else if (daysSinceLast >= 28)
                    currentRank = Rank.Rank2;
            }

            return (currentRank, killsSincePromo);
        }

        public static async Task AssignRankRoles(List<(ulong, Rank, int)> players)
        {
            var guild = Client.GetGuild(Config.guildId);
            var topGun = await guild.GetRoleAsync(Config.rank3RoleId);
            var prime = await guild.GetRoleAsync(Config.rank2RoleId);
            var ace = await guild.GetRoleAsync(Config.rank1RoleId);

            foreach ( var p in players )
            {
                // need to do some varifications here to make sure the bot can assign the role to the user, or to give the bot admin perms
                var user = guild.GetUser(p.Item1);
                await user.RemoveRolesAsync(new [] { topGun, prime, ace });
                switch (p.Item2)
                {
                    case Rank.Rank1:
                        await user.AddRoleAsync(ace);
                        break;
                    case Rank.Rank2: case Rank.Rank3Candidate:
                        await user.AddRoleAsync(prime);
                        break;
                    case Rank.Rank3:
                        await user.AddRoleAsync(topGun);
                        break;
                }
            }
        }

        public static async Task UpdateLeaderBoards(DiscordSocketClient client)
        {
            var posts = DB.LeaderboardPosts.ToList();

            foreach (var post in posts)
            {
                post.LastUpdated = DateTime.UtcNow;

                string text = await CreateLeaderBoardText(post.Type, post.LastUpdated);

                var channel = client.GetChannel(post.ChannelId) as IThreadChannel;
                if (channel == null)
                {
                    continue;
                }

                var msg = await channel.GetMessageAsync(post.MessageId) as IUserMessage;
                if (msg != null)
                {
                    await msg.ModifyAsync(m => m.Content = text);
                }
            }

            await DB.SaveChangesAsync();
        }
    }
}
