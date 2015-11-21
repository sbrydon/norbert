﻿using System;
using ChatSharp;
using log4net;
using Norbert.Cli.Exceptions;

namespace Norbert.Cli
{
    class App
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(App));
        private static Config _config;

        static void Main(string[] args)
        {
            try
            {
                _config = Config.Load();
            }
            catch (LoadConfigException e)
            {
                Log.Fatal(e.Message);
                Console.WriteLine($"{Environment.NewLine}Press any key to exit");
                Console.ReadKey();

                return;
            }

            var client = new IrcClient(_config.Server, new IrcUser(_config.Nick, _config.User));
            client.ConnectionComplete += delegate
            {
                Log.Info("Connected, joining channels..");

                foreach (var channel in _config.Channels)
                {
                    client.JoinChannel(channel);
                    Log.Info($"Joined {channel}");
                }

                Console.WriteLine($"{Environment.NewLine}Press any key to exit");
            };

            Log.Info($"Connecting to {_config.Server}..");
            client.ConnectAsync();

            Console.ReadKey();
            client.Quit(_config.QuitMsg);
        }
    }
}