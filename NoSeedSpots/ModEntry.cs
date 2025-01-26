using System;
using System.Reflection;
using GenericModConfigMenu;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Audio;
using StardewValley.Buildings;
using StardewValley.Extensions;
using StardewValley.GameData.Objects;
using StardewValley.Locations;
using xTile.Layers;
using xTile.Tiles;

namespace EasyToolbar
{
    //todo:
    //replace all seed spots on map load with artifact spots - DONE
    //allow tuning frequency of seed spots - DONE
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        /*********** Properties *********/
        /// <summary>The mod configuration from the player.</summary>
        private ModConfig Config;
        private ModData modData;

        GameLocation currentLocation;     
        List<Vector2> alreadyKnownSeedSpotsInLocation = new();  

        /*********** Public methods *********/

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            //Setup
            Config = Helper.ReadConfig<ModConfig>();
            SetEvents(helper);
        }

        #region Setting up

        private void SetEvents(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.Player.Warped += OnPlayerWarped;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saved += OnSaved;
        }

        private void LoadGenericModConfigSettings()
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            if (!IsConfigFileValid()) return;
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: this.ModManifest,
                reset: () => this.Config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.Config)
            );

           /* configMenu.AddBoolOption(
                 mod: this.ModManifest,
                 name: () => "Show notification message",
                 tooltip: () => "Shows a notification in the bottom-left corner of the screen when a ladder or shaft is discovered.",
                 getValue: () => Config.PlayNotificationMessage,
                 setValue: value => Config.PlayNotificationMessage = value
             );*/

             configMenu.AddNumberOption(
                mod: this.ModManifest,
                 name: () => "Seed spot spawn chance",
                 tooltip: () => "Allows you to control the chances of an artifact spot being converted into a seed spot on generation, ranging from 16.6%(vanilla) to 0%(only artifact spots will spawn).",
                 min: 0,
                 max: 16.6f,
                 interval: 0.1f,
                 getValue: () => Config.SeedSpotSpawnChance,
                 setValue: value => Config.SeedSpotSpawnChance = value
             );



