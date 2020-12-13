using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using HomeScoutingBot.Options;
using HomeScoutingBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HomeScoutingBot
{
    public static class Program
    {
        public static Task Main(string[] args) => Host.CreateDefaultBuilder(args)
                                                      .ConfigureServices(ConfigureServices)
                                                      .UseSystemd()
                                                      .Build()
                                                      .SetupDiscordLogEvents()
                                                      .RunAsync();

        private static void ConfigureServices(HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            services.AddSingleton(sp => new DiscordSocketClient(sp.GetService<DiscordSocketConfig>() ?? new DiscordSocketConfig()));
            services.AddSingleton(sp => new CommandService(sp.GetService<CommandServiceConfig>() ?? new CommandServiceConfig()));

            services.AddHostedService<DiscordService>();

            services.AddSingleton(new DiscordSocketConfig
            {
                GatewayIntents =
                    GatewayIntents.Guilds |
                    GatewayIntents.GuildMembers |
                    GatewayIntents.GuildMessages |
                    GatewayIntents.GuildVoiceStates,
                DefaultRetryMode = RetryMode.RetryRatelimit
            });

            services.AddSingleton(new CommandServiceConfig
            {
                DefaultRunMode = RunMode.Async,
                IgnoreExtraArgs = true
            });

            services.AddOptions<BotOptions>()
                    .Bind(hostBuilderContext.Configuration.GetSection("Bot"));

            services.AddOptions<TextOptions>()
                    .Bind(hostBuilderContext.Configuration.GetSection("Texts"));

            services.AddOptions<GroupOptions>()
                    .Bind(hostBuilderContext.Configuration.GetSection("Group"));

            // As far as I understand, this would be better
            // But it doesn't update on change (see https://github.com/dotnet/runtime/issues/36209#issuecomment-731331916)
            //services.AddOptions<GeneralOptions>()
            //        .BindConfiguration("General");
        }
    }
}
