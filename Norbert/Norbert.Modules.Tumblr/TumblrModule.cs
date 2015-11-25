﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using log4net;
using Norbert.Modules.Common;
using Norbert.Modules.Common.Events;
using Norbert.Modules.Common.Exceptions;

namespace Norbert.Modules.Tumblr
{
    public class TumblrModule : INorbertModule
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(TumblrModule));
        private static readonly Regex Regex = new Regex(@"tumblr\s*(?:of\s*)?(?<tag>.*)");
        private static readonly DateTime MinBefore = new DateTime(2010, 1, 1);
        private static readonly Random Random = new Random();

        private IConfigLoader _configLoader;
        private IChatClient _chatClient;
        private IHttpService _httpService;
        private string _apiKey;

        public void Loaded(IConfigLoader configLoader, IFileSystem fileSystem, 
            IChatClient chatClient, IHttpService httpService)
        {
            _httpService = httpService;
            _chatClient = chatClient;
            _configLoader = configLoader;

            _chatClient.MessageReceived += OnMessageReceived;
            SetupApiKey();
        }

        public void Unloaded()
        {
        }

        private void SetupApiKey()
        {
            var config = new Config();
            try
            {
                config = _configLoader.Load<Config>("Tumblr/Config.json");
            }
            catch (LoadConfigException e)
            {
                Log.Error(e.Message);
            }

            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                _apiKey = null;
                Log.Warn("No api key specified");
            }
            else
            {
                _apiKey = config.ApiKey;
                Log.Info("Using api key from Config.json");
            }
        }

        private async void OnMessageReceived(object sender, MessageReceivedEventArgs msg)
        {
            if (msg.IsPrivate || !msg.IsCommand)
            {
                Log.Debug($"Message ignored: IsPrivate={msg.IsPrivate}, IsCommand={msg.IsCommand}");
                return;
            }

            var match = Regex.Match(msg.Message);
            if (!match.Success)
            {
                Log.Debug("Message ignored: no match found");
                return;
            }

            var tag = match.Groups["tag"].Value;
            if (tag == string.Empty)
            {
                Log.Debug("Message ignored: empty capture group");
                return;
            }

            Log.Debug($"Replying: matches '{Regex}'");

            try
            {
                var post = await GetRandomPost(tag);
                var tumblrMsg = post == null ? 
                    "Whoops, no tumblrs found" : 
                    $"{msg.Nick}: {post.short_url}";

                _chatClient.SendMessage(tumblrMsg, msg.Source);
            }
            catch (HttpServiceException)
            {
                _chatClient.SendMessage("Whoops, something went wrong", msg.Source);
            }
        }

        private async Task<dynamic> GetRandomPost(string tag)
        {
            var allPosts = new List<dynamic>();

            for (var i = 0; i < 3; i++)
            {
                var range = (DateTime.Today - MinBefore).Days;
                var before = MinBefore.AddDays(Random.Next(range));

                var posts = await GetPosts(tag, before, 5);
                allPosts.AddRange(posts);
            }

            var index = Random.Next(0, allPosts.Count);
            Log.Debug($"{allPosts.Count} posts found, choosing post {index}");
            return allPosts.ElementAtOrDefault(index);
        }

        private async Task<List<dynamic>> GetPosts(string tag, DateTime before, int limit)
        {
            var timestamp = (before.Ticks - 621355968000000000) / 10000000;
            var q = $"?api_key={_apiKey}&tag={tag}&before={timestamp}&limit={limit}";

            string uri = $"http://api.tumblr.com/v2/tagged/{q}";
            var posts = await _httpService.GetAsync(uri);

            return ((IEnumerable<dynamic>)posts.response)
                .Where(p => p.type == "photo")
                .ToList();
        } 
    }
}