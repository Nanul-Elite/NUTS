using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NUTS.Database
{
    public  class Settings
    {
        public int Id { get; set; }
        public List<ulong> PingOnKill { get; set; }
        public List<ulong> PingOnTargetCreated { get; set; }
    }
}
