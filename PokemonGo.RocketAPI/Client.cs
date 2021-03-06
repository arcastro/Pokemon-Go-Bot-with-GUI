﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Protobuf;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Helpers;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.Login;
using static PokemonGo.RocketAPI.GeneratedCode.Response.Types;

namespace PokemonGo.RocketAPI
{
    public class Client
    {
        private readonly ISettings _settings;
        private readonly HttpClient _httpClient;
        private AuthType _authType = AuthType.Google;
        private string _accessToken;
        private string _apiUrl;
        private Request.Types.UnknownAuth _unknownAuth;
        private Random r = new Random();

        public bool InterruptMovement = false;

        public double _currentLat;
        public double _currentLng;

        public Client(ISettings settings)
        {
            _settings = settings;
            SetCoordinates(_settings.DefaultLatitude, _settings.DefaultLongitude);

            string proxyUri = _settings.ProxyUri;

            NetworkCredential proxyCreds = new NetworkCredential(
                _settings.ProxyLogin,
                _settings.ProxyPass
            );

            WebProxy proxy = new WebProxy(proxyUri, false)
            {
                UseDefaultCredentials = false,
                Credentials = proxyCreds,
            };

            //Setup HttpClient and create default headers
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = false,
                Proxy = _settings.UseProxy ? proxy : null
            };
            _httpClient = new HttpClient(new RetryHandler(handler));
            
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Niantic App");
                //"Dalvik/2.1.0 (Linux; U; Android 5.1.1; SM-G900F Build/LMY48G)");
            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type",
                "application/x-www-form-urlencoded");
        }

        private void SetCoordinates(double lat, double lng)
        {
            _currentLat = lat;
            _currentLng = lng;
        }

        public async Task DoGoogleLogin(ISettings settings)
        {
            _authType = AuthType.Google;
            if (_settings.GoogleRefreshToken != string.Empty)
            {
                var tokenResponse = await GoogleLogin.GetAccessToken(_settings.GoogleRefreshToken, settings);
                _accessToken = tokenResponse.id_token;
            }
            
            if (_accessToken == null)
            {
                var tokenResponse = await GoogleLogin.GetAccessToken(settings);
                _accessToken = tokenResponse.id_token;
                _settings.GoogleRefreshToken = tokenResponse.access_token;
            }
        }

        public async Task DoPtcLogin(string username, string password)
        {
            _accessToken = await PtcLogin.GetAccessToken(username, password, _settings);
            _authType = AuthType.Ptc;
        }

        public async Task<PlayerUpdateResponse> UpdatePlayerLocation(double lat, double lng, bool forceMove = false)
        {
            double cLat = _currentLat;
            double cLng = _currentLng;
            if (forceMove)
                InterruptMovement = false;
            PlayerUpdateResponse updateResponse = null;
            do
            {
                this.SetCoordinates(lat, lng);
                Random r = new Random();
                var nextLat = _currentLat;
                var nextLng = _currentLng;
                bool delayed = false;

                double nextMoveSpeedFactor = _settings.MoveSpeedFactor + _settings.MoveSpeedFactor * r.NextDouble() * 0.2;

                if (Math.Abs(nextLat - cLat) > 0.0001 * nextMoveSpeedFactor) //around 11 meters
                {
                    nextLat = cLat + ((cLat > lat) ? -0.0001 : 0.0001) * nextMoveSpeedFactor;
                    delayed = true;
                    await Task.Delay(3000);
                }
                if (Math.Abs(nextLng - cLng) > 0.00007 * nextMoveSpeedFactor)
                {
                    nextLng = cLng + ((cLng > lng) ? -0.00007 : 0.00007) * nextMoveSpeedFactor;
                    if (!delayed)
                        await Task.Delay(3000);
                }
                nextLat = nextLat + r.NextDouble() * 0.0000005 * nextMoveSpeedFactor; //0.5m
                nextLng = nextLng + r.NextDouble() * 0.0000001 * nextMoveSpeedFactor;

                this.SetCoordinates(nextLat, nextLng);

                var customRequest = new Request.Types.PlayerUpdateProto()
                {
                    Lat = Utils.FloatAsUlong(nextLat),
                    Lng = Utils.FloatAsUlong(nextLng)
                };
                Logger.PushFormInfo("nextLat", nextLat.ToString());
                Logger.PushFormInfo("nextLng", nextLng.ToString());
                var updateRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 10 + r.NextDouble() * 75,
                    new Request.Types.Requests()
                    {
                        Type = (int)RequestType.PLAYER_UPDATE,
                        Message = customRequest.ToByteString()
                    });
                updateResponse =
                    await
                        _httpClient.PostProtoPayload<Request, PlayerUpdateResponse>($"https://{_apiUrl}/rpc", updateRequest);
                cLat = nextLat; cLng = nextLng;
            } while ((Math.Abs(_currentLat - lat) > 0.00001 || Math.Abs(_currentLng - lng) > 0.00001) && !InterruptMovement); //around 1 meter
            if (InterruptMovement)
                InterruptMovement = false;
            return updateResponse;
        }

        public async Task<bool> SetServer()
        {
            try
            {
                var serverRequest = RequestBuilder.GetInitialRequest(_accessToken, _authType, _currentLat, _currentLng, 10 + r.NextDouble() * 76,
                    RequestType.GET_PLAYER, RequestType.GET_HATCHED_OBJECTS, RequestType.GET_INVENTORY,
                    RequestType.CHECK_AWARDED_BADGES, RequestType.DOWNLOAD_SETTINGS);
                var serverResponse = await _httpClient.PostProto<Request>(Resources.RpcUrl, serverRequest);
                _unknownAuth = new Request.Types.UnknownAuth()
                {
                    Unknown71 = serverResponse.Auth.Unknown71,
                    Timestamp = serverResponse.Auth.Timestamp,
                    Unknown73 = serverResponse.Auth.Unknown73,
                };

                _apiUrl = serverResponse.ApiUrl;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public async Task<GetPlayerResponse> GetProfile()
        {
            var profileRequest = RequestBuilder.GetInitialRequest(_accessToken, _authType, _currentLat, _currentLng, 10 + r.NextDouble() * 76,
                new Request.Types.Requests() { Type = (int)RequestType.GET_PLAYER });
            var profile = await _httpClient.PostProtoPayload<Request, GetPlayerResponse>($"https://{_apiUrl}/rpc", profileRequest);
            Logger.Write($"Player name: {profile.Profile.Username}", colorName: "Red");
            Logger.Write($"Player team: {profile.Profile.Team}", colorName: "Red");
            Logger.PushFormInfo("profileInfo", $"[{profile.Profile.Username}] - [Team {profile.Profile.Team}]");            
            Logger.PushFormInfo("sdGained", $"{profile.Profile.Currency[1].Amount}");
            Logger.PushFormInfo("nextLat", _currentLat.ToString());
            Logger.PushFormInfo("nextLng", _currentLng.ToString());
            return await _httpClient.PostProtoPayload<Request, GetPlayerResponse>($"https://{_apiUrl}/rpc", profileRequest);
        }

        public async Task<DownloadSettingsResponse> GetSettings()
        {
            var settingsRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 10 + r.NextDouble() * 76,
                RequestType.DOWNLOAD_SETTINGS);
            return await _httpClient.PostProtoPayload<Request, DownloadSettingsResponse>($"https://{_apiUrl}/rpc", settingsRequest);
        }

        public async Task<DownloadItemTemplatesResponse> GetItemTemplates()
        {
            var settingsRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 10 + r.NextDouble() * 76,
                RequestType.DOWNLOAD_ITEM_TEMPLATES);
            return
                await
                    _httpClient.PostProtoPayload<Request, DownloadItemTemplatesResponse>($"https://{_apiUrl}/rpc",
                        settingsRequest);
        }



        public async Task<GetMapObjectsResponse> GetMapObjects()
        {
            var customRequest = new Request.Types.MapObjectsRequest()
            {
                CellIds =
                    ByteString.CopyFrom(
                        ProtoHelper.EncodeUlongList(S2Helper.GetNearbyCellIds(_currentLng,
                            _currentLat))),
                Latitude = Utils.FloatAsUlong(_currentLat),
                Longitude = Utils.FloatAsUlong(_currentLng),
                Unknown14 = ByteString.CopyFromUtf8("\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0")
            };

            var mapRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 10 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.GET_MAP_OBJECTS,
                    Message = customRequest.ToByteString()
                },
                new Request.Types.Requests() { Type = (int)RequestType.GET_HATCHED_OBJECTS },
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.GET_INVENTORY,
                    Message = new Request.Types.Time() { Time_ = DateTime.UtcNow.ToUnixTime() }.ToByteString()
                },
                new Request.Types.Requests() { Type = (int)RequestType.CHECK_AWARDED_BADGES },
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.DOWNLOAD_SETTINGS,
                    Message =
                        new Request.Types.SettingsGuid()
                        {
                            Guid = ByteString.CopyFromUtf8("4a2e9bc330dae60e7b74fc85b98868ab4700802e")
                        }.ToByteString()
                });

            return await _httpClient.PostProtoPayload<Request, GetMapObjectsResponse>($"https://{_apiUrl}/rpc", mapRequest);
        }

        public async Task<FortDetailsResponse> GetFort(string fortId, double fortLat, double fortLng)
        {
            var customRequest = new Request.Types.FortDetailsRequest()
            {
                Id = ByteString.CopyFromUtf8(fortId),
                Latitude = Utils.FloatAsUlong(fortLat),
                Longitude = Utils.FloatAsUlong(fortLng),
            };

            var fortDetailRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 10 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.FORT_DETAILS,
                    Message = customRequest.ToByteString()
                });
            return await _httpClient.PostProtoPayload<Request, FortDetailsResponse>($"https://{_apiUrl}/rpc", fortDetailRequest);
        }

        public async Task<FortSearchResponse> SearchFort(string fortId, double fortLat, double fortLng)
        {
            var customRequest = new Request.Types.FortSearchRequest()
            {
                Id = ByteString.CopyFromUtf8(fortId),
                FortLatDegrees = Utils.FloatAsUlong(fortLat),
                FortLngDegrees = Utils.FloatAsUlong(fortLng),
                PlayerLatDegrees = Utils.FloatAsUlong(_currentLat),
                PlayerLngDegrees = Utils.FloatAsUlong(_currentLng)
            };

            var fortDetailRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 30 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.FORT_SEARCH,
                    Message = customRequest.ToByteString()
                });
            return await _httpClient.PostProtoPayload<Request, FortSearchResponse>($"https://{_apiUrl}/rpc", fortDetailRequest);
        }

        public async Task<EncounterResponse> EncounterPokemon(ulong encounterId, string spawnPointGuid)
        {
            var customRequest = new Request.Types.EncounterRequest()
            {
                EncounterId = encounterId,
                SpawnpointId = spawnPointGuid,
                PlayerLatDegrees = Utils.FloatAsUlong(_currentLat),
                PlayerLngDegrees = Utils.FloatAsUlong(_currentLng)
            };

            var encounterResponse = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 30 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.ENCOUNTER,
                    Message = customRequest.ToByteString()
                });
            return await _httpClient.PostProtoPayload<Request, EncounterResponse>($"https://{_apiUrl}/rpc", encounterResponse);
        }

        public async Task<UseItemCaptureRequest> UseCaptureItem(ulong encounterId, AllEnum.ItemId itemId, string spawnPointGuid)
        {
            var customRequest = new UseItemCaptureRequest
            {
                EncounterId = encounterId,
                ItemId = itemId,
                SpawnPointGuid = spawnPointGuid
            };

            var useItemRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 30 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.USE_ITEM_CAPTURE,
                    Message = customRequest.ToByteString()
                });
            return await _httpClient.PostProtoPayload<Request, UseItemCaptureRequest>($"https://{_apiUrl}/rpc", useItemRequest);
        }

        public async Task<CatchPokemonResponse> CatchPokemon(ulong encounterId, string spawnPointGuid, double pokemonLat,
            double pokemonLng, MiscEnums.Item pokeball)
        {
            Random r = new Random();
            int i = r.Next(10);
            var rect = 1.950 + r.NextDouble() * 0.05;
            var spin = 0.85 + r.NextDouble() * 0.15;
            var customRequest = new Request.Types.CatchPokemonRequest()
            {
                EncounterId = encounterId,
                Pokeball = (int)pokeball,
                SpawnPointGuid = spawnPointGuid,
                HitPokemon = 1,
                NormalizedReticleSize = Utils.FloatAsUlong(rect),
                SpinModifier = Utils.FloatAsUlong(spin),
                NormalizedHitPosition = Utils.FloatAsUlong(1)
            };

            var catchPokemonRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 20 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.CATCH_POKEMON,
                    Message = customRequest.ToByteString()
                });
            return
                await
                    _httpClient.PostProtoPayload<Request, CatchPokemonResponse>($"https://{_apiUrl}/rpc", catchPokemonRequest);
        }

        public async Task<TransferPokemonOut> TransferPokemon(ulong pokemonId)
        {
            var customRequest = new TransferPokemon
            {
                PokemonId = pokemonId
            };

            var releasePokemonRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 30 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.RELEASE_POKEMON,
                    Message = customRequest.ToByteString()
                });
            return await _httpClient.PostProtoPayload<Request, TransferPokemonOut>($"https://{_apiUrl}/rpc", releasePokemonRequest);
        }

        public async Task<EvolvePokemonOut> EvolvePokemon(ulong pokemonId)
        {
            var customRequest = new EvolvePokemon
            {
                PokemonId = pokemonId
            };

            var releasePokemonRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 30 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.EVOLVE_POKEMON,
                    Message = customRequest.ToByteString()
                });
            return
                await
                    _httpClient.PostProtoPayload<Request, EvolvePokemonOut>($"https://{_apiUrl}/rpc", releasePokemonRequest);
        }

        public async Task<GetInventoryResponse> GetInventory()
        {
            var inventoryRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 30 + r.NextDouble() * 76, RequestType.GET_INVENTORY);
            return await _httpClient.PostProtoPayload<Request, GetInventoryResponse>($"https://{_apiUrl}/rpc", inventoryRequest);
        }

        public async Task<RecycleInventoryItemResponse> RecycleItem(AllEnum.ItemId itemId, int amount)
        {
            var customRequest = new RecycleInventoryItem
            {
                ItemId = (AllEnum.ItemId)Enum.Parse(typeof(AllEnum.ItemId), itemId.ToString()),
                Count = amount
            };

            var releasePokemonRequest = RequestBuilder.GetRequest(_unknownAuth, _currentLat, _currentLng, 30 + r.NextDouble() * 76,
                new Request.Types.Requests()
                {
                    Type = (int)RequestType.RECYCLE_INVENTORY_ITEM,
                    Message = customRequest.ToByteString()
                });
            return await _httpClient.PostProtoPayload<Request, RecycleInventoryItemResponse>($"https://{_apiUrl}/rpc", releasePokemonRequest);
        }
    }
}
