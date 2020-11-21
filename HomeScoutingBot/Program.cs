using System.Threading.Tasks;
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
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton<CommandService>();

            services.AddHostedService<DiscordService>();

            services.AddOptions<BotOptions>()
                    .Bind(hostBuilderContext.Configuration.GetSection("Bot"));

            services.AddOptions<TextOptions>()
                    .Bind(hostBuilderContext.Configuration.GetSection("Texts"));

            // As far as I understand, this would be better
            // But it doesn't update on change (see https://github.com/dotnet/runtime/issues/36209#issuecomment-731331916)
            //services.AddOptions<GeneralOptions>()
            //        .BindConfiguration("General");
        }
    }
}