#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }

        #endregion

        #region Event callbacks
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            LoadGenericModConfigSettings();
        }

        //Called when player changes map locations in general
        private void OnPlayerWarped(object sender, WarpedEventArgs e)
        {
            if (currentLocation != null && (currentLocation.IsOutdoors || currentLocation is MineShaft))
            {
                //Monitor.Log($"Saving seed spots in {currentLocation.Name}",LogLevel.Debug);
                foreach (var seedSpot in alreadyKnownSeedSpotsInLocation)
                {
                    var spotData = new SeedSpotData();
                    spotData.Position = seedSpot;
                    spotData.LocationId = currentLocation.Name;
                    if (!modData.SeedSpotDataList.Contains(spotData)) modData.SeedSpotDataList.Add(spotData);
                }
            }

            currentLocation = e.NewLocation;
            alreadyKnownSeedSpotsInLocation.Clear();
            if (currentLocation != null
            && (currentLocation.IsOutdoors || currentLocation is MineShaft))
            {
                if (modData.SeedSpotDataList.Count > 0)
                {
                    //Monitor.Log($"Looking for existing seed spots in {currentLocation.Name}", LogLevel.Debug);
                    var list = modData.SeedSpotDataList.FindAll(x => x.LocationId == currentLocation.Name);
                    if (list != null && list.Count > 0)
                    {
                        foreach (var item in list)
                        {   
                            if(!currentLocation.isObjectAtTile((int)item.Position.X, (int)item.Position.Y)) 
                            {
                                Monitor.Log($"Did not find object at tile {item.Position.X},{item.Position.Y} on map {item.LocationId}. Is there something wrong with the mod save file?");
                                modData.SeedSpotDataList.Remove(item);
                                continue;
                            }
                            else if (currentLocation.getObjectAtTile((int)item.Position.X, (int)item.Position.Y).QualifiedItemId != "(O)SeedSpot")
                            {
                                //Something happened and the seed spot isn't there anymore. Season changes, hoeing, etc. Remove it from the list.
                                modData.SeedSpotDataList.Remove(item);
                                continue;
                            }
                            alreadyKnownSeedSpotsInLocation.Add(item.Position);
                        }
                    }
                }

                LookForSeedSpots();
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            var model = this.Helper.Data.ReadSaveData<ModData>("SeedSpotData");
            if (model == null) model = new ModData();

            int? day = Game1.player.dayOfMonthForSaveGame;
            int? month = Game1.player.seasonForSaveGame;
            int? year = Game1.player.yearForSaveGame;

            //Monitor.Log($"Current date is [{day} {(month == null ? "null" : ConvertIntToSeason((int)month))} year {year}].", LogLevel.Debug);

            if (model.day != null & model.month != null && model.year != null)
            {
                int modelDay = (int)model.day + 1;
                int modelMonth = (int)model.month;
                int modelYear = (int)model.year;
                if(modelDay >= 29)
                {
                    modelDay = 1;
                    modelMonth += 1;
                    if (modelMonth >=4)
                    {
                        modelMonth = 0;
                        modelYear += 1;
                    }
                }

                if (modelDay != day || modelMonth != month || modelYear != year)
                {
                    string modelDayString = modelDay.ToString();
                    string modelMonthString = ConvertIntToSeason(modelMonth);
                    string modelYearString = modelYear.ToString();
                    Monitor.Log($"Model load error: Loaded date is [{modelDayString} {modelMonthString} year {modelYearString}] but current date is [{day} {(month == null ? "null" : ConvertIntToSeason((int)month))} year {year}]."
                    + " This probably means you reloaded an old save and old seed spot locations might no longer be valid."
                    + " Affected spots will be rerolled and no action is required on your part. ", LogLevel.Debug);
                }

            }
            modData = model;
        }

        private void OnSaved(object sender, SavedEventArgs e)
        {
            modData.day = Game1.player.dayOfMonthForSaveGame;
            modData.month = Game1.player.seasonForSaveGame;
            modData.year = Game1.player.yearForSaveGame;
            this.Helper.Data.WriteSaveData("SeedSpotData", modData);
        }


        #endregion

        #region Actually doing stuff

        private void LookForSeedSpots()
        {         

            var objs = currentLocation.Objects;
            List<Vector2> seedSpotKeys = new();

            foreach(var key in objs.Keys)
            {
                objs.TryGetValue(key, out var obj);

                if (obj == null) continue;

                if (obj.QualifiedItemId == "(O)SeedSpot")
                {
                    if(alreadyKnownSeedSpotsInLocation.Contains(key)) continue;
                    Monitor.Log($"Found seed spot at {key}");

                    seedSpotKeys.Add(key);
                    alreadyKnownSeedSpotsInLocation.Add(key);
                }
            }

            if(seedSpotKeys.Count > 0)
            {
                foreach(var key in seedSpotKeys)
                {
                    float seedSpawnChance = Config.SeedSpotSpawnChance;
                    if (seedSpawnChance < 0)
                    {
                        Config.SeedSpotSpawnChance = 0;
                        seedSpawnChance = 0;
                    }
                    if (seedSpawnChance > 0)
                    {
                        if (seedSpawnChance > 16.6f)
                        {
                            Config.SeedSpotSpawnChance = 16.6f;
                            seedSpawnChance = 16.6f;
                        }
                        float realChance = (seedSpawnChance * 100) / 16.6f;

                        Random random = Utility.CreateDaySaveRandom();

                        bool willSpawnSeeds = random.NextBool(realChance / 100);

                        if (willSpawnSeeds) continue;
                    }
                    Monitor.Log($"Converting seed spot at {key}");
                    currentLocation.setObjectAt(key.X, key.Y, ItemRegistry.Create<StardewValley.Object>("(O)590"));
                    alreadyKnownSeedSpotsInLocation.Remove(key);
                }                
            }
        }

        private string ConvertIntToSeason(int seasonInt)
        {
            return seasonInt switch
            {
                0 => "spring",
                1 => "summer",
                2 => "fall",
                3 => "winter",
                _ => "default"
            };
        }

        #endregion

        #region Safety checks

        private bool ValidPlayerChecks(Farmer currentPlayer)
        {
            if (!currentPlayer.IsLocalPlayer) return false;
            if (currentPlayer.IsBusyDoingSomething()) return false;

            return true;
        }
        private bool IsConfigFileValid()
        {
            bool valid = true;
            if (this.Config == null)
            {
                valid = false;
                Monitor.Log($"Warning! The mod config file is not valid.",
                LogLevel.Debug);
            }

            return valid;
        }
        #endregion
    }
}

