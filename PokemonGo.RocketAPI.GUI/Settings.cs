using System.Configuration;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.GeneratedCode;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using AllEnum;

namespace PokemonGo.RocketAPI.GUI
{
    public class Settings : ISettings
    {
        public AuthType AuthType => (AuthType)Enum.Parse(typeof(AuthType), UserSettings.Default.AuthType);
        public string PtcUsername => UserSettings.Default.PtcUsername;
        public string PtcPassword => UserSettings.Default.PtcPassword;
        public double DefaultLatitude => UserSettings.Default.DefaultLatitude;
        public double DefaultLongitude => UserSettings.Default.DefaultLongitude;
        public bool UseProxy => UserSettings.Default.UseProxy;
        public string ProxyUri => UserSettings.Default.ProxyUri;
        public string ProxyLogin => UserSettings.Default.ProxyLogin;
        public string ProxyPass => UserSettings.Default.ProxyPass;
        public bool AutoEvolve => UserSettings.Default.AutoEvolve;
        public bool AutoTransfer => UserSettings.Default.AutoTransfer;
        public bool TransferOnlyWeak => UserSettings.Default.TransferOnlyWeak;
        public int MoveSpeedFactor => UserSettings.Default.MoveSpeedFactor;

        ICollection<KeyValuePair<ItemId, int>> ISettings.itemRecycleFilter
        {
            get
            {
                //Type and amount to keep
                return new[]
                {
                    new KeyValuePair<ItemId, int>(ItemId.ItemPokeBall, 5),
                    new KeyValuePair<ItemId, int>(ItemId.ItemGreatBall, 30),
                     new KeyValuePair<ItemId, int>(ItemId.ItemRevive, 40),
                     new KeyValuePair<ItemId, int>(ItemId.ItemPotion, 10),
                     new KeyValuePair<ItemId, int>(ItemId.ItemSuperPotion, 60),
                     new KeyValuePair<ItemId, int>(ItemId.ItemRazzBerry, 50),
                };
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public string GoogleRefreshToken
        {
            get { return UserSettings.Default.GoogleRefreshToken; }
            set
            {
                UserSettings.Default.GoogleRefreshToken = value;
                UserSettings.Default.Save();
            }
        }
    }
}
