namespace AggregatedTrendStatisticsDataSource
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;
	using TrendStatisticsShared.DataSources;
	using TrendStatisticsShared.Models;

	/// <summary>
	/// Represents a data source for retrieving trend statistics for all elements of a specific protocol.
	/// </summary>
	[GQIMetaData(Name = "Aggregated Trend Statistics")]
	public sealed class AggregatedTrendStatisticsDataSource : BaseTrendStatisticsDataSource
	{
		private readonly GQIIntArgument _pidArgument = new GQIIntArgument("Column Parameter ID");
		private readonly GQIStringArgument _protocolArgument = new GQIStringArgument("Production Protocol Name");

		private string _protocolName;
		private IReadOnlyList<IElementReference> _elements;

		/// <summary>
		/// Gets the list of elements to process.
		/// </summary>
		protected override IReadOnlyList<IElementReference> Elements => _elements ?? EmptyElements;

		/// <summary>
		/// Gets the input arguments for the data source.
		/// </summary>
		public override GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				_protocolArgument,
				_pidArgument,
			};
		}

		/// <summary>
		/// Processes the input arguments provided by the user.
		/// </summary>
		public override OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_protocolName = args.GetArgumentValue<string>(_protocolArgument);
			ParameterId = args.GetArgumentValue<int>(_pidArgument);

			return new OnArgumentsProcessedOutputArgs();
		}

		/// <summary>
		/// Core preparation logic for protocol-based element discovery.
		/// </summary>
		protected override OnPrepareFetchOutputArgs OnPrepareFetchCore(OnPrepareFetchInputArgs args)
		{
			var responseMsgs = Dms.SendMessages(GetLiteElementInfo.ByProtocol(_protocolName, "Production")) as DMSMessage[];
			if (responseMsgs == null || responseMsgs.Length == 0)
			{
				Logger.Information("No elements found for the specified protocol.");
				_elements = EmptyElements;
				return new OnPrepareFetchOutputArgs();
			}

			var elementInfos = responseMsgs.Select(x => x as LiteElementInfoEvent).Where(x => x != null).ToList();
			Logger.Information($"Retrieved {elementInfos.Count} elements from protocol.");

			// Validate protocol and parameter
			var protocol = Dms.SendMessage(new GetProtocolMessage(_protocolName, "Production")) as GetProtocolInfoResponseMessage;
			if (protocol == null)
			{
				Logger.Error($"Protocol '{_protocolName}' not found.");
				_elements = EmptyElements;
				return new OnPrepareFetchOutputArgs();
			}

			var tableColumn = protocol.Parameters.FirstOrDefault(p => p.ID == ParameterId);
			if (tableColumn == null)
			{
				Logger.Error($"Parameter ID {ParameterId} not found in protocol '{_protocolName}'.");
				_elements = EmptyElements;
				return new OnPrepareFetchOutputArgs();
			}

			if (!tableColumn.IsTableColumn)
			{
				Logger.Error($"Parameter ID {ParameterId} is not a table column in protocol '{_protocolName}'.");
				_elements = EmptyElements;
				return new OnPrepareFetchOutputArgs();
			}

			_elements = elementInfos.Select(e => new ProtocolElementReference(e) as IElementReference).ToList().AsReadOnly();

			Logger.Information($"Processing {_elements.Count} elements.");

			return new OnPrepareFetchOutputArgs();
		}

		/// <summary>
		/// Implementation of IElementReference for protocol-discovered elements.
		/// </summary>
		private class ProtocolElementReference : IElementReference
		{
			private readonly LiteElementInfoEvent _elementInfo;

			public ProtocolElementReference(LiteElementInfoEvent elementInfo)
			{
				_elementInfo = elementInfo ?? throw new ArgumentNullException(nameof(elementInfo));
			}

			public int DataMinerID => _elementInfo.DataMinerID;

			public int ElementID => _elementInfo.ElementID;

			public string Key => $"{_elementInfo.DataMinerID}/{_elementInfo.ElementID}";
		}
	}
}
