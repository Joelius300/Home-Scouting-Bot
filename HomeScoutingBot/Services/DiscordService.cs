using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using HomeScoutingBot.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Threading;

namespace HomeScoutingBot.Services
{
    public class DiscordService : IHostedService
    {
        private readonly IOptionsMonitor<GeneralOptions> _config;
        private readonly CommandService _commandService;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly ILogger<DiscordService> _logger;

        public DiscordService(IServiceProvider services, DiscordSocketClient client, CommandService commandService, ILogger<DiscordService> logger, IOptionsMonitor<GeneralOptions> config)
        {
            _commandService = commandService;
            _client = client;
            _services = services;
            _logger = logger;
            _config = config;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // register modules that are public and inherit ModuleBase<T>
            await _commandService.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

            _commandService.CommandExecuted += CommandExecutedAsync;
            _client.MessageReceived += MessageReceivedAsync;

            await _client.LoginAsync(TokenType.Bot, _config.CurrentValue.Token);
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

            string prefix = _config.CurrentValue.Prefix;
            int argPos = 0;
            if (!(message.HasMentionPrefix(_client.CurrentUser, ref argPos) || // for @'ing the bot
                  message.HasStringPrefix(prefix, ref argPos)))                // for using the prefix
            {
                return;
            }

            ICommandContext context = new SocketCommandContext(_client, message);

            await _commandService.ExecuteAsync(context, argPos, _services);
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
            {
                _logger.LogDebug("Command '{0}' executed for '{1}'.", command.Value.Name, context.User.Username);
                return;
            }

            _logger.LogWarning("Command failed to execute for '{0}': {1}", context.User.Username, result.ErrorReason);

            if (command.IsSpecified)
            {
                // command exists but failed to execute
                await context.Channel.SendMessageAsync(string.Format(_config.CurrentValue.ErrorMessage, context.User.Username, result));
            }
        }
    }
}
