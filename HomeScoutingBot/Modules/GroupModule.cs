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
    [RequireUserPermission(GuildPermission.ManageChannels | GuildPermission.ManageRoles)]
    public class GroupModule : ModuleBase<ScopedSocketCommandContext>
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

        [Command(nameof(Setup))]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task Setup(int amount)
        {
            if (amount < 1)
                throw new ArgumentOutOfRangeException(nameof(amount));

            for (int i = 0; i < amount; i++)
            {
                await CreateGroup(i + 1);
            }

            await ReplyAsync(string.Format(_textConfig.GroupsCreated, amount));
        }

        private async Task<(RestRole role, RestCategoryChannel category, RestTextChannel text, RestVoiceChannel voice)> CreateGroup(int groupNumber)
        {
            string name = string.Format(_groupConfig.GroupChannelNameTemplate, groupNumber);
            RestRole role = await Context.Guild.CreateRoleAsync(name,
                                                                GuildPermissions.None,
                                                                _roleColors[(groupNumber-1) % _roleColors.Count], // groupNumber is 1-indexed
                                                                isMentionable: false,
                                                                isHoisted: true,
                                                                options: _retryRateLimitRequestOptions);

            // Create group-category which only 'role' can see and join
            RestCategoryChannel category = await Context.Guild.CreateCategoryChannelAsync(name, options: _retryRateLimitRequestOptions);

            OverwritePermissions groupPermission = new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow);
            await category.AddPermissionOverwriteAsync(role, groupPermission, _retryRateLimitRequestOptions);
            // The bot needs those permissions as well otherwise it can't delete the channel later on. Alternatively you could add 'role' to the bot.
            await category.AddPermissionOverwriteAsync(Context.Client.CurrentUser, groupPermission, _retryRateLimitRequestOptions);

            OverwritePermissions everyonePermission = new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny);
            await category.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, everyonePermission, _retryRateLimitRequestOptions);

            // Add one text and one voice channel
            Action<GuildChannelProperties> assignCategoryId = c => c.CategoryId = category.Id;
            RestTextChannel text = await Context.Guild.CreateTextChannelAsync(name, assignCategoryId, _retryRateLimitRequestOptions);
            RestVoiceChannel voice = await Context.Guild.CreateVoiceChannelAsync(name, assignCategoryId, _retryRateLimitRequestOptions);

            return (role, category, text, voice);
        }

        [Command(nameof(Distribute))]
        [RequireContext(ContextType.Guild)] // These precondition attributes can't be localized since they require compile time strings..
        [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task Distribute(int groupSize, GroupDistributeArguments? arguments = default)
        {
            /* ---------- handle arguments ---------- */
            if (groupSize < 1)
                throw new ArgumentOutOfRangeException(nameof(groupSize), "The group size has to be higher than 1.");

            if (Context.User is not SocketGuildUser guildUser)
                throw new InvalidOperationException("Message wasn't sent in the context of a guild.");

            if (guildUser.VoiceChannel is null)
                throw new InvalidOperationException("You have to be in a voice channel to use this command."); // TODO maybe use a custom precondition attribute

            Dictionary<int, IRole> groupRolesCache = new Dictionary<int, IRole>(); // Context.Guild.Roles doesn't update fast enough despite the GUILDS intent
            List<SocketGuildUser> usersToGroup = guildUser.VoiceChannel.Users.ToList(); // Gets all users *connected* to the voice channel!
            GroupOverflowHandling overflowHandling = default;
            bool createMissingGroups = true;
            if (arguments is not null)
            {
                overflowHandling = arguments.OverflowHandling;
                createMissingGroups = arguments.CreateMissingGroups;
                ExcludeSpecifiedUsers(arguments.Exclude, usersToGroup);
            }

            int totalUsersToDistribute = usersToGroup.Count; // just for user-feedback
            if (usersToGroup.Count == 0)
                throw new ArgumentException("There are no users left to put into groups.");

            if (groupSize > usersToGroup.Count)
                throw new ArgumentException("The group size can't be higher than the number of users to group.");

            int groupAmount = usersToGroup.Count / groupSize;
            if (overflowHandling == GroupOverflowHandling.Error &&
                usersToGroup.Count % groupSize > 0)
            {
                // In case of disallowed overflow, throw the exception before distributing any roles (fast fail)
                throw new InvalidOperationException($"{usersToGroup.Count} users can't be split evenly into {groupAmount} groups.");
            }

            /* ---------- distribute evenly without taking overflow into account ---------- */
            for (int i = 0; i < groupAmount; i++)
            {
                IRole groupRole = await GetGroupRole(i + 1, createMissingGroups, roleCache: null);
                groupRolesCache[i + 1] = groupRole;
                for (int _ = 0; _ < groupSize; _++)
                {
                    // Take and remove a random user from the list and add it to the current group
                    int randomIndex = Rng.Next(usersToGroup.Count);
                    SocketGuildUser user = usersToGroup[randomIndex];
                    usersToGroup.RemoveAt(randomIndex);

                    await user.AddRoleAsync(groupRole, _retryRateLimitRequestOptions);
                }
            }

            /* ---------- handle overflow / distribute leftover users ---------- */
            if (usersToGroup.Count > 0)
            {
                switch (overflowHandling)
                {
                    case GroupOverflowHandling.Spread:
                        for (int i = 0; i < usersToGroup.Count; i++)
                        {
                            // Put all users in the group at their position with loop-around - random enough for us.
                            // Since it only puts users in the already existing groups, we can safely pass createIfMissing: false.
                            // We need the cache here since since Context.Guild.Roles isn't updated fast enough when createMissingGroups=true.
                            IRole groupRole = await GetGroupRole((i % groupAmount) + 1, createIfMissing: false, groupRolesCache);
                            await usersToGroup[i].AddRoleAsync(groupRole, _retryRateLimitRequestOptions);
                        }

                        break;
                    case GroupOverflowHandling.NewGroup:
                        groupAmount++;
                        // This group was 100% not cached so we don't even pass the cache
                        IRole role = await GetGroupRole(groupAmount, createMissingGroups, roleCache: null);
                        foreach (SocketGuildUser user in usersToGroup)
                        {
                            await user.AddRoleAsync(role, _retryRateLimitRequestOptions);
                        }

                        break;
                    default: // GroupOverflowHandling.Error was already handled before
                        break;
                }
            }

            await ReplyAsync($"{totalUsersToDistribute} users were split into {groupAmount} groups."); // TODO TextOptions
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
                    users.RemoveAll(g => g.Roles.Any(r => r.Id == mentionId)); // id-based Contains
                }
                else if (ignore.StartsWith("<@!")) // exclude specific users
                {
                    int index = users.FindIndex(u => u.Id == mentionId);
                    if (index > -1)
                    {
                        users.RemoveAt(index);
                    }
                }
            }
        }

        private async Task<IRole> GetGroupRole(int groupNumber, bool createIfMissing, IReadOnlyDictionary<int, IRole>? roleCache)
        {
            IRole? role;

            if (roleCache is not null && roleCache.TryGetValue(groupNumber, out role))
                return role;

            string roleName = string.Format(_groupConfig.GroupChannelNameTemplate, groupNumber);
            role = Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));

            if (role is not null)
                return role;

            if (!createIfMissing) // this is a late fail. Easily recoverable by calling the breakup command but still not good..
                throw new InvalidOperationException($"The guild doesn't appear to be setup for {roleName}.");

            return (await CreateGroup(groupNumber)).role;
        }

        [Command(nameof(BreakUp))]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task BreakUp()
        {
            List<SocketRole> roles = GetGroupRoles().ToList();
            foreach (SocketGuildUser user in Context.Guild.Users)
            {
                // user.RemoveRolesAsync can't do it in batch so it sends loads of request if we don't check the roles first
                foreach (SocketRole role in user.Roles)
                {
                    if (roles.Any(r => role.Id == r.Id)) // id-based Contains
                    {
                        await user.RemoveRoleAsync(role, _retryRateLimitRequestOptions);
                    }
                }
            }

            await ReplyAsync("All groups were broken up."); // TODO TextOptions
        }

        [Command(nameof(Teardown))]
        [RequireContext(ContextType.Guild)]
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.ManageRoles | GuildPermission.SendMessages)]
        public async Task Teardown()
        {
            // TODO Maybe confirm with user (but that could break statelessness)

            foreach (SocketCategoryChannel category in GetGroupCategories())
            {
                foreach (SocketGuildChannel innerChannel in category.Channels)
                {
                    await Delete(innerChannel);
                }

                await Delete(category);
            }

            int deletedRolesCount = 0; // this isn't always reliable but provides some user feedback
            foreach (SocketRole role in GetGroupRoles())
            {
                await Delete(role);
                deletedRolesCount++;
            }

            await ReplyAsync(string.Format(_textConfig.GroupsDeleted, deletedRolesCount));

            Task Delete(IDeletable deletable) => deletable.DeleteAsync(_retryRateLimitRequestOptions);
        }

        private IEnumerable<SocketCategoryChannel> GetGroupCategories() => Context.Guild.CategoryChannels.Where(c => GroupNameMatcher(c.Name));
        private IEnumerable<SocketRole> GetGroupRoles() => Context.Guild.Roles.Where(c => GroupNameMatcher(c.Name));

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
