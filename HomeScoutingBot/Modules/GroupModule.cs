using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
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
        private readonly Lazy<Predicate<string>> _groupNameMatcher;
        private readonly RequestOptions _retryRateLimitRequestOptions;
        private readonly IReadOnlyList<Color> _roleColors = new[] // Just some good looking Colors from Chart.js
        {
            new Color(77,  201, 246), new Color(246, 112, 25),  new Color(245, 55,  148), new Color(83,  123, 196), new Color(172, 194, 54),
            new Color(22,  106, 143), new Color(0,   169, 80),  new Color(88,  89,  91),  new Color(133, 73,  186), new Color(255, 99,  132),
            new Color(255, 159, 64),  new Color(75,  192, 192), new Color(54,  162, 235), new Color(153, 102, 255), new Color(201, 203, 207)
        };

        protected Predicate<string> GroupNameMatcher => _groupNameMatcher.Value;

        public GroupModule(IOptionsSnapshot<TextOptions> textConfig, IOptionsSnapshot<GroupOptions> groupConfig, ILogger<GroupModule> logger)
        {
            _textConfig = textConfig.Value;
            _groupConfig = groupConfig.Value;
            _logger = logger;
            _groupNameMatcher = new Lazy<Predicate<string>>(GetGroupNameMatcher);
            _retryRateLimitRequestOptions = new RequestOptions { RetryMode = RetryMode.RetryRatelimit }; // Not sure if this is needed
        }

        [Command(nameof(Setup))] // Should probably be RunMode Async but for that the scoping stuff need to be reworked
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task Setup(int amount)
        {
            if (amount < 2)
                throw new ArgumentOutOfRangeException(nameof(amount));

            for (int i = 0; i < amount; i++)
            {
                string name = string.Format(_groupConfig.GroupChannelNameTemplate, i + 1);

                RestRole role = await Context.Guild.CreateRoleAsync(name,
                                                                    GuildPermissions.None,
                                                                    _roleColors[i % _roleColors.Count],
                                                                    isMentionable: false,
                                                                    isHoisted: true,
                                                                    options: _retryRateLimitRequestOptions);

                ICategoryChannel category = await Context.Guild.CreateCategoryChannelAsync(name, options: _retryRateLimitRequestOptions);

                OverwritePermissions groupPermission = new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow);
                await category.AddPermissionOverwriteAsync(role, groupPermission, _retryRateLimitRequestOptions);
                // The bot needs those permissions as well otherwise it can't delete the channel later on. Alternatively you could add 'role' to the bot
                await category.AddPermissionOverwriteAsync(Context.Client.CurrentUser, groupPermission, _retryRateLimitRequestOptions);

                OverwritePermissions everyonePermission = new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny);
                await category.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, everyonePermission, _retryRateLimitRequestOptions);

                Action<GuildChannelProperties> assignCategoryId = c => c.CategoryId = category.Id;
                await Context.Guild.CreateTextChannelAsync(name, assignCategoryId, _retryRateLimitRequestOptions);
                await Context.Guild.CreateVoiceChannelAsync(name, assignCategoryId, _retryRateLimitRequestOptions);
            }

            await ReplyAsync(string.Format(_textConfig.GroupsCreated, amount));
        }

        [Command(nameof(Teardown))]
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task Teardown()
        {
            // TODO Maybe confirm with user

            int count = 0;
            IEnumerable<IDeletable> groupChannels = GetAllGroupChannels();
            foreach (IDeletable deletable in groupChannels.Concat(GetAllGroupRoles()))
            {
                await deletable.DeleteAsync(_retryRateLimitRequestOptions);
                if (deletable is ICategoryChannel)
                {
                    count++;
                }
            }

            await ReplyAsync(string.Format(_textConfig.GroupsDeleted, count));
        }

        // Returns Voice, Text AND Categories
        private IEnumerable<SocketGuildChannel> GetAllGroupChannels()
        {
            IEnumerable<SocketCategoryChannel> groupCategories = Context.Guild.CategoryChannels
                                                                              .Where(c => GroupNameMatcher(c.Name));

            foreach (SocketCategoryChannel categoryChannel in groupCategories)
            {
                foreach (SocketGuildChannel channel in categoryChannel.Channels)
                {
                    yield return channel;
                }

                yield return categoryChannel;
            }
        }

        private IEnumerable<SocketRole> GetAllGroupRoles() => Context.Guild.Roles.Where(c => GroupNameMatcher(c.Name));

        private Predicate<string> GetGroupNameMatcher()
        {
            const string Placeholder = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // regex-safe placeholder used for allowing regex-unsafe name-templates

            string nameRegex = string.Format(_groupConfig.GroupChannelNameTemplate, Placeholder);
            nameRegex = Regex.Escape(nameRegex);
            nameRegex = nameRegex.Replace(Placeholder, "\\d+");

            return name => Regex.IsMatch(name, $"^{nameRegex}$", RegexOptions.IgnoreCase);
        }
    }
}
