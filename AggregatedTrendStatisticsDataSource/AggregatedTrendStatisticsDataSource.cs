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
		private readonly int _minPageSize = 50; // We keep adding new tables until we have at least 10 rows. We will then still add the rest of the table.

		private readonly GQIIntArgument _pidArgument = new GQIIntArgument("Column Parameter ID");
		private readonly GQIStringArgument _protocolArgument = new GQIStringArgument("Production Protocol Name");

		private GQIDMS _dms;
		private IGQILogger _logger;
		private string _protocolName;
		private int _pid;
		private int _elementIndex = 0;
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
			for (; rows.Count <= _minPageSize && _elementIndex < _elements.Count; _elementIndex++)
			{
				var element = _elements[_elementIndex];
				var indicesResponse = _dms.SendMessage(new GetDynamicTableIndices(element.DataMinerID, element.ElementID, _pid) { Filter = GetDynamicTableIndicesType.TrendedOnly }) as DynamicTableIndicesResponse;
				//_logger.Information($"Found {indicesResponse.Indices.Length} trended indices for element {element.DataMinerID}/{element.ElementID} with protocol {_protocolName} and Table ID {_tablePID}.");
				foreach (var index in indicesResponse.Indices)
				{
					var statistics = _dms.SendMessage(new GetHistogramTrendDataMessage(element.DataMinerID, element.ElementID, _pid, index.IndexValue)
					{
						StartTime = DateTime.Now.AddDays(-30),
						EndTime = DateTime.Now,
						RetrievalWithPrimaryKey = true,
						TrendingType = TrendingType.Auto,
						Intervals = _intervals,
					}) as GetHistogramTrendDataResponseMessage;

					if (!(statistics.TrendStatistics?.Values?.FirstOrDefault() is TrendStatistics stats))
					{
						//_logger.Information($"No statistics found for element {element.DataMinerID}/{element.ElementID} with table PK {index.IndexValue}.");
						continue;
					}

					_logger.Information($"Row count: {rows.Count}/{_minPageSize} - " + _protocolName + " - " + element.DataMinerID + "/" + element.ElementID + " - " + index.IndexValue + " - Average: " + stats.Average);
					rows.Add(new GQIRow(new GQICell[]
					{
						new GQICell() { Value = $"{element.DataMinerID}/{element.ElementID}" },
						new GQICell() { Value = index.IndexValue },
						new GQICell() { Value = stats.Average },
					}));
				}
			}

			return new GQIPage(rows.ToArray())
			{
				HasNextPage = _elementIndex + 1 < _elements.Count,
			};
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
