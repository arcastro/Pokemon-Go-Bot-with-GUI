using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.GeneratedCode;
using System.Collections.Generic;

namespace PokemonGo.RocketAPI
{
    public interface ISettings
    {
        AuthType AuthType { get; }
        double DefaultLatitude { get; }
        double DefaultLongitude { get; }
        string GoogleRefreshToken { get; set; }
        string PtcPassword { get; }
        string PtcUsername { get; }
        bool UseProxy { get; }
        string ProxyUri { get; }
        string ProxyLogin { get; }
        string ProxyPass { get; }
        bool AutoEvolve { get; }
        bool AutoTransfer { get; }
        bool TransferOnlyWeak { get; }

        ICollection<KeyValuePair<AllEnum.ItemId, int>> itemRecycleFilter { get; set; }
    }
}