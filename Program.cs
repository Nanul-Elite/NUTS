using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;
using NUTS.Database;
using Microsoft.EntityFrameworkCore;

namespace NUTS
{
    public class Program
    {
        private DiscordSocketClient _client;

        private ConfigData? _config;
        private string _databasePath = "";
        private string _configPath = "";

        private ConfigHandler _configHandler;
        private TargetsManager _targetsManager;
        private InteractionService _interactions;
        private NutsDbContext _db;

        public const string version = "0.1.0";

        public static ConfigData Config => I._config;
        public static TargetsManager TargetsManager => I._targetsManager;
        public static DiscordSocketClient Client => I._client;
        public static Program I;
        public static NutsDbContext DB => I._db;

        public static IServiceProvider? services;

        public Program()
        {
            string baseDir = AppContext.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data"); // get the Data directory where the config & the DB are at
            Directory.CreateDirectory(dataDir);

            _configPath = Path.Combine(dataDir, "config.json");
            _databasePath = Path.Combine(dataDir, "nuts.db");

            Console.WriteLine($"### Config Path: {_configPath}");
            Console.WriteLine($"### DB Path: {_databasePath}");

            _configHandler = new ConfigHandler();
            LoadConfig();

            if (_config == null || string.IsNullOrEmpty(_config.token))
            {
                Console.Error.WriteLine("Could not load config or Token, shutting down");
                Environment.Exit(1); // Fully exit the program
            }

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
                AlwaysDownloadUsers = true,
            });

            var serviceCollection = new ServiceCollection();

            serviceCollection.AddDbContext<NutsDbContext>(options =>
                options.UseSqlite($"Data Source={_databasePath}"));

            services = serviceCollection.BuildServiceProvider();

            using (var scope = services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<NutsDbContext>();
                bool created = db.Database.EnsureCreated();
            }

            _db = services.GetRequiredService<NutsDbContext>();

            _targetsManager = new TargetsManager(_db);
            _interactions = new InteractionService(_client.Rest);
            _client.Ready += OnReady;
            _client.InteractionCreated += HandleInteraction;
            _client.JoinedGuild += OnJoinGuild;
        }

        static async Task Main(string[] args)
        {
            var bot = new Program();
            I = bot;

            await bot.StartBotAsync();
        }

        public async Task StartBotAsync()
        {
            if (_config == null) return;

            await _client.LoginAsync(Discord.TokenType.Bot, _config.token);
            await _client.StartAsync();

            Console.WriteLine("Brrrt");
            foreach (var item in _client.Guilds)
            {
                Console.WriteLine(item.Name);
            }

            await Task.Delay(-1);
        }

        public bool LoadConfig()
        {
            _config = _configHandler.ParseConfig(_configPath);
            return _config != null;
        }

        private async Task OnReady()
        {
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            await _interactions.RegisterCommandsToGuildAsync(Config.guildId); 
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);

            if (interaction is SocketAutocompleteInteraction auto)
            {
                // Targets Autocomplete
                if (auto.Data.CommandName == "edit_target" &&
                auto.Data.Current.Name == "target-guid")
                {
                    var focused = auto.Data.Current.Value?.ToString() ?? "";

                    var results = TargetsManager.GetAllTargets()
                        .Where(c => c.CmdrName.Contains(focused, StringComparison.OrdinalIgnoreCase))
                        .Take(25)
                        .Select(c => new AutocompleteResult(c.CmdrName, c.Guid));

                    await auto.RespondAsync(results);
                }

                // Leaderboard autocomplete
                if (auto.Data.CommandName == "remove_leaderboard" &&
                    auto.Data.Current.Name == "leaderboard")
                {
                    var focused = auto.Data.Current.Value?.ToString() ?? "";

                    var list = DB.LeaderboardPosts.ToList();

                    var results = list.Where(p => p.BoardName.Contains(focused, StringComparison.OrdinalIgnoreCase))
                                       .Take(25)
                                       .Select(p => new AutocompleteResult(p.BoardName, p.BoardName));

                    await auto.RespondAsync(results);
                    return;
                }
                return;
            }

            await _interactions.ExecuteCommandAsync(context, null);
        }

        private async Task OnJoinGuild(SocketGuild guild)
        {
            var defaultChannel = guild.SystemChannel;
            if (defaultChannel != null)
            {
                await defaultChannel.SendMessageAsync($"NUTS go brrrrrrrt");
            }
        }
    }
}
