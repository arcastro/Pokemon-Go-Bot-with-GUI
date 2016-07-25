using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AllEnum;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Logic.Utils;

namespace PokemonGo.RocketAPI.Logic
{
    public class Logic
    {
        private readonly Client _client;
        private readonly ISettings _clientSettings;
        private readonly Inventory _inventory;

        private Dictionary<string, FortData> currentPokeStops = new Dictionary<string, FortData>();

        private FortData FortToMove;

        private bool stopRoutine = false;

        public Logic(ISettings clientSettings)
        {
            _clientSettings = clientSettings;
            _client = new Client(_clientSettings);
            _inventory = new Inventory(_client);
        }

        public void ForceMoveToPokestop(string id)
        {
            if (currentPokeStops.ContainsKey(id))
            {
                FortToMove = currentPokeStops[id];
                stopRoutine = true;
            }
        }

        public async void Execute()
        {
            Logger.Write($"Starting Execute on login server: {_clientSettings.AuthType}", LogLevel.Info);

            if (_clientSettings.AuthType == AuthType.Ptc)
                await _client.DoPtcLogin(_clientSettings.PtcUsername, _clientSettings.PtcPassword);
            else if (_clientSettings.AuthType == AuthType.Google)
                await _client.DoGoogleLogin(_clientSettings);

            while (true)
            {
                try
                {                    
                    await _client.SetServer();
                    await _client.GetProfile();
                    if (_clientSettings.AutoEvolve)
                        await EvolveAllPokemonWithEnoughCandy();
                    if (_clientSettings.AutoTransfer)
                        await TransferDuplicatePokemon(_clientSettings.TransferOnlyWeak,true);
                    await RecycleItems();
                    await RepeatAction(25, async () => await ExecuteFarmingPokestopsAndPokemons(_client));

                    /*
                * Example calls below
                *
                var profile = await _client.GetProfile();
                var settings = await _client.GetSettings();
                var mapObjects = await _client.GetMapObjects();
                var inventory = await _client.GetInventory();
                var pokemons = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Pokemon).Where(p => p != null && p?.PokemonId > 0);
                */
                }
                catch (Exception ex)
                {
                    Logger.Write($"Exception: {ex}", LogLevel.Error);
                    Logger.PushFormInfo("wipe", "");
                }

                await Task.Delay(10000);
            }
        }

        public async Task RepeatAction(int repeat, Func<Task> action)
        {
            for (int i = 0; i < repeat; i++)
                await action();
        }

        private async Task ExecuteFarmingPokestopsAndPokemons(Client client)
        {
            var mapObjects = await client.GetMapObjects();

            var pokeStops = mapObjects.MapCells.SelectMany(i => i.Forts).Where(i => i.Type == FortType.Checkpoint && i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime());

            var orderedPs = pokeStops.OrderBy(x => Math.Pow(Math.Pow((x.Longitude - _client._currentLng), 2) + Math.Pow((x.Latitude - _client._currentLat), 2), 0.5));

            foreach (var pokeStop in orderedPs)
            {
                if (!currentPokeStops.ContainsKey(pokeStop.Id))
                    currentPokeStops.Add(pokeStop.Id, pokeStop);
                Logger.PushMapObject("ps", pokeStop.LureInfo?.LureExpiresTimestampMs > DateTime.UtcNow.ToUnixTime() ? "lured" : "normal", pokeStop.Latitude, pokeStop.Longitude, pokeStop.Id);
            }

            FortData closestPS = null;
            if (FortToMove != null)
            {
                closestPS = FortToMove;
                FortToMove = null;
                stopRoutine = false;
            }
            else
                closestPS = orderedPs.First();
            var update = await client.UpdatePlayerLocation(closestPS.Latitude, closestPS.Longitude);
            //var fortInfo = await client.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
            var fortSearch = await client.SearchFort(closestPS.Id, closestPS.Latitude, closestPS.Longitude);
            Logger.Write($"Farmed XP: {fortSearch.ExperienceAwarded}, Gems: { fortSearch.GemsAwarded}, Eggs: {fortSearch.PokemonDataEgg} Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded)}", LogLevel.Info, "LightBlue");
            Logger.PushFormInfo("xpGained", fortSearch.ExperienceAwarded.ToString());
            Logger.PushFormIntInfo("ps", 1);
            if (stopRoutine) return;

            await Task.Delay(3000);

            if (stopRoutine) return;

            await ExecuteCatchAllNearbyPokemons(client);


        }

