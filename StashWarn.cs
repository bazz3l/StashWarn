using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Stash Warn", "Bazz3l", "0.0.4")]
    [Description("Send notification to discord when someone uncovers another players/clans stash.")]
    public class StashWarn : RustPlugin
    {
        [PluginReference] Plugin Clans, Friends;
        
        #region Fields

        private const string PermIgnore = "stashwarn.ignore";
        private const string PermUse = "stashwarn.use";
        
        private readonly Dictionary<ulong, int> _violations = new Dictionary<ulong, int>();
        private PluginConfig _config;
        private StoredData _stored;

        #endregion

        #region Storage

        private class StoredData
        {
            public readonly List<ulong> Toggles = new List<ulong>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _stored);
        }
        
        #endregion
        
        #region Config

        PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                
                DiscordWebhook = "https://discordapp.com/api/webhooks/760189069090750516/2BwpTG4psgd8L71GQmH0hLx9dG8oWjA4Bbj1lNLkTkp0EWupN5_M55GTzVvyJnmICH3o",
                DiscordUsername = "Stash Warn",
                DiscordAvatar = "https://cdn.discordapp.com/attachments/598270871806803982/760248934474973234/310.png",
                DiscordTitle = "Stash Uncovered!",
                DiscordColor = 65535,
                DiscordImage = "https://cdn.discordapp.com/attachments/598270871806803982/760249104675766282/419.png",
                DiscordDescription = "Pst!, {0} uncovered a stash check it out bitch.",
                EnableTeams = true,
                EnableClans = true,
                EnableFriend = true
            };
        }

        private class PluginConfig
        {
            [JsonProperty("Discord webhook url")]
            public string DiscordWebhook;
            
            [JsonProperty("Discord username")]
            public string DiscordUsername;
            
            [JsonProperty("Discord avatar")]
            public string DiscordAvatar;

            [JsonProperty("Discord embed title")]
            public string DiscordTitle;
            
            [JsonProperty("Discord embed image")]
            public string DiscordImage;
            
            [JsonProperty("Discord embed color")]
            public int DiscordColor;

            [JsonProperty("Discord embed description")]
            public string DiscordDescription;

            [JsonProperty("Enable team checks")]
            public bool EnableTeams;
            
            [JsonProperty("Enable clan checks")]
            public bool EnableClans;
            
            [JsonProperty("Enable friend checks")]
            public bool EnableFriend;
            
            [JsonProperty("Stash inventory items")]
            public Dictionary<string, int> StashItems = new Dictionary<string, int>
            {
                {"lowgradefuel", 100},
                {"metal.fragments", 100},
                {"metal.ore", 100},
                {"cloth", 100},
                {"leather", 100},
                {"scrap", 10},
                {"fat.animal", 100},
                {"gunpowder", 100},
                {"metal.refined", 10},
                {"hq.metal.ore", 10},
                {"bone.fragments", 100},
                {"charcoal", 100},
                {"stones", 100},
                {"sulfur", 100},
                {"sulfur.ore", 100},
                {"wood", 100}
            };
        }

        #endregion
        
        #region Oxide

        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        protected override void LoadDefaultMessages()
        {
           lang.RegisterMessages(new Dictionary<string, string>
               {
                   {"InvalidSyntax", "/stash or /stash <amount>"},
                   {"Permission", "Unknown command {0}"},
                   {"Placed", "Stash placed time to catch some scum bags."},
                   {"Toggle", "Stash warn is now {0}."},
                   {"Enabled", "<color=#73D43B>Enabled</color>"},
                   {"Disabled", "<color=#DC143C>Disabled</color>"}
               }, this);
        }
        
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermIgnore, this);
            permission.RegisterPermission(PermUse, this);
        }

        private void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
            _stored = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            StashContainer stash = gameObject.GetComponent<StashContainer>();
            if (stash == null)
            {
                return;
            }
            
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null || !HasPermission(player, PermUse) || !_stored.Toggles.Contains(player.userID))
            {
                return;
            }

            SetupStash(stash);
            
            player.ChatMessage(Lang("Placed", player.UserIDString));
        }

        private void CanSeeStash(BasePlayer suspect, StashContainer stash)
        {
            if (stash.IsOpen() || HasPermission(suspect, PermIgnore))
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
        
        #region Commands

        [ChatCommand("stash")]
        private void CmdStash(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, PermUse))
            {
                player.ChatMessage(Lang("Permission", player.UserIDString, command));
                return;
            }

            if (args.Length != 0)
            {
                GiveStash(player, args[0]);
                return;
            }

            TogglePlayer(player.userID);

            player.ChatMessage(Lang("Toggle", player.UserIDString, (_stored.Toggles.Contains(player.userID) ? Lang("Enabled", player.UserIDString) : Lang("Disabled", player.UserIDString))));
        }

        #endregion
        
        #region Helpers

        private void TogglePlayer(ulong userID)
        {
            if (_stored.Toggles.Contains(userID))
                _stored.Toggles.Remove(userID);
            else
                _stored.Toggles.Add(userID);
            
            SaveData();
        }

        private void GiveStash(BasePlayer player, string value)
        {
            int amount = 1;

            int.TryParse(value, out amount);

            player.inventory.GiveItem(ItemManager.CreateByName("stash.small", amount));
        }

        private void SetupStash(StashContainer stash)
        {
            stash.OwnerID = 0UL;
            
            stash.SetHidden(true);
            
            for (int i = 0; i < stash.inventory.capacity; i++)
            {
                KeyValuePair<string, int> invItem = _config.StashItems.ElementAt(Random.Range(0, _config.StashItems.Count));
                
                ItemManager.CreateByName(invItem.Key, invItem.Value)?.MoveToContainer(stash.inventory);
            }
        }

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
                   || IsTeamMember(owner, suspect)
                   || IsClanMember(owner, suspect) 
                   || IsFriend(owner, suspect);
        }
        
        private bool IsClanMember(BasePlayer owner, BasePlayer suspect)
        {
            if (!_config.EnableClans || Clans == null)
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
            if (!_config.EnableFriend || Friends == null)
            {
                return false;
            }

            return Friends.Call<bool>("AreFriends", owner.userID, suspect.userID);
        }

        private bool IsTeamMember(BasePlayer owner, BasePlayer suspect)
        {
            if (!_config.EnableTeams)
            {
                return false;
            }
            
            RelationshipManager.PlayerTeam team = RelationshipManager.Instance.FindTeam(owner.currentTeam);
            if (team == null)
            {
                return false;
            }

            return team.members.Contains(suspect.userID);
        }

        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
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
            private int Color { get; set; }

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