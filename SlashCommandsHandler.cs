using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using NUTS.Database;
using System.Globalization;

namespace NUTS
{
    public class SlashCommandsHandler : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("create_target", "Create a target")]
        public async Task CreateTarget([Summary(description: "CMDR Name")] string targetName, [Summary(description: "Reason")] string reason)
        {
            ulong forumChannelId = Program.Config.targetsChannel;
            var forumChannel = Context.Client.GetChannel(forumChannelId) as SocketForumChannel;

            if (forumChannel == null)
            {
                await RespondAsync("Forum channel not found.");
                return;
            }

            var target = TargetsManager.CreateTarget(targetName, reason);

            var thread = await forumChannel.CreatePostAsync(
                title: $"Target: CMDR {targetName}",
                embed: await EmbedFactory.BuildTargetEmbed(target)
            );

            // Grab the first message ID
            var starterMessage = await thread.GetMessageAsync(thread.Id);
            target.MessageId = starterMessage.Id;
            target.ThreadId = thread.Id;

            await TargetsManager.AddTarget(target);

            string url = $"https://discord.com/channels/{Context.Guild.Id}/{thread.Id}";
            await RespondAsync($"Target Created: {url}");

            var settings = Program.DB.Settings.FirstOrDefault();
            if (settings != null)
            {
                List<ulong> userIds = settings.PingOnTargetCreated.ToList();
                if (userIds != null && userIds.Count > 0)
                {
                    string mentions = string.Join(" ", userIds.Select(id => $"<@{id}>"));
                    await thread.SendMessageAsync($"{mentions}\nThere is a new Target");
                }
            }
        }

        // TODO: seperate kill report so timestamp is a must
        [SlashCommand("edit_target", "Edit an existing target")]
        public async Task EditTarget(
                            [Autocomplete] string targetGuid,
                            TargetState? state = null,
                            string? name = null,
                            string? sentence = null,
                            string? thumbUrl = null,
                            string? squad = null,
                            string? affiliation = null,
                            string? intel = null,
                            string? inaraUrl = null,
                            string? gankersUrl = null,
                            IUser? killedBy = null,
                            [Summary(description: "UTC(game time) Timestamp as yyyy-MM-dd HH:mm - e.g: 2025-08-26 17:36")]string? timestemp = null
                            )
        {
            await DeferAsync(ephemeral: true);

            var targetData = TargetsManager.GetTarget(targetGuid);
            if (targetData == null)
            {
                await FollowupAsync("❌ Target not found.", ephemeral: true);
                return;
            }

            if (state != null) targetData.Status = (int)state;
            if (killedBy != null)
            {
                targetData.KilledBy.Add(killedBy.Id);

                DateTime ts = timestemp != null ? DateTime.ParseExact(timestemp, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).ToUniversalTime() : DateTime.UtcNow;
                
                var newKillRecord = new KillRecord()
                {
                    KillerUserId = killedBy.Id,
                    TargetGuid = targetGuid,
                    TargetDataId = targetData.Id,
                    TargetData = targetData,
                    Timestamp = ts,
                };

                await Program.DB.KillRecords.AddAsync(newKillRecord);
            }

            if (name != null) targetData.CmdrName = name;
            if (sentence != null) targetData.Sentence = sentence;
            if (thumbUrl != null) targetData.ThumbUrl = thumbUrl;
            if (squad != null) targetData.Squad = squad;
            if (affiliation != null) targetData.Affiliation = affiliation;
            if (intel != null) targetData.Intel = intel;
            if (inaraUrl != null) targetData.InaraUrl = inaraUrl;
            if (gankersUrl != null) targetData.GankersUrl = gankersUrl;

            await TargetsManager.UpdateTargetData(targetData);

            var guild = Context.Guild;
            var threadChannel = guild.GetChannel(targetData.ThreadId) as IThreadChannel;

            if (threadChannel != null)
            {
                if (name != null)
                    await threadChannel.ModifyAsync(t => t.Name = $"Target: CMDR {name}");

                var message = await threadChannel.GetMessageAsync(targetData.MessageId) as IUserMessage;
                if (message != null)
                {
                    var embed = await EmbedFactory.BuildTargetEmbed(targetData);
                    await message.ModifyAsync(m => m.Embed = embed);

                    await FollowupAsync("✅ Target updated.", ephemeral: true);

                    if (killedBy != null)
                    {
                        await LeaderBoardsManager.UpdateLeaderBoards(Program.Client);

                        var settings = Program.DB.Settings.FirstOrDefault();
                        if (settings != null)
                        {
                            List<ulong> userIds = settings.PingOnKill.ToList();
                            if (userIds != null && userIds.Count > 0)
                            {
                                string mentions = string.Join(" ", userIds.Select(id => $"<@{id}>"));
                                await threadChannel.SendMessageAsync($"{mentions}\nThis Target Has Been Killed");
                            }
                        }
                    }

                    return;
                }
            }

            await FollowupAsync("❌ Could not find thread or message.", ephemeral: true);
        }

        [SlashCommand("create_leaderboard", "Post a leaderboard into this thread")]
        public async Task CreateLeaderboard([Summary("days", "Number of days to look back (0 = All Time, -1 = Ranks)")] int days, [Summary("name", "The name for this leaderboard")] string name)
        {
            await DeferAsync(ephemeral: true);

            var channel = Context.Channel;
            if (channel is not IThreadChannel threadChannel)
            {
                await FollowupAsync("❌ This command must be used inside a thread.", ephemeral: true);
                return;
            }

            string text = await LeaderBoardsManager.CreateLeaderBoardText(days, DateTime.UtcNow);

            var message = await threadChannel.SendMessageAsync(text);

            // Save to database
            var post = new LeaderboardPost
            {
                ChannelId = threadChannel.Id,
                MessageId = message.Id,
                BoardName = name,
                Type = days,
                LastUpdated = DateTime.UtcNow
            };

            Program.DB.LeaderboardPosts.Add(post);
            await Program.DB.SaveChangesAsync();

            await FollowupAsync($"✅ Leaderboard posted in thread: {threadChannel.Mention}", ephemeral: true);
        }

        [SlashCommand("remove_leaderboard", "Remove a leaderboard post from the database")]
        public async Task RemoveLeaderboard([Autocomplete] string leaderboard)
        {
            await DeferAsync(ephemeral: true);

            var post = Program.DB.LeaderboardPosts
                                 .FirstOrDefault(p => p.BoardName == leaderboard);

            if(post != null)
            {
                await Context.Channel.DeleteMessageAsync(post.MessageId);
            }
            else if (post == null)
            {
                await FollowupAsync("❌ No leaderboard found with that title.", ephemeral: true);
                return;
            }

            Program.DB.LeaderboardPosts.Remove(post);
            await Program.DB.SaveChangesAsync();

            await FollowupAsync($"✅ Leaderboard '{leaderboard}' removed from the database.", ephemeral: true);
        }

        [SlashCommand("update_leaderboards", "Update the Leaderboards")]
        public async Task UpdateBoards()
        {
            await DeferAsync(ephemeral: true);
            await LeaderBoardsManager.UpdateLeaderBoards(Program.Client);
            await FollowupAsync("Leaderboards Updated", ephemeral: true);
        }

        [SlashCommand("toggle_pings", "Add or remove yourself from ping lists")]
        public async Task TogglePing(bool pingOnKill, bool pingOnTargetCreated)
        {
            var userId = Context.User.Id;

            var settings = Program.DB.Settings.FirstOrDefault();
            if (settings == null)
            {
                settings = new Settings
                {
                    PingOnKill = new List<ulong>(),
                    PingOnTargetCreated = new List<ulong>()
                };
                Program.DB.Settings.Add(settings);
            }

            if (pingOnKill)
            {
                if (!settings.PingOnKill.Contains(userId))
                    settings.PingOnKill.Add(userId);
            }
            else
            {
                settings.PingOnKill.Remove(userId);
            }

            if (pingOnTargetCreated)
            {
                if (!settings.PingOnTargetCreated.Contains(userId))
                    settings.PingOnTargetCreated.Add(userId);
            }
            else
            {
                settings.PingOnTargetCreated.Remove(userId);
            }

            await Program.DB.SaveChangesAsync();

            string actionKill = pingOnKill ? "added to" : "removed from";
            string actionTarget = pingOnTargetCreated ? "added to" : "removed from";

            await RespondAsync(
                $"You {Context.User.Mention} have been {actionKill} the Ping On Kill list and {actionTarget} the Ping On Target Created list.",
                ephemeral: true
            );
        }

        // to use this there needs to be at least 1 target in the DB
        //[SlashCommand("populate_test_kills", "Populate KillRecords for testing rank functionality")]
        public async Task PopulateTestKills()
        {
            var channel = (ITextChannel)Context.Channel;
            var users = await channel.GetUsersAsync().FlattenAsync();
            var selectedUsers = users.Take(5).ToList();

            foreach (var item in selectedUsers)
            {
                Console.WriteLine(item.DisplayName);
            }

            if (!selectedUsers.Any())
            {
                await RespondAsync("No users available in this channel.");
                return;
            }

            DateTime now = DateTime.UtcNow;
            int totalUsers = selectedUsers.Count;

            for (int i = 0; i < totalUsers; i++)
            {
                var user = selectedUsers[i];

                // Each user will get a few kills spaced by seconds to simulate time passing
                List<DateTime> killTimes = new List<DateTime>();

                if (i < 3)
                {
                    // Top Gun candidates
                    killTimes.Add(now.AddDays(-33)); // Rank 1
                    killTimes.Add(now.AddDays(-30)); // Rank 1
                    killTimes.Add(now.AddDays(-24)); // Rank 1 -> Rank 2
                    killTimes.Add(now.AddDays(-22)); // Rank 2
                    killTimes.Add(now.AddDays(-20)); // Rank 2 -> Rank 3

                    if (i == 1)
                        killTimes.Add(now.AddDays(-6));
                    if (i == 0)
                        killTimes.Add(now.AddDays(-5));
                }
                else if (i == 3)
                {
                    // Other users distributed in Rank 1
                    killTimes.Add(now.AddDays(-20));
                    killTimes.Add(now.AddDays(-10));
                    killTimes.Add(now.AddDays(-6.5f));
                }
                else if(i == 4)
                {
                    //Rank 2
                    killTimes.Add(now.AddDays(-24));
                    killTimes.Add(now.AddDays(-20));
                    killTimes.Add(now.AddDays(-16));
                    killTimes.Add(now.AddDays(-13.8f));
                }

                foreach (var killTime in killTimes)
                {
                    var newKill = new KillRecord()
                    {
                        KillerUserId = user.Id,
                        Timestamp = killTime,
                        TargetGuid = Guid.NewGuid().ToString(), // Dummy data
                        TargetDataId = 1,
                    };
                    Program.DB.KillRecords.Add(newKill);
                }
            }

            await Program.DB.SaveChangesAsync();

            await LeaderBoardsManager.UpdateLeaderBoards(Program.Client);

            await RespondAsync("Test kills populated! Check ranks now.");
        }
    }
}