        private async Task ExecuteCatchAllNearbyPokemons(Client client)
        {
            var mapObjects = await client.GetMapObjects();

            //var pokemons = mapObjects.MapCells.SelectMany(i => i.NearbyPokemons);
            var pokemons = mapObjects.MapCells.SelectMany(i => i.WildPokemons);
            //var pokemons = mapObjects.MapCells.SelectMany(i => i.CatchablePokemons);

            var orderedPokemons = pokemons.OrderBy(x => Math.Pow(Math.Pow((x.Longitude - _client._currentLng), 2) + Math.Pow((x.Latitude - _client._currentLat), 2), 0.5));

            foreach (var pokemon in orderedPokemons)
            {
                Logger.PushMapObject("pm", pokemon.PokemonData?.PokemonId.ToString(), pokemon.Latitude, pokemon.Longitude, pokemon.EncounterId.ToString());
            }

            foreach (var pokemon in orderedPokemons)
            {
                if (stopRoutine) return;
                var update = await client.UpdatePlayerLocation(pokemon.Latitude, pokemon.Longitude);

                var encounterPokemonResponse = await client.EncounterPokemon(pokemon.EncounterId, pokemon.SpawnpointId);
                var pokemonCP = encounterPokemonResponse?.WildPokemon?.PokemonData?.Cp;
                var pokeball = await GetBestBall(pokemonCP);

                CatchPokemonResponse caughtPokemonResponse;
                do
                {
                    if (encounterPokemonResponse?.CaptureProbability?.CaptureProbability_?.First() < 0.4)
                    {
                        //Throw berry is we can
                        await UseBerry(pokemon.EncounterId, pokemon.SpawnpointId);
                    }

                    caughtPokemonResponse = await client.CatchPokemon(pokemon.EncounterId, pokemon.SpawnpointId, pokemon.Latitude, pokemon.Longitude, pokeball);
                    await Task.Delay(2000);
                }
                while (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed);

                var caught = caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess;

                Logger.Write(caught ? $"We caught a {pokemon.PokemonData.PokemonId} with CP {encounterPokemonResponse?.WildPokemon?.PokemonData?.Cp} using a {pokeball}, xp: {caughtPokemonResponse.Scores.Xp?.Sum()}" : $"{pokemon.PokemonData.PokemonId} with CP {encounterPokemonResponse?.WildPokemon?.PokemonData?.Cp} got away while using a {pokeball}...", LogLevel.Info, caught ? "Green" : "Yellow");
                if (caught)
                {
                    Logger.PushFormInfo("xpGained", caughtPokemonResponse.Scores.Xp?.Sum().ToString());
                    Logger.PushFormInfo("sdGained", caughtPokemonResponse.Scores.Stardust?.Sum().ToString());
                    Logger.PushFormIntInfo("pm", 1);
                }
                Logger.PushMapObject("pm_rm", pokemon.PokemonData.PokemonId.ToString(), pokemon.Latitude, pokemon.Longitude, pokemon.EncounterId.ToString());

                await Task.Delay(5000);
            }
        }
        
        private async Task EvolveAllPokemonWithEnoughCandy()
        {
            var pokemonToEvolve = await _inventory.GetPokemonToEvolve();
            foreach (var pokemon in pokemonToEvolve)
            {
                var evolvePokemonOutProto = await _client.EvolvePokemon((ulong)pokemon.Id);

                if (evolvePokemonOutProto.Result == EvolvePokemonOut.Types.EvolvePokemonStatus.PokemonEvolvedSuccess)
                {
                    Logger.Write($"Evolved {pokemon.PokemonId} successfully for {evolvePokemonOutProto.ExpAwarded}xp", LogLevel.Info, "Cyan");
                    Logger.PushFormInfo("xpGained", evolvePokemonOutProto.ExpAwarded.ToString());
                }
                else
                    Logger.Write($"Failed to evolve {pokemon.PokemonId}. EvolvePokemonOutProto.Result was {evolvePokemonOutProto.Result}, stopping evolving {pokemon.PokemonId}", LogLevel.Info, "Red");
                    

                await Task.Delay(3000);
            }
        }

        private async Task TransferDuplicatePokemon(bool onlyWeak, bool keepPokemonsThatCanEvolve = false)
        {
            var duplicatePokemons = await _inventory.GetDuplicatePokemonToTransfer(onlyWeak);

            foreach (var duplicatePokemon in duplicatePokemons)
            {
                var transfer = await _client.TransferPokemon(duplicatePokemon.Id);
                Logger.Write($"Transfer {duplicatePokemon.PokemonId} with {duplicatePokemon.Cp} CP", LogLevel.Info, "Magenta");
                await Task.Delay(500);
            }
        }

        private async Task RecycleItems()
        {
            var items = await _inventory.GetItemsToRecycle(_clientSettings);

            foreach (var item in items)
            {
                var transfer = await _client.RecycleItem((AllEnum.ItemId)item.Item_, item.Count);
                Logger.Write($"Recycled {item.Count}x {(AllEnum.ItemId)item.Item_}", LogLevel.Info);
                await Task.Delay(500);
            }
        }

        private async Task<MiscEnums.Item> GetBestBall(int? pokemonCp)
        {
            var pokeBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_POKE_BALL);
            var greatBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_GREAT_BALL);
            var ultraBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_ULTRA_BALL);
            var masterBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_MASTER_BALL);

            if (masterBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_MASTER_BALL;
            else if (ultraBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (ultraBallsCount > 0 && pokemonCp >= 600)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 600)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (greatBallsCount > 0 && pokemonCp >= 350)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (pokeBallsCount > 0)
                return MiscEnums.Item.ITEM_POKE_BALL;
            if (greatBallsCount > 0)
                return MiscEnums.Item.ITEM_GREAT_BALL;
            if (ultraBallsCount > 0)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            if (masterBallsCount > 0)
                return MiscEnums.Item.ITEM_MASTER_BALL;

            return MiscEnums.Item.ITEM_POKE_BALL;
        }

        public async Task UseBerry(ulong encounterId, string spawnPointId)
        {
            var inventoryBalls = await _inventory.GetItems();
            var berries = inventoryBalls.Where(p => (ItemId) p.Item_ == ItemId.ItemRazzBerry);
            var berry = berries.FirstOrDefault();

            if (berry == null)
                return;
            
            var useRaspberry = await _client.UseCaptureItem(encounterId, AllEnum.ItemId.ItemRazzBerry, spawnPointId);
            Logger.Write($"Use Rasperry. Remaining: {berry.Count}", LogLevel.Info);
            await Task.Delay(3000);
        }
    }
}