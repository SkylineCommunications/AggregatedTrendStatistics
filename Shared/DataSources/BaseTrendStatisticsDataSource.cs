namespace TrendStatisticsShared.DataSources
{
	using System;
	using System.Collections.Generic;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using TrendStatisticsShared.Models;
	using TrendStatisticsShared.Services;

	/// <summary>
	/// Abstract base class for trend statistics data sources.
	/// </summary>
	public abstract class BaseTrendStatisticsDataSource : IGQIDataSource, IGQIOnPrepareFetch, IGQIOnInit, IGQIInputArguments
	{
		protected static readonly IReadOnlyList<IElementReference> EmptyElements = new List<IElementReference>().AsReadOnly();
		
		private readonly ProcessingState _state = new ProcessingState();
		private readonly GQIIntArgument _pidArgument = new GQIIntArgument("Column Parameter ID") { IsRequired = true };
		private readonly GQIStringArgument _subscriptionFilterArgument = new GQIStringArgument("Subscription Filter (Optional)")
		{
			IsRequired = false,
			DefaultValue = string.Empty,
		};
		
		private TrendStatisticsConfig _config;
		private TrendStatisticsService _trendService;

		/// <summary>
		/// Gets or sets the parameter ID of the table column to analyze.
		/// </summary>
		protected int ParameterId { get; set; }

		/// <summary>
		/// Gets or sets the subscription filter to apply when retrieving table indices.
		/// </summary>
		protected string SubscriptionFilter { get; set; }

		/// <summary>
		/// Gets the DataMiner DMS interface.
		/// </summary>
		protected GQIDMS Dms { get; private set; }

		/// <summary>
		/// Gets the logger instance.
		/// </summary>
		protected IGQILogger Logger { get; private set; }

		/// <summary>
		/// Gets the list of elements to process. 
		/// Must maintain order for pagination and support index-based access.
		/// </summary>
		protected abstract IReadOnlyList<IElementReference> Elements { get; }

		/// <summary>
		/// Gets the trend statistics configuration.
		/// Derived classes can override to customize configuration values.
		/// </summary>
		protected virtual TrendStatisticsConfig GetConfig()
		{
			return new TrendStatisticsConfig
			{
				MaxPageSize = 10,
				ParameterId = ParameterId,
				TrendDays = 30,
				HistogramIntervals = 100,
				SubscriptionFilter = SubscriptionFilter,
			};
		}

		/// <summary>
		/// Initializes the data source with the specified elements.
		/// </summary>
		/// <param name="args">The initialization arguments.</param>
		/// <returns>The initialization output arguments.</returns>
		public virtual OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			Dms = args.DMS;
			Logger = args.Logger;

			return new OnInitOutputArgs();
		}

		/// <summary>
		/// Gets the input arguments for the data source.
		/// Builds the complete argument list from derived-specific and common arguments.
		/// </summary>
		/// <returns>An array of GQI arguments.</returns>
		public GQIArgument[] GetInputArguments()
		{
			var innerArgs = GetInputArgumentsInner();
			var commonArgs = new GQIArgument[] { _pidArgument, _subscriptionFilterArgument };
			
			// Build argument array: derived-specific arguments followed by common arguments
			var result = new List<GQIArgument>(innerArgs.Length + commonArgs.Length);
			result.AddRange(innerArgs);
			result.AddRange(commonArgs);
			
			return result.ToArray();
		}

		/// <summary>
		/// Gets the data source-specific input arguments.
		/// Derived classes implement this to provide their unique arguments.
		/// </summary>
		/// <returns>An array of GQI arguments specific to the derived class.</returns>
		protected abstract GQIArgument[] GetInputArgumentsInner();

		/// <summary>
		/// Processes the input arguments provided by the user.
		/// Processes common arguments and delegates to derived class for specific arguments.
		/// </summary>
		/// <param name="args">The arguments processing input arguments.</param>
		/// <returns>The arguments processing output arguments.</returns>
		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			// Process common arguments
			ParameterId = args.GetArgumentValue<int>(_pidArgument);
			SubscriptionFilter = args.GetArgumentValue<string>(_subscriptionFilterArgument);

			// Let derived class process its specific arguments
			return OnArgumentsProcessedInner(args);
		}

		/// <summary>
		/// Processes the data source-specific input arguments.
		/// Derived classes implement this to handle their unique arguments.
		/// </summary>
		/// <param name="args">The arguments processing input arguments.</param>
		/// <returns>The arguments processing output arguments.</returns>
		protected abstract OnArgumentsProcessedOutputArgs OnArgumentsProcessedInner(OnArgumentsProcessedInputArgs args);

		/// <summary>
		/// Gets the output columns for the data source.
		/// </summary>
		/// <returns>An array of GQI columns.</returns>
		public virtual GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Element Key"),
				new GQIStringColumn("Table PK"),
				new GQIDoubleColumn("Average"),
			};
		}

		/// <summary>
		/// Prepares for data fetching by initializing the trend service.
		/// </summary>
		/// <param name="args">The prepare fetch input arguments.</param>
		/// <returns>The prepare fetch output arguments.</returns>
		public virtual OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			_config = GetConfig();
			_trendService = new TrendStatisticsService(Dms, Logger, _config);

			return OnPrepareFetchInner(args);
		}

		/// <summary>
		/// Inner preparation logic to be implemented by derived classes.
		/// </summary>
		/// <param name="args">The prepare fetch input arguments.</param>
		/// <returns>The prepare fetch output arguments.</returns>
		protected abstract OnPrepareFetchOutputArgs OnPrepareFetchInner(OnPrepareFetchInputArgs args);

		/// <summary>
		/// Gets the next page of data.
		/// </summary>
		/// <param name="args">The get next page input arguments.</param>
		/// <returns>The next page of data.</returns>
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

					var primaryKeys = _trendService.GetTablePrimaryKeys(element);
					if (primaryKeys == null || primaryKeys.Length == 0)
					{
						Logger.Warning($"No primary keys returned for element {element.Key}, skipping to next element.");
						_state.MoveToNextElement();
						continue;
					}

					// Process primary keys starting from the current position
					for (int i = _state.IndexPosition; i < primaryKeys.Length && rows.Count < _config.MaxPageSize; i++)
					{
						var primaryKey = primaryKeys[i];
						if (string.IsNullOrEmpty(primaryKey))
						{
							Logger.Warning($"Null or empty primary key at position {i} for element {element.Key}, skipping.");
							_state.IndexPosition++;
							continue;
						}

						var statistics = _trendService.GetTrendStatistics(element, primaryKey);
						if (statistics == null)
						{
							_state.IndexPosition++;
							continue;
						}

						Logger.Information($"Row count: {rows.Count}/{_config.MaxPageSize} - Element: {element.Key} - PK: {primaryKey} - Average: {statistics.Average}");

						var row = _trendService.CreateRow(element, primaryKey, statistics);
						if (row != null)
						{
							rows.Add(row);
						}

						_state.IndexPosition++;
					}

					// If we've processed all primary keys for this element, move to next element
					if (_state.IndexPosition >= primaryKeys.Length)
					{
						_state.MoveToNextElement();
					}
					else
					{
						// We stopped in the middle of this element due to page size limit
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

				// Return what we have so far and mark as no more pages to prevent infinite loops
				return new GQIPage(rows.ToArray())
				{
					HasNextPage = false,
				};
			}
		}
	}
}