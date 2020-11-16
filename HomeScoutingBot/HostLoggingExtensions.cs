using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace HomeScoutingBot
{
    internal static class HostLoggingExtensions
    {
        /// <summary>
        /// Redirects discord logging events to <see cref="ILogger"/> instances.
        /// </summary>
        /// <param name="host"></param>
        public static IHost SetupDiscordLogEvents(this IHost host)
        {
            IServiceProvider sp = host.Services;
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            DiscordSocketClient client = sp.GetRequiredService<DiscordSocketClient>();
            client.Log += (message) => Log<DiscordSocketClient>(loggerFactory, message);

            CommandService commandService = sp.GetRequiredService<CommandService>();
            commandService.Log += (message) => Log<CommandService>(loggerFactory, message);

            return host;
        }

        private static Task Log<T>(ILoggerFactory loggerFactory, LogMessage message)
        {
            ILogger<T> logger = loggerFactory.CreateLogger<T>();

            if (message.Exception is not null)
            {
                logger.LogError(message.Exception, message.Message);
            }
            else
            {
                logger.Log(LogLevelFromSeverity(message.Severity), message.Message);
            }

            return Task.CompletedTask;
        }

        private static LogLevel LogLevelFromSeverity(LogSeverity severity) => severity switch
        {
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Critical => LogLevel.Critical,
            _ => throw new NotSupportedException($"Unsupported LogSeverity {severity}")
        };
    }
}
