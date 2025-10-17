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
		private readonly GQIStringArgument _protocolArgument = new GQIStringArgument("Production Protocol Name") { IsRequired = true };

		private string _protocolName;
		private IReadOnlyList<IElementReference> _elements;

		protected override IReadOnlyList<IElementReference> Elements => _elements ?? EmptyElements;

		/// <summary>
		/// Gets the data source-specific input arguments.
		/// </summary>
		protected override GQIArgument[] GetInputArgumentsInner()
		{
			return new GQIArgument[]
			{
				_protocolArgument,
			};
		}

		/// <summary>
		/// Processes the data source-specific input arguments.
		/// </summary>
		protected override OnArgumentsProcessedOutputArgs OnArgumentsProcessedInner(OnArgumentsProcessedInputArgs args)
		{
			_protocolName = args.GetArgumentValue<string>(_protocolArgument);

			return new OnArgumentsProcessedOutputArgs();
		}

		/// <summary>
		 /// Inner preparation logic for protocol-based element discovery.
		/// </summary>
		protected override OnPrepareFetchOutputArgs OnPrepareFetchInner(OnPrepareFetchInputArgs args)
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
