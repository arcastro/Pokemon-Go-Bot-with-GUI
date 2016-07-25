using PokemonGo.RocketAPI.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokemonGo.RocketAPI.GUI
{
    public class FormLogger : ILogger
    {
        private LogLevel maxLogLevel;

        /// <summary>
        /// To create a FormLogger, we must define a maximum log level.
        /// All levels above won't be logged.
        /// </summary>
        /// <param name="maxLogLevel"></param>
        public FormLogger(LogLevel maxLogLevel)
        {
            this.maxLogLevel = maxLogLevel;
        }

        /// <summary>
        /// Log a specific message by LogLevel. Won't log if the LogLevel is greater than the maxLogLevel set.
        /// </summary>
        /// <param name="message">The message to log. The current time will be prepended.</param>
        /// <param name="level">Optional. Default <see cref="LogLevel.Info"/>.</param>
        public void Write(string message, LogLevel level = LogLevel.Info, string colorName = "White")
        {
            if (level > maxLogLevel)
                return;

            Color cColor = Color.FromName(colorName);

            Form1.gameForm.PushNewRow(message, cColor);
        }

        public void FormInfo(string infoType, string info)
        {
            Form1.gameForm.PushNewInfo(infoType, info);
        }

        public void MapObject(string oType, string oName, double lat, double lng, string id)
        {
            Form1.gameForm.PushNewMapObject(oType, oName, lat, lng, id);
        }

        public void FormIntInfo(string infoType, int amount)
        {
            Form1.gameForm.PushCounterInfo(infoType, amount);
        }
    }

}
