namespace Colorgrade
{
    public class ColorTable
    {
        public string Location { get; set; } = string.Empty;
        public string Weather { get; set; } = "any";
        public string Time { get; set; } = "any";
        public string Filename { get; set; } = string.Empty;
        public string Season { get; set; } = "any";
        public int MinFloor { get; set; } = 0;
        public int MaxFloor { get; set; } = 9999;

        internal int GetTime()
        {
            if (Time.ToLower() == "any") return 0;
            if (!int.TryParse(Time, out int time)) {
                Colorgrade.StaticMonitor.Log("Invalid time specification for ColorTable: " + Time, StardewModdingAPI.LogLevel.Warn);
                return 0;
            }
            return time;
        }
    }
}
