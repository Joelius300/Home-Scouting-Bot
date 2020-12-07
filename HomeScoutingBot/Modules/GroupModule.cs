using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using HomeScoutingBot.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeScoutingBot.Modules
{
    [RequireUserPermission(GuildPermission.ManageChannels)]
    public class GroupModule : ModuleBase<SocketCommandContext>
    {
        private readonly TextOptions _textConfig;
        private readonly GroupOptions _groupConfig;
        private readonly ILogger<GroupModule> _logger;

        public GroupModule(IOptionsSnapshot<TextOptions> textConfig, IOptionsSnapshot<GroupOptions> groupConfig, ILogger<GroupModule> logger)
        {
            _textConfig = textConfig.Value;
            _groupConfig = groupConfig.Value;
            _logger = logger;
        }

        [Command(nameof(Setup))] // Should probably be RunMode Async but for that the scoping stuff need to be reworked
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.SendMessages)]
        public async Task Setup(int amount)
        {
            if (amount < 2)
                throw new ArgumentOutOfRangeException(nameof(amount));

            RequestOptions requestOptions = new RequestOptions
            {
                RetryMode = RetryMode.RetryRatelimit
            };

            for (int i = 1; i <= amount; i++)
            {
                string name = string.Format(_groupConfig.GroupChannelNameTemplate, i);
                ICategoryChannel category = await Context.Guild.CreateCategoryChannelAsync(name, options: requestOptions);

                Action<GuildChannelProperties> assignCategoryId = c => c.CategoryId = category.Id;
                await Context.Guild.CreateTextChannelAsync(name, assignCategoryId, requestOptions);
                await Context.Guild.CreateVoiceChannelAsync(name, assignCategoryId, requestOptions);
            }

            await ReplyAsync(string.Format(_textConfig.GroupsCreated, amount));
        }

        [Command(nameof(Teardown))]
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.SendMessages)]
        public async Task Teardown()
        {
            // TODO Maybe confirm with user

            RequestOptions requestOptions = new RequestOptions
            {
                RetryMode = RetryMode.RetryRatelimit
            };

            int count = 0;
            foreach (SocketGuildChannel channel in GetAllGroupChannels())
            {
                await channel.DeleteAsync(requestOptions);
                if (channel is ICategoryChannel)
                {
                    count++;
                }
            }

            await ReplyAsync(string.Format(_textConfig.GroupsDeleted, count));
        }

        // Returns Voice, Text AND Categories
        private IEnumerable<SocketGuildChannel> GetAllGroupChannels()
        {
            const string Placeholder = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // regex-safe placeholder used for allowing regex-unsafe name-templates

            string nameRegex = string.Format(_groupConfig.GroupChannelNameTemplate, Placeholder);
            nameRegex = Regex.Escape(nameRegex);
            nameRegex = nameRegex.Replace(Placeholder, "\\d+");

            Predicate<string> matchName = name => Regex.IsMatch(name, $"^{nameRegex}$", RegexOptions.IgnoreCase);
            IEnumerable<SocketCategoryChannel> groupCategories = Context.Guild.CategoryChannels
                                                                              .Where(c => matchName(c.Name));

            foreach (SocketCategoryChannel categoryChannel in groupCategories)
            {
                foreach (SocketGuildChannel channel in categoryChannel.Channels)
                {
                    yield return channel;
                }

                yield return categoryChannel;
            }
        }
    }
}
