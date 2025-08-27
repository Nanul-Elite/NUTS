using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NUTS.Database
{
    public class LeaderboardPost
    {
        public int Id { get; set; }
        public ulong ChannelId { get; set; }   // forum channel ID
        public ulong MessageId { get; set; }   // root message inside the thread
        public string BoardName { get; set; }
        public int Type { get; set; } // e.g., 0 - AllTime, >0 - TimeFrame where the int is the number of days to the past
        public DateTime? LastUpdated { get; set; }
    }
}
