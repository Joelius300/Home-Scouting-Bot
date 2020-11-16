using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeScoutingBot.Options
{
    public class TextOptions
    {
        /// <summary>
        /// A format string which takes the username and the error message.
        /// </summary>
        public string CommandExecutionFailed { get; set; } = "Sorry {0}, something went wrong: {1}";
    }
}
