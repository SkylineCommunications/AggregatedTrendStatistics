namespace AggregatedTrendStatisticsByElementDataSource
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using TrendStatisticsShared.DataSources;
	using TrendStatisticsShared.Models;

	/// <summary>
	/// Represents a data source for specific elements.
	/// Allows users to configure one or more specific elements instead of all elements from a protocol.
	/// </summary>
	[GQIMetaData(Name = "Element-Specific Trend Statistics")]
	public sealed class AggregatedTrendStatisticsByElementDataSource : BaseTrendStatisticsDataSource
	{
		private readonly GQIIntArgument _pidArgument = new GQIIntArgument("Column Parameter ID") { IsRequired = true };
		private readonly GQIStringArgument _elementsArgument = new GQIStringArgument("Element Keys (',' or ';' separated)")
		{
			IsRequired = true,
			DefaultValue = "1/1,1/2",
		};

		private IReadOnlyList<IElementReference> _elements;

		protected override IReadOnlyList<IElementReference> Elements => _elements ?? EmptyElements;

		public override GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[]
			{
				_elementsArgument,
				_pidArgument,
			};
		}

		public override OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			var elementsString = args.GetArgumentValue<string>(_elementsArgument);
			ParameterId = args.GetArgumentValue<int>(_pidArgument);

			var elementsList = new List<CustomElementReference>();

			if (!string.IsNullOrWhiteSpace(elementsString))
			{
				var elementKeys = elementsString.Split(',', ';').Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x));

				foreach (var elementKey in elementKeys)
				{
					if (TryParseElementKey(elementKey, out int dmaId, out int elementId))
					{
						elementsList.Add(new CustomElementReference(dmaId, elementId, elementKey));
					}
					else
					{
						Logger.Warning($"Invalid element key format: '{elementKey}'. Expected format: 'DmaId/ElementId' (e.g., '1/123')");
					}
				}
			}

			_elements = elementsList.Cast<IElementReference>().ToList().AsReadOnly();

			Logger.Information($"Parsed {_elements.Count} valid element keys from input.");

			return new OnArgumentsProcessedOutputArgs();
		}

		protected override OnPrepareFetchOutputArgs OnPrepareFetchCore(OnPrepareFetchInputArgs args)
		{
			if (_elements == null || _elements.Count == 0)
			{
				Logger.Information("No valid elements configured.");
				return new OnPrepareFetchOutputArgs();
			}

			Logger.Information($"Preparing to fetch data for {_elements.Count} configured elements.");

			return new OnPrepareFetchOutputArgs();
		}

		private bool TryParseElementKey(string elementKey, out int dmaId, out int elementId)
		{
			dmaId = 0;
			elementId = 0;

			if (string.IsNullOrWhiteSpace(elementKey))
				return false;

			var parts = elementKey.Split('/');
			if (parts.Length != 2)
				return false;

			return int.TryParse(parts[0], out dmaId) && int.TryParse(parts[1], out elementId);
		}

		private class CustomElementReference : IElementReference
		{
			public CustomElementReference(int dataMinerID, int elementID, string key)
			{
				DataMinerID = dataMinerID;
				ElementID = elementID;
				Key = key;
			}

			public int DataMinerID { get; }

			public int ElementID { get; }

			public string Key { get; }
		}
	}
}