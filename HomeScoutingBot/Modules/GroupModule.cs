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

        [Command(nameof(Teardown))]
        [RequireBotPermission(GuildPermission.ManageChannels | GuildPermission.SendMessages)]
        public async Task Teardown()
        {
            const string Placeholder = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; // regex-safe placeholder used for allowing regex-unsafe name-templates

            await ReplyAsync("Tearing down..");

            // TODO Potentially find an easy way to confirm using some other message

            string nameRegex = string.Format(_groupConfig.GroupChannelNameTemplate, Placeholder);
            nameRegex = Regex.Escape(nameRegex);
            nameRegex = nameRegex.Replace(Placeholder, "\\d+");

            Predicate<string> matchName = name => Regex.IsMatch(name, $"^{nameRegex}$", RegexOptions.IgnoreCase);

            IEnumerable<SocketCategoryChannel> groupCategories = Context.Guild.CategoryChannels.Where(c => matchName(c.Name));
            foreach (SocketCategoryChannel categoryChannel in groupCategories)
            {
                foreach (SocketGuildChannel channel in categoryChannel.Channels)
                {
                    await channel.DeleteAsync();
                }

                await categoryChannel.DeleteAsync();
            }
        }
    }
}
