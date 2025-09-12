namespace AggregatedTrendStatisticsDataSource
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Net.Trending;

	/// <summary>
	/// Represents a data source.
	/// See: https://aka.dataminer.services/gqi-external-data-source for a complete example.
	/// </summary>
	[GQIMetaData(Name = "Aggregated Trend Statistics")]
	public sealed class AggregatedTrendStatisticsDataSource : IGQIDataSource
		, IGQIOnPrepareFetch
		, IGQIOnInit
		, IGQIInputArguments
	{
		private readonly int _maxPageSize = 10; // Maximum rows per page

		private readonly GQIIntArgument _pidArgument = new GQIIntArgument("Column Parameter ID");
		private readonly GQIStringArgument _protocolArgument = new GQIStringArgument("Production Protocol Name");

		private GQIDMS _dms;
		private IGQILogger _logger;
		private string _protocolName;
		private int _pid;
		private int _elementIndex = 0;
		private int _indexPosition = 0; // Track position within current element's indices
		private List<LiteElementInfoEvent> _elements;
		private int _tablePID;
		private HistogramInterval[] _intervals;

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			_logger = args.Logger;

			return new OnInitOutputArgs();
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				_protocolArgument,
				_pidArgument,
			};
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_protocolName = args.GetArgumentValue<string>(_protocolArgument);
			_pid = args.GetArgumentValue<int>(_pidArgument);

			return new OnArgumentsProcessedOutputArgs();
		}

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("Element key"),
				new GQIStringColumn("Table PK"),
				new GQIDoubleColumn("Average"),
			};
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			var rows = new List<GQIRow>();

			try
			{
				while (rows.Count < _maxPageSize && _elementIndex < _elements.Count)
				{
					var element = _elements[_elementIndex];
					if (element == null)
					{
						_logger.Warning($"Element at index {_elementIndex} is null, skipping to next element.");
						_elementIndex++;
						_indexPosition = 0;
						continue;
					}

					DynamicTableIndicesResponse indicesResponse;
					try
					{
						indicesResponse = _dms.SendMessage(new GetDynamicTableIndices(element.DataMinerID, element.ElementID, _pid) { Filter = GetDynamicTableIndicesType.TrendedOnly }) as DynamicTableIndicesResponse;
					}
					catch (Exception ex)
					{
						_logger.Error($"Failed to get indices for element {element.DataMinerID}/{element.ElementID}: {ex.Message}");
						_elementIndex++;
						_indexPosition = 0;
						continue;
					}

					if (indicesResponse?.Indices == null)
					{
						_logger.Warning($"No indices response or null indices for element {element.DataMinerID}/{element.ElementID}, skipping to next element.");
						_elementIndex++;
						_indexPosition = 0;
						continue;
					}

					// Start from the current index position within this element
					for (int i = _indexPosition; i < indicesResponse.Indices.Length && rows.Count < _maxPageSize; i++)
					{
						var index = indicesResponse.Indices[i];
						if (index?.IndexValue == null)
						{
							_logger.Warning($"Null index at position {i} for element {element.DataMinerID}/{element.ElementID}, skipping.");
							_indexPosition = i + 1;
							continue;
						}

						GetHistogramTrendDataResponseMessage statistics;
						try
						{
							statistics = _dms.SendMessage(new GetHistogramTrendDataMessage(element.DataMinerID, element.ElementID, _pid, index.IndexValue)
							{
								StartTime = DateTime.Now.AddDays(-30),
								EndTime = DateTime.Now,
								RetrievalWithPrimaryKey = true,
								TrendingType = TrendingType.Auto,
								Intervals = _intervals,
							}) as GetHistogramTrendDataResponseMessage;
						}
						catch (Exception ex)
						{
							_logger.Warning($"Failed to get trend statistics for element {element.DataMinerID}/{element.ElementID}, index {index.IndexValue}: {ex.Message}");
							_indexPosition = i + 1;
							continue;
						}

						if (!(statistics?.TrendStatistics?.Values?.FirstOrDefault() is TrendStatistics stats))
						{
							// _logger.Information($"No statistics found for element {element.DataMinerID}/{element.ElementID} with table PK {index.IndexValue}.");
							_indexPosition = i + 1;
							continue;
						}

						_logger.Information($"Row count: {rows.Count}/{_maxPageSize} - " + _protocolName + " - " + element.DataMinerID + "/" + element.ElementID + " - " + index.IndexValue + " - Average: " + stats.Average);

						try
						{
							rows.Add(new GQIRow(new GQICell[]
							{
								new GQICell() { Value = $"{element.DataMinerID}/{element.ElementID}" },
								new GQICell() { Value = index.IndexValue },
								new GQICell() { Value = stats.Average },
							}));
						}
						catch (Exception ex)
						{
							_logger.Error($"Failed to create GQI row for element {element.DataMinerID}/{element.ElementID}, index {index.IndexValue}: {ex.Message}");
							_indexPosition = i + 1;
							continue;
						}

						_indexPosition = i + 1; // Update position within current element
					}

					// If we've processed all indices for this element, move to next element
					if (_indexPosition >= indicesResponse.Indices.Length)
					{
						_elementIndex++;
						_indexPosition = 0; // Reset index position for next element
					}
					else
					{
						// We stopped in the middle of this element due to page size limit
						break;
					}
				}

				// Determine if there are more pages
				bool hasNextPage = false;
				if (_elementIndex < _elements.Count)
				{
					if (_indexPosition > 0)
					{
						// We're in the middle of an element, so there are definitely more rows
						hasNextPage = true;
					}
					else
					{
						// Check if current element has indices or if there are more elements
						var element = _elements[_elementIndex];
						if (element != null)
						{
							try
							{
								var indicesResponse = _dms.SendMessage(new GetDynamicTableIndices(element.DataMinerID, element.ElementID, _pid) { Filter = GetDynamicTableIndicesType.TrendedOnly }) as DynamicTableIndicesResponse;
								hasNextPage = (indicesResponse?.Indices?.Length ?? 0) > 0 || (_elementIndex + 1) < _elements.Count;
							}
							catch (Exception ex)
							{
								_logger.Warning($"Failed to check indices for hasNextPage on element {element.DataMinerID}/{element.ElementID}: {ex.Message}");
								hasNextPage = (_elementIndex + 1) < _elements.Count;
							}
						}
						else
						{
							hasNextPage = (_elementIndex + 1) < _elements.Count;
						}
					}
				}

				_logger.Information($"Returning page with {rows.Count} rows. Has next page: {hasNextPage}");

				return new GQIPage(rows.ToArray())
				{
					HasNextPage = hasNextPage,
				};
			}
			catch (Exception ex)
			{
				_logger.Error($"Unexpected error in GetNextPage: {ex.Message}");
				_logger.Error($"Stack trace: {ex.StackTrace}");

				// Return what we have so far and mark as no more pages to prevent infinite loops
				return new GQIPage(rows.ToArray())
				{
					HasNextPage = false,
				};
			}
		}

		public OnPrepareFetchOutputArgs OnPrepareFetch(OnPrepareFetchInputArgs args)
		{
			var responseMsgs = _dms.SendMessages(GetLiteElementInfo.ByProtocol(_protocolName, "Production")) as DMSMessage[];
			if (responseMsgs == null || responseMsgs.Length == 0)
			{
				_logger.Information("No elements found for the specified protocol.");
				return new OnPrepareFetchOutputArgs();
			}

			_elements = responseMsgs.Select(x => x as LiteElementInfoEvent).ToList();
			_logger.Information($"Retrieved {_elements.Count} elements.");

			var protocol = _dms.SendMessage(new GetProtocolMessage(_protocolName, "Production")) as GetProtocolInfoResponseMessage;
			if (protocol == null)
			{
				_logger.Error($"Protocol '{_protocolName}' not found.");
				return new OnPrepareFetchOutputArgs();
			}

			var tableColumn = protocol.Parameters
										.FirstOrDefault(p => p.ID == _pid);
			if (tableColumn == null)
			{
				_logger.Error($"Parameter ID {_pid} not found in protocol '{_protocolName}'.");
				return new OnPrepareFetchOutputArgs();
			}

			if (!tableColumn.IsTableColumn)
			{
				_logger.Error($"Parameter ID {_pid} is not a table column in protocol '{_protocolName}'.");
				return new OnPrepareFetchOutputArgs();
			}

			_tablePID = tableColumn.ParentTablePid;

			List<HistogramInterval> intervalList = new List<HistogramInterval>();
			for (int i = 0; i < 100; i++)
			{
				intervalList.Add(new HistogramInterval(double.MinValue, double.MinValue));
			}

			_intervals = intervalList.ToArray();

			return new OnPrepareFetchOutputArgs();
		}
	}
}
