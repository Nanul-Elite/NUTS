using System.Collections.Concurrent;
using Discord;
using NUTS.Database;
using static NUTS.Program;

namespace NUTS
{
    public enum TargetState
    {
        UnderConsideration = 0,
        Active = 1,
        Complete = 2,
        Paused = 3,
    }

    public class TargetsManager
    {
        private ConcurrentDictionary<string, TargetData> _targets;
        private static ConcurrentDictionary<string, TargetData> Targets
        {
            get { return Program.TargetsManager._targets; }
            set { Program.TargetsManager._targets = value; }
        }

        public TargetsManager(NutsDbContext db) 
        {
            var allTargets = db.TargetDatas.ToList();
            if(allTargets != null && allTargets.Count != 0)
            {
                _targets = new ConcurrentDictionary<string, TargetData>(allTargets.ToDictionary(c => c.Guid, c => c));
            }
        }

        public static TargetData CreateTarget(string name, string reason)
        {
            var target = new TargetData();
            target.Guid = Guid.NewGuid().ToString();
            target.CmdrName = name;
            target.Reason = reason;
            target.Status = (int)TargetState.UnderConsideration;
            target.KilledBy = new List<ulong>();
            return target;
        }

        public static async Task AddTarget(TargetData target)
        {
            if (Targets == null) Targets = new ConcurrentDictionary<string, TargetData>();

            Targets.TryAdd(target.Guid, target);
            try
            {
                DB.TargetDatas.Add(target);
                await DB.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save target: {ex}");
                throw;
            }
        }

        public async Task<IMessage?> GetTargetMessage(ulong threadId, ulong messageId)
        {
            var guild = Client.GetGuild(Config.guildId);
            if (guild == null) return null;

            var threadChannel = guild.GetChannel(threadId) as ITextChannel;
            if (threadChannel == null) return null;

            var message = await threadChannel.GetMessageAsync(messageId);
            return message;
        }

        public static List<TargetData> GetAllTargets() => Targets.Values.ToList();

        public static TargetData? GetTarget(string targetGuid) => Targets.TryGetValue(targetGuid, out var target) ? target : null;
        public static TargetData? GetTargettByName(string targetName) => Targets.FirstOrDefault(c => c.Value.CmdrName == targetName).Value;

        public static async Task UpdateTargetData(TargetData target)
        {
            Targets[target.Guid] = target; // update cache

            DB.TargetDatas.Update(target); // mark as modified in DB
            await DB.SaveChangesAsync();
        }
    }
}
