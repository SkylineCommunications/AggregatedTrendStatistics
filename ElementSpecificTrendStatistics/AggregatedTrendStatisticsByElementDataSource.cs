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
		private readonly GQIStringArgument _elementsArgument = new GQIStringArgument("Element Keys (',' or ';' separated)")
		{
			IsRequired = true,
			DefaultValue = "1/1,1/2",
		};

		private IReadOnlyList<IElementReference> _elements;

		/// <summary>
		/// Gets the list of elements to process.
		/// </summary>
		protected override IReadOnlyList<IElementReference> Elements => _elements ?? EmptyElements;

		/// <summary>
		/// Gets the data source-specific input arguments.
		/// </summary>
		protected override GQIArgument[] GetInputArgumentsInner()
		{
			return new GQIArgument[]
			{
				_elementsArgument,
			};
		}

		/// <summary>
		/// Processes the data source-specific input arguments.
		/// </summary>
		protected override OnArgumentsProcessedOutputArgs OnArgumentsProcessedInner(OnArgumentsProcessedInputArgs args)
		{
			var elementsString = args.GetArgumentValue<string>(_elementsArgument);

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

		/// <summary>
		/// Inner preparation logic for element-specific data sources.
		/// </summary>
		protected override OnPrepareFetchOutputArgs OnPrepareFetchInner(OnPrepareFetchInputArgs args)
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

		/// <summary>
		/// Implementation of IElementReference for user-specified elements.
		/// </summary>
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