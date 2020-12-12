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
        private readonly Lazy<Random> _rng;
        private readonly RequestOptions _retryRateLimitRequestOptions;
        private readonly IReadOnlyList<Color> _roleColors = new[] // Just some good looking Colors from Chart.js
        {
            new Color(77,  201, 246), new Color(246, 112, 25),  new Color(245, 55,  148), new Color(83,  123, 196), new Color(172, 194, 54),
            new Color(22,  106, 143), new Color(0,   169, 80),  new Color(88,  89,  91),  new Color(133, 73,  186), new Color(255, 99,  132),
            new Color(255, 159, 64),  new Color(75,  192, 192), new Color(54,  162, 235), new Color(153, 102, 255), new Color(201, 203, 207)
        };

        protected Predicate<string> GroupNameMatcher => _groupNameMatcher.Value;
        protected Random Rng => _rng.Value;

        public GroupModule(IOptionsSnapshot<TextOptions> textConfig, IOptionsSnapshot<GroupOptions> groupConfig, ILogger<GroupModule> logger)
        {
            _textConfig = textConfig.Value;
            _groupConfig = groupConfig.Value;
            _logger = logger;
            _groupNameMatcher = new Lazy<Predicate<string>>(GetGroupNameMatcher);
            _rng = new Lazy<Random>();
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

        [Command(nameof(Distribute))]
        [RequireContext(ContextType.Guild)] // These precondition attributes can't be localized since they require compile time strings..
        [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task Distribute(int groupSize, GroupDistributeArguments? arguments = default)
        {
            if (groupSize < 1)
                throw new ArgumentOutOfRangeException(nameof(groupSize), "The group size has to be higher than 1.");

            if (Context.User is not SocketGuildUser guildUser)
                throw new InvalidOperationException("Message wasn't sent in the context of a guild.");

            if (guildUser.VoiceChannel is null)
                throw new InvalidOperationException("You have to be in a voice channel to use this command."); // TODO maybe use a custom precondition attribute

            List<SocketGuildUser> usersToGroup = guildUser.VoiceChannel.Users.ToList();
            GroupOverflowHandling overflowHandling = default;
            if (arguments is not null)
            {
                overflowHandling = arguments.OverflowHandling;
                ExcludeSpecifiedUsers(arguments.Exclude, usersToGroup);
            }

            if (usersToGroup.Count == 0)
                throw new ArgumentException("There are no users left to put into groups.");

            if (groupSize > usersToGroup.Count)
                throw new ArgumentException("The group size has to be lower than the number of users to group.");

            int groupAmount = usersToGroup.Count / groupSize;
            if (overflowHandling == GroupOverflowHandling.Error &&
                usersToGroup.Count % groupSize > 0)
            {
                // Throw the exception before distributing any roles
                throw new InvalidOperationException($"{usersToGroup.Count} users can't be split evenly into {groupAmount} groups.");
            }

            for (int i = 0; i < groupAmount; i++)
            {
                for (int _ = 0; _ < groupSize; _++)
                {
                    // Take and remove a random user from the list and add it to the current group
                    int randomIndex = Rng.Next(usersToGroup.Count);
                    SocketGuildUser user = usersToGroup[randomIndex];
                    usersToGroup.RemoveAt(randomIndex);

                    await AddUserToGroup(user, i + 1);
                }
            }

            int groupsCreated = groupAmount; // just for user-feedback
            if (usersToGroup.Count > 0)
            {
                switch (overflowHandling)
                {
                    case GroupOverflowHandling.Spread:
                        for (int i = 0; i < usersToGroup.Count; i++)
                        {
                            // Put all users in the group at their position with loop-around - random enough for us
                            await AddUserToGroup(usersToGroup[i], (i % groupAmount) + 1);
                        }

                        break;
                    case GroupOverflowHandling.NewGroup:
                        groupsCreated++;
                        foreach (SocketGuildUser user in usersToGroup)
                        {
                            await AddUserToGroup(user, groupsCreated);
                        }

                        break;
                    default: // GroupOverflowHandling.Error was already handled before
                        break;
                }
            }

            await ReplyAsync($"{groupAmount * groupSize + usersToGroup.Count} users were split into {groupsCreated} groups."); // TODO TextOptions
        }

        [Command(nameof(BreakUp))]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task BreakUp()
        {
            List<SocketRole> roles = GetAllGroupRoles().ToList();
            foreach (SocketGuildUser user in Context.Guild.Users)
            {
                foreach (SocketRole role in user.Roles)
                {
                    if (roles.Any(r => role.Id == r.Id))
                    {
                        await user.RemoveRoleAsync(role, _retryRateLimitRequestOptions);
                    }
                }
            }

            await ReplyAsync("All groups were broken up."); // TODO TextOptions
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

        private Task AddUserToGroup(IGuildUser user, int group)
        {
            string roleName = string.Format(_groupConfig.GroupChannelNameTemplate, group);
            SocketRole? role = Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));

            if (role is null) // TODO oh boy this is a late fail. Easily recoverable by calling the breakup command but still..
                throw new InvalidOperationException($"The guild doesn't appear to be setup for {roleName}.");

            return user.AddRoleAsync(role, _retryRateLimitRequestOptions);
        }

        private Predicate<string> GetGroupNameMatcher()
        {
            const string Placeholder = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // regex-safe placeholder used for allowing regex-unsafe name-templates

            string nameRegex = string.Format(_groupConfig.GroupChannelNameTemplate, Placeholder);
            nameRegex = Regex.Escape(nameRegex);
            nameRegex = nameRegex.Replace(Placeholder, "\\d+");

            return name => Regex.IsMatch(name, $"^{nameRegex}$", RegexOptions.IgnoreCase);
        }

        private static void ExcludeSpecifiedUsers(IEnumerable<string> excludes, List<SocketGuildUser> users)
        {
            foreach (string ignore in excludes)
            {
                if (!(ignore.StartsWith("<@") && ignore.EndsWith('>')))
                    // TODO maybe fallback to name-based user and role comparison
                    throw new NotSupportedException("To exclude users or roles, mention them with @.");

                ulong mentionId = ulong.Parse(ignore.Substring(3, ignore.Length - 3 - 1));
                if (ignore.StartsWith("<@&")) // exclude specific roles
                {
                    users.RemoveAll(g => g.Roles.Any(r => r.Id == mentionId));
                }
                else if (ignore.StartsWith("<@!")) // exclude specific users
                {
                    int index = users.FindIndex(u => u.Id == mentionId);
                    users.RemoveAt(index);
                }
            }
        }
    }
}
