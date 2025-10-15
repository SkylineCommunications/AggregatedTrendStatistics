namespace TrendStatisticsShared.Models
{
	public class TrendStatisticsConfig
	{
		public int MaxPageSize { get; set; } = 10;

		public int ParameterId { get; set; }

		public int TrendDays { get; set; } = 30;

		public int HistogramIntervals { get; set; } = 100;
	}
}