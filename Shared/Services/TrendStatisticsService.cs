namespace TrendStatisticsShared.Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Trending;
	using TrendStatisticsShared.Models;

	/// <summary>
	/// Service for retrieving trend statistics from DataMiner elements.
	/// </summary>
	public class TrendStatisticsService
	{
		private readonly GQIDMS _dms;
		private readonly IGQILogger _logger;
		private readonly TrendStatisticsConfig _config;
		private readonly HistogramInterval[] _intervals;

		/// <summary>
		/// Initializes a new instance of the <see cref="TrendStatisticsService"/> class.
		/// </summary>
		/// <param name="dms">The DataMiner DMS interface.</param>
		/// <param name="logger">The logger instance.</param>
		/// <param name="config">The trend statistics configuration.</param>
		public TrendStatisticsService(GQIDMS dms, IGQILogger logger, TrendStatisticsConfig config)
		{
			_dms = dms ?? throw new ArgumentNullException(nameof(dms));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_config = config ?? throw new ArgumentNullException(nameof(config));

			// Initialize histogram intervals
			var intervalList = new List<HistogramInterval>();
			for (int i = 0; i < _config.HistogramIntervals; i++)
			{
				intervalList.Add(new HistogramInterval(double.MinValue, double.MinValue));
			}

			_intervals = intervalList.ToArray();
		}

		/// <summary>
		/// Retrieves table primary keys for rows that have trending enabled and match the subscription filter.
		/// Uses GetPartialTableMessage with subscription filters for server-side filtering.
		/// </summary>
		/// <param name="element">The element reference.</param>
		/// <returns>Array of primary keys, or empty array if failed.</returns>
		public string[] GetTablePrimaryKeys(IElementReference element)
		{
			try
			{
				// Build filters array: always include trend filter, plus optional user filter
				var filters = BuildFiltersArray();

				// Use GetPartialTableMessage which supports filters
				var tableRequest = new GetPartialTableMessage(
					element.DataMinerID,
					element.ElementID,
					_config.ParameterId,
					filters);

				if (filters != null && filters.Length > 0)
				{
					_logger.Information($"Requesting table with filters: {string.Join("; ", filters)}");
				}

				var tableResponse = _dms.SendMessage(tableRequest) as ParameterChangeEventMessage;

				if (tableResponse?.NewValue?.ArrayValue == null)
				{
					_logger.Warning($"No table data returned for element {element.Key}, parameter {_config.ParameterId}");
					return Array.Empty<string>();
				}

				// Parse the table to extract primary keys
				var primaryKeys = ParseTablePrimaryKeys(tableResponse, element);

				_logger.Information($"Retrieved {primaryKeys.Length} trended primary keys from element {element.Key}");
				return primaryKeys;
			}
			catch (Exception ex)
			{
				_logger.Error($"Failed to get table primary keys for element {element.Key}: {ex.Message}");
				return Array.Empty<string>();
			}
		}

		private string[] BuildFiltersArray()
		{
			var filtersList = new List<string>();

			filtersList.Add($"trend=avg,{_config.ParameterId}|rt,{_config.ParameterId}");

			if (!string.IsNullOrWhiteSpace(_config.SubscriptionFilter))
			{
				filtersList.Add(_config.SubscriptionFilter);
			}

			return filtersList.ToArray();
		}

		private string[] ParseTablePrimaryKeys(ParameterChangeEventMessage tableMessage, IElementReference element)
		{
			// Table structure: ArrayValue contains columns (not rows)
			// By DataMiner convention, columns[0] is the primary key column

			var columns = tableMessage.NewValue.ArrayValue;

			if (columns == null || columns.Length == 0)
			{
				_logger.Warning($"Table has no columns for element {element.Key}");
				return Array.Empty<string>();
			}

			var primaryKeyColumn = columns[0];
			if (primaryKeyColumn?.ArrayValue == null || primaryKeyColumn.ArrayValue.Length == 0)
			{
				_logger.Warning($"Primary key column is empty for element {element.Key}");
				return Array.Empty<string>();
			}

			// Extract primary keys from the column
			var primaryKeys = new List<string>(primaryKeyColumn.ArrayValue.Length);

			for (int i = 0; i < primaryKeyColumn.ArrayValue.Length; i++)
			{
				var pk = primaryKeyColumn.ArrayValue[i]?.CellValue?.StringValue;
				if (!string.IsNullOrEmpty(pk))
				{
					primaryKeys.Add(pk);
				}
			}

			_logger.Information($"Parsed {primaryKeys.Count} primary keys from table (total rows: {primaryKeyColumn.ArrayValue.Length})");
			return primaryKeys.ToArray();
		}

		/// <summary>
		/// Retrieves trend statistics for the specified element and table index.
		/// </summary>
		/// <param name="element">The element reference.</param>
		/// <param name="indexValue">The table index value.</param>
		/// <returns>The trend statistics, or null if failed or no data available.</returns>
		public TrendStatistics GetTrendStatistics(IElementReference element, string indexValue)
		{
			try
			{
				var statistics = _dms.SendMessage(new GetHistogramTrendDataMessage(element.DataMinerID, element.ElementID, _config.ParameterId, indexValue)
				{
					StartTime = DateTime.Now.AddDays(-_config.TrendDays),
					EndTime = DateTime.Now,
					RetrievalWithPrimaryKey = true,
					TrendingType = TrendingType.Auto,
					Intervals = _intervals,
				}) as GetHistogramTrendDataResponseMessage;

				return statistics?.TrendStatistics?.Values?.FirstOrDefault();
			}
			catch (Exception ex)
			{
				_logger.Warning($"Failed to get trend statistics for element {element.Key}, index {indexValue}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Creates a GQI row from trend statistics data.
		/// </summary>
		/// <param name="element">The element reference.</param>
		/// <param name="indexValue">The table index value.</param>
		/// <param name="statistics">The trend statistics.</param>
		/// <returns>A GQI row, or null if creation failed.</returns>
		public GQIRow CreateRow(IElementReference element, string indexValue, TrendStatistics statistics)
		{
			try
			{
				return new GQIRow(new GQICell[]
				{
					new GQICell() { Value = element.Key },
					new GQICell() { Value = indexValue },
					new GQICell() { Value = statistics.Average },
				});
			}
			catch (Exception ex)
			{
				_logger.Error($"Failed to create GQI row for element {element.Key}, index {indexValue}: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Checks if there are more pages available for the given elements and processing state.
		/// </summary>
		/// <param name="elements">The list of elements.</param>
		/// <param name="state">The current processing state.</param>
		/// <returns>True if there are more pages available; otherwise, false.</returns>
		public bool HasNextPage(IReadOnlyList<IElementReference> elements, ProcessingState state)
		{
			if (state.ElementIndex < elements.Count)
			{
				if (state.IndexPosition > 0)
				{
					return true;
				}
				else
				{
					var element = elements[state.ElementIndex];
					if (element != null)
					{
						try
						{
							var primaryKeys = GetTablePrimaryKeys(element);
							return (primaryKeys?.Length ?? 0) > 0 || (state.ElementIndex + 1) < elements.Count;
						}
						catch (Exception ex)
						{
							_logger.Warning($"Failed to check primary keys for hasNextPage on element {element.Key}: {ex.Message}");
							return (state.ElementIndex + 1) < elements.Count;
						}
					}
					else
					{
						return (state.ElementIndex + 1) < elements.Count;
					}
				}
			}

			return false;
		}
	}
}