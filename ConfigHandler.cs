using Newtonsoft.Json;

namespace NUTS
{
    public class ConfigHandler
    {
        public ConfigData? ParseConfig(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"Config file not found at: {path}");
                return null;
            }

            using (StreamReader sr = new StreamReader(path))
            {
                string json = sr.ReadToEnd();
                ConfigData config = JsonConvert.DeserializeObject<ConfigData>(json);

                if (config != null)
                    return config;
            }

            return null;
        }
    }

    public class ConfigData
    {
        public string token { get; set; }
        public ulong targetsChannel { get; set; }
        public ulong guildId {  get; set; }
        public ulong rank3RoleId { get; set; }
        public ulong rank2RoleId { get; set; }
        public ulong rank1RoleId { get; set; }

    }
}