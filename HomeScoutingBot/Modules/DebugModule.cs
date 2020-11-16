using System.Threading.Tasks;
using Discord.Commands;

namespace HomeScoutingBot.Modules
{
    [RequireOwner]
    public class DebugModule : ModuleBase<SocketCommandContext>
    {
        [Command("Ping")]
        public Task Ping() => ReplyAsync("Pong");
    }
}
