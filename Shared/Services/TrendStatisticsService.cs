namespace TrendStatisticsShared.Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Trending;
	using TrendStatisticsShared.Models;

	public class TrendStatisticsService
	{
		private readonly GQIDMS _dms;
		private readonly IGQILogger _logger;
		private readonly TrendStatisticsConfig _config;
		private readonly HistogramInterval[] _intervals;

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

		public DynamicTableIndicesResponse GetTableIndices(IElementReference element)
		{
			try
			{
				return _dms.SendMessage(new GetDynamicTableIndices(element.DataMinerID, element.ElementID, _config.ParameterId)
				{
					Filter = GetDynamicTableIndicesType.TrendedOnly,
				}) as DynamicTableIndicesResponse;
			}
			catch (Exception ex)
			{
				_logger.Error($"Failed to get indices for element {element.Key}: {ex.Message}");
				return null;
			}
		}

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
							var indicesResponse = GetTableIndices(element);
							return (indicesResponse?.Indices?.Length ?? 0) > 0 || (state.ElementIndex + 1) < elements.Count;
						}
						catch (Exception ex)
						{
							_logger.Warning($"Failed to check indices for hasNextPage on element {element.Key}: {ex.Message}");
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