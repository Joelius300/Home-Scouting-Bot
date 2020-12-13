using System.Threading.Tasks;
using Discord.Commands;

namespace HomeScoutingBot.Modules
{
    [RequireOwner]
    public class DebugModule : ModuleBase<ScopedSocketCommandContext>
    {
        [Command(nameof(Ping))]
        public Task Ping() => ReplyAsync("Pong");
    }
}
