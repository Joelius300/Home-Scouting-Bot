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

            services.Configure<GeneralOptions>(hostBuilderContext.Configuration.GetSection("General"));
            // This doesn't update IOptionsMonitor for some reason
            //services.AddOptions<GeneralOptions>()
            //        .BindConfiguration("General");
        }
    }
}
