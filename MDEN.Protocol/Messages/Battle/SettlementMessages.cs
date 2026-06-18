namespace MDEN.Protocol.Messages.Battle
{
    public class SettlementResultPush
    {
        public string[] DragonCoinUids { get; set; }
        public string[] ComboUids { get; set; }
        public string[] PerfectUids { get; set; }
        public string[] SleepwalkUids { get; set; }
        public SettlementPlayerNameEntry[] PlayerNames { get; set; }
        public SettlementChartEntry[] PlayedCharts { get; set; }
    }

    public class SettlementPlayerNameEntry
    {
        public string Uid { get; set; }
        public string Name { get; set; }
    }

    public class SettlementChartEntry
    {
        public string ChartKey { get; set; }
        public int Difficulty { get; set; }
        public string ChartName { get; set; }
        public float DurationSeconds { get; set; }
    }
}
