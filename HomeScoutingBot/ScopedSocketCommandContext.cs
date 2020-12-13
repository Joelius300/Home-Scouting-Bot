using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace HomeScoutingBot
{
    public class ScopedSocketCommandContext : SocketCommandContext, IDisposable
    {
        public IServiceScope ServiceScope { get; }

        public ScopedSocketCommandContext(DiscordSocketClient client, SocketUserMessage msg, IServiceScope serviceScope) : base(client, msg)
        {
            ServiceScope = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
        }

        public void Dispose() => ServiceScope.Dispose();
    }
}
