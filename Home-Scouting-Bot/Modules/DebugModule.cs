using System.Threading.Tasks;
using Discord.Commands;

namespace Home_Scouting_Bot.Modules
{
    [RequireOwner]
    public class DebugModule : ModuleBase<SocketCommandContext>
    {
        [Command("Ping")]
        public Task Ping() => ReplyAsync("Pong");
    }
}
