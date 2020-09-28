using System.Collections.Generic;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Stash Warn", "Bazz3l", "0.0.1")]
    [Description("Send notification to discord when someone uncovers another players/clans stash.")]
    public class StashWarn : RustPlugin
    {
        [PluginReference] Plugin Clans, Friends;
        
        #region Fields
        
        private readonly Dictionary<ulong, int> _violations = new Dictionary<ulong, int>();
        
        private PluginConfig _config;

        #endregion

        #region Config

        PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                DiscordWebhook = "https://discordapp.com/api/webhooks/webhook-here",
                DiscordUsername = "Stash Warn",
                DiscordDescription = "Pst!, {0} uncovered a stash check it out bitch.",
                DiscordTitle = "Stash Uncovered!",
                DiscordImage = "https://cdn.discordapp.com/attachments/598270871806803982/760249104675766282/419.png",
                DiscordAvatar = "https://cdn.discordapp.com/attachments/598270871806803982/760248934474973234/310.png",
                DiscordColor = 65535
            };
        }

        private class PluginConfig
        {
            public string DiscordWebhook;
            public string DiscordUsername;
            public string DiscordDescription;
            public string DiscordTitle;
            public string DiscordImage;
            public string DiscordAvatar;
            public int DiscordColor;
        }

        #endregion
        
        #region Oxide

        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        private void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
        }

        private void CanSeeStash(BasePlayer suspect, StashContainer stash)
        {
            if (stash.IsOpen())
            {
                return;
            }

            if (stash.OwnerID == 0UL)
            {
                SetViolation(suspect.userID);

                SendMessage(null, suspect);
            }
            else
            {
                BasePlayer owner = BasePlayer.FindByID(stash.OwnerID);
                if (owner == null)
                {
                    return;
                }

                if (IsStashOwner(owner, suspect))
                {
                    return;
                }

                SetViolation(suspect.userID);

                SendMessage(owner, suspect);                
            }
        }

        #endregion
        
        #region Helpers

        private void SetViolation(ulong userID)
        {
            if (!_violations.ContainsKey(userID))
                _violations.Add(userID, 0);
            else
                _violations[userID]++;
        }
        
        private int GetViolations(ulong userID)
        {
            return _violations.ContainsKey(userID) ? _violations[userID] : 0;
        }

        private bool IsStashOwner(BasePlayer owner, BasePlayer suspect)
        {
            return owner.userID == suspect.userID 
                   || IsClanMember(owner, suspect) 
                   || IsFriend(owner, suspect);
        }
        
        private bool IsClanMember(BasePlayer owner, BasePlayer suspect)
        {
            if (Clans == null)
            {
                return false;
            }
            
            string stashClan = Clans.Call<string>("GetClanOf", owner);
            string suspectClan = Clans.Call<string>("GetClanOf", suspect);
            
            if (stashClan == null || suspectClan == null)
            {
                return false;
            }

            return stashClan == suspectClan;
        }
        
        private bool IsFriend(BasePlayer owner, BasePlayer suspect)
        {
            if (Friends == null)
            {
                return false;
            }

            return Friends.Call<bool>("AreFriends", owner.userID, suspect.userID);
        }

        #endregion
        
        #region Discord

        private DiscordMessage BuildMessage(BasePlayer owner, BasePlayer suspect)
        {
            EmbedBuilder builder = new EmbedBuilder()
                .WithTitle(_config.DiscordTitle)
                .WithColor(_config.DiscordColor)
                .WithImage(_config.DiscordImage)
                .WithDescription(string.Format(_config.DiscordDescription, suspect.displayName.EscapeRichText()))
                .AddField("Owner", (owner == null ? "Server Placed" : $"{owner.displayName.EscapeRichText()} - {owner.UserIDString}"))
                .AddField("Suspect", $"{suspect.displayName.EscapeRichText()} - {suspect.UserIDString}")
                .AddField("Violations", GetViolations(suspect.userID))
                .AddField("Location", suspect.ServerPosition.ToString());

            return new DiscordMessage()
                .WithUsername(_config.DiscordUsername)
                .WithAvatar(_config.DiscordAvatar)
                .WithContent("")
                .SetEmbed(builder);            
        }

        private void SendMessage(BasePlayer owner, BasePlayer suspect)
        {
            DiscordMessage message = BuildMessage(owner, suspect);
            if (message == null)
            {
                return;
            }

            webrequest.Enqueue(_config.DiscordWebhook, message.ToJson(), (code, response) => {}, this, RequestMethod.POST, new Dictionary<string, string>() {
                { "Content-Type", "application/json" }
            });
        }
        
        private class EmbedBuilder
        {
            public EmbedBuilder()
            {
                Fields = new List<Field>();
            }

            [JsonProperty("title")]
            private string Title { get; set; }

            [JsonProperty("description")]
            private string Description { get; set; }

            [JsonProperty("image")]
            private EmbedImage Image { get; set; }

            [JsonProperty("color")]
            private int Color { get; set; } = 111111;

            [JsonProperty("url")]
            private string Url { get; set; }

            [JsonProperty("fields")]
            private List<Field> Fields { get; }

            public EmbedBuilder WithTitle(string value)
            {
                Title = value;

                return this;
            }

            public EmbedBuilder WithDescription(string value)
            {
                Description = value;

                return this;
            }

            public EmbedBuilder WithImage(string value)
            {
                Image = new EmbedImage(value);

                return this;
            }

            public EmbedBuilder WithColor(int value)
            {
                Color = value;

                return this;
            }

            public EmbedBuilder WithUrl(string url)
            {
                Url = url;

                return this;
            }

            public EmbedBuilder AddInlineField(string name, object value)
            {
                Fields.Add(new Field(name, value, true));

                return this;
            }

            public EmbedBuilder AddField(string name, object value)
            {
                Fields.Add(new Field(name, value, false));

                return this;
            }

            public EmbedBuilder AddField(Field field)
            {
                Fields.Add(field);

                return this;
            }

            public string GetTitle()
            {
                return Title;
            }

            private Field[] GetFields() => Fields.ToArray();

            internal class Field
            {
                public Field(string name, object value, bool inline)
                {
                    Name = name;
                    Value = value;
                    Inline = inline;
                }

                [JsonProperty("name")]
                public string Name { get; set; }

                [JsonProperty("value")]
                public object Value { get; set; }

                [JsonProperty("inline")]
                public bool Inline { get; set; }
            }

            internal class EmbedImage
            {
                public EmbedImage(string url)
                {
                    Url = url;
                }

                [JsonProperty("url")]
                public string Url { get; set; }
            }
        }

        private class DiscordMessage
        {
            [JsonProperty("username")]
            private string Username { get; set; }

            [JsonProperty("content")]
            private string Content { get; set; }

            [JsonProperty("avatar_url")]
            private string AvatarUrl { get; set; }

            [JsonProperty("embeds")]
            private EmbedBuilder[] Embeds { get; set; }

            public DiscordMessage WithUsername(string value)
            {
                Username = value;

                return this;
            }

            public DiscordMessage WithContent(string value)
            {
                Content = value;

                return this;
            }

            public DiscordMessage WithAvatar(string value)
            {
                AvatarUrl = value;

                return this;
            }

            public DiscordMessage SetEmbed(EmbedBuilder value)
            {
                Embeds = new[] { value };

                return this;
            }

            public string ToJson() => JsonConvert.SerializeObject(this);
        }
        
        #endregion
    }
}