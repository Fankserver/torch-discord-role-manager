using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers.ChatManager;
using Torch.Session;

namespace DiscordRoleManager
{
    public class RolePlugin : TorchPluginBase, IWpfPlugin
    {
        public const string ConfigFileName = "DiscordRoleManager.cfg";
        public RoleConfig Config => _config?.Data;

        private RoleControl _control;
        private TorchSessionManager _sessionManager;
        private ChatManagerServer _chatmanager;
        private IMultiplayerManagerBase _multibase;
        private Persistent<RoleConfig> _config;
        private HashSet<ulong> _conecting = new HashSet<ulong>();
        private Dictionary<ulong, string> _linkIds = new Dictionary<ulong, string>();

        public readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new RoleControl(this));

        public void Save() => _config.Save();

        /// <inheritdoc />
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            var configPath = Path.Combine(StoragePath, ConfigFileName);

            try
            {
                _config = Persistent<RoleConfig>.Load(configPath);
            }
            catch (Exception e)
            {
                Log.Warn(e);
            }

            if (_config?.Data == null)
                _config = new Persistent<RoleConfig>(configPath, new RoleConfig());

            if (Config.BotToken.Length == 0)
                Log.Warn("No BOT token set, plugin will not work at all! Add your bot TOKEN, save and restart torch.");

            _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged += SessionChanged;
            else
                Log.Warn("No session manager loaded!");
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            switch (state)
            {
                case TorchSessionState.Loaded:
                    _multibase = Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerBase>();
                    if (_multibase != null)
                    {
                        _multibase.PlayerJoined += _multibase_PlayerJoined;
                        MyEntities.OnEntityAdd += MyEntities_OnEntityAdd;
                        _multibase.PlayerLeft += _multibase_PlayerLeft;
                    }
                    else
                        Log.Warn("No join/leave manager loaded!");

                    _chatmanager = Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
                    if (_chatmanager != null)
                        _chatmanager.MessageRecieved += MessageRecieved;

                    else
                        Log.Warn("No chat manager loaded!");

                    Log.Warn("Starting Discord role manager!");

                    //DDBridge = new DiscordBridge(this);

                    ////send status
                    //if (Config.UseStatus)
                    //    StartTimer();

                    break;
                case TorchSessionState.Unloading:
                    //if (DDBridge != null && Config.Stopped.Length > 0)
                    //    DDBridge.SendStatusMessage(null, Config.Stopped);

                    if (_multibase != null)
                    {
                        _multibase.PlayerJoined -= _multibase_PlayerJoined;
                        MyEntities.OnEntityAdd -= MyEntities_OnEntityAdd;
                        _multibase.PlayerLeft -= _multibase_PlayerLeft;
                    }

                    if (_chatmanager != null)
                        _chatmanager.MessageRecieved -= MessageRecieved;

                    _conecting.Clear();
                    _linkIds.Clear();

                    break;
                case TorchSessionState.Unloaded:
                    //if (DDBridge != null)
                    //    DDBridge.Stopdiscord();

                    Log.Warn("Discord role manager unloaded!");

                    break;
                default:
                    // ignore
                    break;
            }
        }

        private void _multibase_PlayerLeft(IPlayer obj)
        {
            //Remove to conecting list
            _conecting.Remove(obj.SteamId);
        }

        private void _multibase_PlayerJoined(IPlayer obj)
        {
            //Add to conecting list
            _conecting.Add(obj.SteamId);
        }

        private void MessageRecieved(TorchChatMessage msg, ref bool consumed)
        {
            if (msg.AuthorSteamId.HasValue && (msg.Message == "/link" || msg.Message == "/verify"))
            {
                consumed = true;
                _chatmanager.SendMessageAsOther(0, "ASD", msg.AuthorSteamId.Value);
            }
        }

        private void MyEntities_OnEntityAdd(VRage.Game.Entity.MyEntity obj)
        {
            if (obj is MyCharacter character)
            {
                Task.Run(() =>
                {
                    System.Threading.Thread.Sleep(Config.InfoDelay);
                    if (_conecting.Contains(character.ControlSteamId) && character.IsPlayer)
                    {
                        //After spawn on world, remove from connecting list
                        _conecting.Remove(character.ControlSteamId);

                        _chatmanager.SendMessageAsOther(0, "Write '/link' into the chat to link your steam account with discord");
                    }
                });
            }
        }
    }
}
