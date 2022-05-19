using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Colorgrade
{
    public class Colorgrade : Mod
    {
        internal static IMonitor StaticMonitor;

        //toggle and table data
        internal static bool EnableColorTables = false;
        internal static List<ColorTable> ColorTables;
        internal static List<ColorTable> MatchingColorTables;

        //search criteria
        internal static string SearchWeather = "any";
        internal static string SearchSeason = "Spring";
        internal static string SearchLocation = "FarmHouse";
        internal static int SearchFloor = 0;
        internal static int SearchTime = 600;


        //rendering variables
        private static RenderTarget2D VanillaWorldScreen;
        private static RenderTarget2D LUTWorldScreen;
        private static Effect LUTEffect;
        private static string LookupTableAFilename = string.Empty;
        private static string LookupTableBFilename = string.Empty;
        private static Texture2D LookupTableA;
        private static Texture2D LookupTableB;
        private static float LookupTableProgress;

        public override void Entry(IModHelper helper)
        {
            StaticMonitor = Monitor;
            ColorTables = new List<ColorTable>();
            helper.Events.GameLoop.DayStarted += OnDayStart;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnTick;
            helper.Events.Player.Warped += OnWarp;

            byte[] lfxbc = File.ReadAllBytes(Path.Join(helper.DirectoryPath, "lut.mgfx"));
            LUTEffect = new Effect(Game1.graphics.GraphicsDevice, lfxbc);

            var harmony = new Harmony("mod.kitchen.colorgrade");
            harmony.Patch(
               original: AccessTools.Method(typeof(Game1), nameof(Game1.DrawWorld), new Type[] { typeof(GameTime), typeof(RenderTarget2D) }),
               prefix: new HarmonyMethod(typeof(Colorgrade), nameof(DrawWorld_Pre)),
               postfix: new HarmonyMethod(typeof(Colorgrade), nameof(DrawWorld_Post))
            );

            ColorTables = Helper.Data.ReadJsonFile<List<ColorTable>>("colortables.json");
        }

        private void FindMatchingColorTables()
        {
            EnableColorTables = false;
            //filter by location
            var ct1 = ColorTables.Where(x => x.Location == SearchLocation);
            if (!ct1.Any()) return;
            //filter by floor
            var ct2 = ct1.Where(x => x.MinFloor <= SearchFloor && x.MaxFloor >= SearchFloor);
            if (!ct2.Any()) return;
            //filter by weather
            var ct3 = ct2.Where(x => x.Weather == SearchWeather);
            if (!ct3.Any())
            {
                ct3 = ct2.Where(x => x.Weather == "any");
                if (!ct3.Any()) return;
            }
            //filter by season
            var ct4 = ct3.Where(x => x.Season == SearchSeason);
            if (!ct4.Any())
            {
                ct4 = ct3.Where(x => x.Season == "any");
                if (!ct4.Any()) return;
            }
            EnableColorTables = true;
            MatchingColorTables = ct4.OrderBy(x => x.GetTime()).ToList();
            UpdateCurrentColorTables();
        }

        public static int GetPreciseTimeOfDay()
        {
            int num = (int)Math.Floor(Utility.ConvertTimeToMinutes(Game1.timeOfDay) + (float)Game1.gameTimeInterval / 7000f * 10f);
            return Utility.ConvertMinutesToTime(num);
        }

        public void UpdateCurrentColorTables()
        {
            if (!EnableColorTables) return;
            if(MatchingColorTables.Count <= 0)
            {
                EnableColorTables = false;
                return;
            }
            //Monitor.Log("Updating Colorgrade...", LogLevel.Info);
            ColorTable A =  null, B = null;
            int ATime = 0, BTime = 0;
            float progress = 0f;
            int currentTime = GetPreciseTimeOfDay();
            if (MatchingColorTables.Count == 1)
            {
                A = B = MatchingColorTables.First();
                ATime = BTime = A.GetTime();
            } else
            {
                foreach (var ct in MatchingColorTables)
                {
                    if(currentTime > ct.GetTime())
                    {
                        A = ct;
                        ATime = ct.GetTime();
                    } else
                    {
                        B = ct;
                        BTime = ct.GetTime();
                        break;
                    }
                }
                if (A == null) A = MatchingColorTables.First();
                if (B == null) B = A;
                if (currentTime > BTime || BTime <= ATime) progress = 1f;
                else
                {
                    //calculate progress
                    //fix the math for treating 100 as an hour
                    int ATimeInMinutes = ((ATime / 100) * 60) + (ATime % 100);
                    int BTimeInMinutes = ((BTime / 100) * 60) + (BTime % 100);
                    int currentTimeInMinutes = ((currentTime / 100) * 60) + (currentTime % 100);
                    float range = BTimeInMinutes - ATimeInMinutes;
                    float value = currentTimeInMinutes - ATimeInMinutes;
                    progress = value / range;
                }
            }
            //apply A, B, and progress
            //Monitor.Log("LUT progress: " + (float)Math.Round(progress, 2));
            LUTEffect.Parameters["LUTProgress"].SetValue((float)Math.Round(progress, 2));
            if(A.Filename != LookupTableAFilename)
            {
                LookupTableAFilename = A.Filename;
                if (LookupTableA != null) LookupTableA.Dispose();
                LookupTableA = Helper.ModContent.Load<Texture2D>(Path.Join("ColorTables", A.Filename));
                LUTEffect.Parameters["LUT1"].SetValue(LookupTableA);
            }
            if(B.Filename != LookupTableBFilename)
            {
                LookupTableBFilename = B.Filename;
                if (LookupTableB != null) LookupTableB.Dispose();
                LookupTableB = Helper.ModContent.Load<Texture2D>(Path.Join("ColorTables", B.Filename));
                LUTEffect.Parameters["LUT2"].SetValue(LookupTableB);
            }
        }

        private void OnWarp(object sender, StardewModdingAPI.Events.WarpedEventArgs e)
        {
            //update location, floor
            //floor does not gradiate, just min/max for matching
            SearchLocation = Game1.player.currentLocation.Name;
            if (char.IsDigit(SearchLocation[SearchLocation.Length - 1]))
            {
                var stack = new Stack<char>();
                for (var i = SearchLocation.Length - 1; i >= 0; i--)
                {
                    if (!char.IsNumber(SearchLocation[i])) break;
                    stack.Push(SearchLocation[i]);
                    SearchLocation = SearchLocation.Remove(i);
                }
                SearchFloor = int.Parse(stack.ToArray());
            }
            else
            {
                SearchFloor = 0;
            }
            //Monitor.Log("LUT Location: " + SearchLocation, LogLevel.Info);
            //Monitor.Log("LUT Floor: " + SearchFloor, LogLevel.Info);
            FindMatchingColorTables();
        }

        private void OnTick(object sender, StardewModdingAPI.Events.OneSecondUpdateTickedEventArgs e)
        {
            //update time
            if (!EnableColorTables) return;
            int tod = GetPreciseTimeOfDay();
            if (tod != SearchTime)
            {
                SearchTime = tod;
                UpdateCurrentColorTables();
                //Monitor.Log("LUT Time: " + SearchTime, LogLevel.Info);
            }
        }

        private void OnDayStart(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            //update weather and season
            SearchWeather = Game1.weatherForTomorrow.ToLower();
            //Monitor.Log("LUT Weather: " + SearchWeather, LogLevel.Info);

            SearchSeason = Game1.currentSeason.ToLower();
            //Monitor.Log("LUT Season: " + SearchSeason, LogLevel.Info);

            SearchLocation = Game1.player.currentLocation.Name;
            //Monitor.Log("LUT Location: " + SearchLocation, LogLevel.Info);
            FindMatchingColorTables();
        }

        public static bool DrawWorld_Pre(GameTime time, ref RenderTarget2D target_screen)
        {
            if (!EnableColorTables) return true;
            VanillaWorldScreen = target_screen;
            if (VanillaWorldScreen == null)
            {
                if(LUTWorldScreen == null)
                    LUTWorldScreen = new RenderTarget2D(Game1.graphics.GraphicsDevice, Game1.graphics.PreferredBackBufferWidth, Game1.graphics.PreferredBackBufferHeight);
            }
            else
            {
                if (LUTWorldScreen == null || target_screen.GraphicsDevice != LUTWorldScreen.GraphicsDevice || target_screen.Width != LUTWorldScreen.Width || target_screen.Height != LUTWorldScreen.Height)
                {
                    if (LUTWorldScreen != null) LUTWorldScreen.Dispose();
                    LUTWorldScreen = new RenderTarget2D(target_screen.GraphicsDevice, target_screen.Width, target_screen.Height);
                }
            }
            target_screen = LUTWorldScreen;
            Game1.SetRenderTarget(target_screen);
            return true;
        }

        public static void DrawWorld_Post(GameTime time, RenderTarget2D target_screen)
        {
            if (!EnableColorTables) return;
            target_screen = VanillaWorldScreen;
            Game1.SetRenderTarget(target_screen);
            Game1.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, LUTEffect, null);
            Game1.spriteBatch.Draw(LUTWorldScreen, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.White);
            Game1.spriteBatch.End();
        }
    }
}
