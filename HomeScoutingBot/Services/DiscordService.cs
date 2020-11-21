using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using HomeScoutingBot.Options;

namespace HomeScoutingBot.Services
{
    public class DiscordService : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commandService;
        private readonly ILogger<DiscordService> _logger;
        private readonly IOptionsMonitor<BotOptions> _botConfig;
        private readonly IOptionsMonitor<TextOptions> _textConfig;

        public DiscordService(IServiceProvider services, DiscordSocketClient client, CommandService commandService, ILogger<DiscordService> logger, IOptionsMonitor<BotOptions> botConfig, IOptionsMonitor<TextOptions> textConfig)
        {
            _services = services;
            _client = client;
            _commandService = commandService;
            _logger = logger;
            _botConfig = botConfig;
            _textConfig = textConfig;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // register modules that are public and inherit ModuleBase<T>
            await _commandService.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

            _commandService.CommandExecuted += CommandExecutedAsync;
            _client.MessageReceived += MessageReceivedAsync;

            await _client.LoginAsync(TokenType.Bot, _botConfig.CurrentValue.Token);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _commandService.CommandExecuted -= CommandExecutedAsync;
            _client.MessageReceived -= MessageReceivedAsync;

            await _client.LogoutAsync();
            await _client.StopAsync();
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            if (rawMessage is not SocketUserMessage message ||
                message.Source != MessageSource.User)
            {
                return;
            }

            string prefix = _botConfig.CurrentValue.Prefix;
            int argPos = 0;
            if (!(message.HasMentionPrefix(_client.CurrentUser, ref argPos) || // for @'ing the bot
                  message.HasStringPrefix(prefix, ref argPos)))                // for using the prefix
            {
                return;
            }

            ICommandContext context = new SocketCommandContext(_client, message);

            using (IServiceScope scope = _services.CreateScope())
            {
                await _commandService.ExecuteAsync(context, argPos, scope.ServiceProvider);
            }
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
            {
                _logger.LogDebug("Command '{0}' executed for '{1}'.", command.Value.Name, context.User.Username);
                return;
            }

            _logger.LogWarning("Command failed to execute for '{0}': {1}", context.User.Username, result.ErrorReason);

            if (command.IsSpecified) // command exists but failed to execute
            {
                string name = context.User.Username;
                if (context.User is IGuildUser guildUser)
                {
                    name = guildUser.Nickname ?? name;
                }

                await context.Channel.SendMessageAsync(string.Format(_textConfig.CurrentValue.CommandExecutionFailed, name, result));
            }
        }
    }
}
