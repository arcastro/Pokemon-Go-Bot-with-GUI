﻿using PokemonGo.RocketAPI.Logging;
using System;

namespace PokemonGo.RocketAPI
{
	/// <summary>
	/// Generic logger which can be used across the projects.
	/// Logger should be set to properly log.
	/// </summary>
	public static class Logger
	{
		private static ILogger logger;

		/// <summary>
		/// Set the logger. All future requests to <see cref="Write(string, LogLevel)"/> will use that logger, any old will be unset.
		/// </summary>
		/// <param name="logger"></param>
		public static void SetLogger(ILogger logger)
		{
			Logger.logger = logger;
		}

		/// <summary>
		/// Log a specific message to the logger setup by <see cref="SetLogger(ILogger)"/> .
		/// </summary>
		/// <param name="message">The message to log.</param>
		/// <param name="level">Optional level to log. Default <see cref="LogLevel.Info"/>.</param>
		public static void Write(string message, LogLevel level = LogLevel.Info, string colorName = "White")
		{
			if (logger == null)
				return;
            logger.Write(message, level, colorName);
		}

        public static void PushFormInfo(string infoType, string  info)
        {
            if (logger == null)
                return;
            logger.FormInfo(infoType, info);
        }

        public static void PushMapObject(string oType, string oName, double lat, double lng, string id)
        {
            if (logger == null)
                return;
            logger.MapObject(oType, oName, lat, lng, id);
        }
    }

	public enum LogLevel
	{
		None = 0,
		Error = 1,
		Warning = 2,
		Info = 3,
		Debug = 4
	}
}