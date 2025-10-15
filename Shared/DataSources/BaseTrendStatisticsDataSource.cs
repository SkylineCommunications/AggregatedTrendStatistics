namespace TrendStatisticsShared.DataSources
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using TrendStatisticsShared.Models;
	using TrendStatisticsShared.Services;

	public abstract class BaseTrendStatisticsDataSource : IGQIDataSource, IGQIOnPrepareFetch, IGQIOnInit, IGQIInputArguments
	{
		protected static readonly IReadOnlyList<IElementReference> EmptyElements = new List<IElementReference>().AsReadOnly();

		private readonly ProcessingState _state = new ProcessingState();
		private TrendStatisticsConfig _config;
		private TrendStatisticsService _trendService;

		protected int ParameterId { get; set; }

		protected GQIDMS Dms { get; private set; }

		protected IGQILogger Logger { get; private set; }

		protected abstract IReadOnlyList<IElementReference> Elements { get; }

		protected virtual TrendStatisticsConfig GetConfig()
		{
			return new TrendStatisticsConfig
			{
				ParameterId = ParameterId,
			};
		}

		public virtual OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			Dms = args.DMS;
			Logger = args.Logger;

			return new OnInitOutputArgs();
		}

		public abstract GQIArgument[] GetInputArguments();

		public abstract OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args);

		public virtual GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Element Key"),
				new GQIStringColumn("Table PK"),
				new GQIDoubleColumn("Average"),
			};
		}

		public virtual OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_config = GetConfig();
			_trendService = new TrendStatisticsService(Dms, Logger, _config);

			return OnPrepareFetchCore(args);
		}

		protected abstract OnPrepareFetchOutputArgs OnPrepareFetchCore(OnPrepareFetchInputArgs args);

		public virtual GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var rows = new List<GQIRow>();

			try
			{
				while (rows.Count < _config.MaxPageSize && _state.ElementIndex < Elements.Count)
				{
					var element = Elements[_state.ElementIndex];
					if (element == null)
					{
						Logger.Warning($"Element at index {_state.ElementIndex} is null, skipping to next element.");
						_state.MoveToNextElement();
						continue;
					}

					var indicesResponse = _trendService.GetTableIndices(element);
					if (indicesResponse?.Indices == null)
					{
						Logger.Warning($"No indices response or null indices for element {element.Key}, skipping to next element.");
						_state.MoveToNextElement();
						continue;
					}

					for (int i = _state.IndexPosition; i < indicesResponse.Indices.Length && rows.Count < _config.MaxPageSize; i++)
					{
						var index = indicesResponse.Indices[i];
						if (index?.IndexValue == null)
						{
							Logger.Warning($"Null index at position {i} for element {element.Key}, skipping.");
							_state.IndexPosition++;
							continue;
						}

						var statistics = _trendService.GetTrendStatistics(element, index.IndexValue);
						if (statistics == null)
						{
							_state.IndexPosition++;
							continue;
						}

						Logger.Information($"Row count: {rows.Count}/{_config.MaxPageSize} - Element: {element.Key} - PK: {index.IndexValue} - Average: {statistics.Average}");

						var row = _trendService.CreateRow(element, index.IndexValue, statistics);
						if (row != null)
						{
							rows.Add(row);
						}

						_state.IndexPosition++;
					}

					if (_state.IndexPosition >= indicesResponse.Indices.Length)
					{
						_state.MoveToNextElement();
					}
					else
					{
						break;
					}
				}

				bool hasNextPage = _trendService.HasNextPage(Elements, _state);

				Logger.Information($"Returning page with {rows.Count} rows. Has next page: {hasNextPage}");

				return new GQIPage(rows.ToArray())
				{
					HasNextPage = hasNextPage,
				};
			}
			catch (Exception ex)
			{
				Logger.Error($"Unexpected error in GetNextPage: {ex.Message}");
				Logger.Error($"Stack trace: {ex.StackTrace}");

				return new GQIPage(rows.ToArray())
				{
					HasNextPage = false,
				};
			}
		}
	}
}