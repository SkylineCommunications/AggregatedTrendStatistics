namespace TrendStatisticsShared.Models
{
	/// <summary>
	/// Represents configuration for trend statistics retrieval.
	/// </summary>
	public class TrendStatisticsConfig
	{
		/// <summary>
		/// Gets or sets the maximum number of rows to return per page.
		/// </summary>
		public int MaxPageSize { get; set; } = 10;

		/// <summary>
		/// Gets or sets the parameter ID of the table column to analyze.
		/// </summary>
		public int ParameterId { get; set; }

		/// <summary>
		/// Gets or sets the number of days to look back for trend data.
		/// </summary>
		public int TrendDays { get; set; } = 30;

		/// <summary>
		/// Gets or sets the number of histogram intervals to use for trend analysis.
		/// </summary>
		public int HistogramIntervals { get; set; } = 100;

		/// <summary>
		/// Gets or sets the subscription filter to apply when retrieving table data.
		/// Uses DataMiner's native subscription filter format.
		/// Format: "value={columnPid}:{filterValue}" (e.g., "value=1002:Active" or "value=1003:Enabled")
		/// This filter is applied server-side by DataMiner for optimal performance.
		/// </summary>
		public string SubscriptionFilter { get; set; }
	}
}