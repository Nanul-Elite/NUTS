using Discord;
using NUTS.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NUTS
{
    public class EmbedFactory
    {
        public static async Task<Embed> BuildTargetEmbed(TargetData target)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle($"Target: CMDR {target.CmdrName}");

            Color color = new Color();
            switch (target.Status)
            {
                case (int)TargetState.UnderConsideration: color = Color.Blue; break;
                case (int)TargetState.Active: color = Color.Red; break;
                case (int)TargetState.Complete: color = Color.Green; break;
                case (int)TargetState.Paused: color = Color.DarkGrey; break;
            }
            embedBuilder.WithColor(color);

            if (!string.IsNullOrWhiteSpace(target.ThumbUrl))
                embedBuilder.WithThumbnailUrl(target.ThumbUrl);

            string statusText = Enum.GetName(typeof(TargetState), target.Status) ?? "Unknown";
            statusText = System.Text.RegularExpressions.Regex.Replace(statusText, "(\\B[A-Z])", " $1");

            var sb = new StringBuilder();
            sb.AppendLine($"**Status:** {statusText}");
            sb.AppendLine($"**Reason:** {target.Reason ?? "N/A"}");
            sb.AppendLine($"**Sentence:** {target.Sentence ?? "N/A"}");
            sb.AppendLine();

            sb.AppendLine($"**Squad:** {target.Squad ?? "N/A"}");
            sb.AppendLine($"**Affiliation:** {target.Affiliation ?? "N/A"}");
            sb.AppendLine();

            sb.AppendLine($"**Intel:** {target.Intel ?? "N/A"}");
            if(!string.IsNullOrEmpty(target.InaraUrl))
                sb.AppendLine($"[Inara Page]({target.InaraUrl})");
            if (!string.IsNullOrEmpty(target.GankersUrl))
                sb.AppendLine($"[Gankers Page]({target.GankersUrl})");
            sb.AppendLine();

            if (target.KilledBy != null && target.KilledBy.Count > 0)
            {
                sb.AppendLine("**Killed By:**");
                var grouped = target.KilledBy
                    .GroupBy(k => k)
                    .Select(g => new { UserId = g.Key, Count = g.Count() });

                foreach (var entry in grouped)
                {
                    string countText = entry.Count > 1 ? $" x{entry.Count}" : "";
                    sb.AppendLine($"- <@{entry.UserId}>{countText}");
                }
            }
            else
            {
                sb.AppendLine("**Killed By:** None");
            }

            embedBuilder.WithDescription(sb.ToString());

            embedBuilder.WithFooter("Nefarious User Termination Services");

            return embedBuilder.Build();
        }
    }
}
