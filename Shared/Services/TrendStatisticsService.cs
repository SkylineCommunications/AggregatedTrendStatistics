namespace TrendStatisticsShared.Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net;
	using Skyline.DataMiner.Net.Exceptions;
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
		/// Uses SessionTableMessage for pagination to retrieve all matching rows across multiple pages.
		/// </summary>
		/// <param name="element">The element reference.</param>
		/// <returns>Array of primary keys, or empty array if failed.</returns>
		public string[] GetTablePrimaryKeys(IElementReference element)
		{
			int sessionId = -1;
			var elementId = new ElementID(element.DataMinerID, element.ElementID);

			try
			{
				// Build filters array: always include trend filter, plus optional user filter
				var filters = BuildFiltersArray();

				if (filters != null && filters.Length > 0)
				{
					_logger.Information($"Requesting table with filters: {string.Join("; ", filters)}");
				}

				var allPrimaryKeys = new List<string>();
				uint currentPage = 1;
				bool hasMorePages = true;

				// Loop through all pages
				while (hasMorePages)
				{
					SessionTableMessage pageRequest;

					if (sessionId < 0)
					{
						// First request: Start the session with filters
						// Note: First page is page 1, not page 0
						pageRequest = new SessionTableMessage(
							elementId,
							_config.ParameterId,
							filters);
					}
					else
					{
						// Subsequent requests: Use session ID and page number
						pageRequest = new SessionTableMessage(
							elementId,
							sessionId,
							currentPage);
					}

					var pageResponse = _dms.SendMessage(pageRequest) as ParameterChangeEventMessage;

					if (pageResponse?.NewValue?.ArrayValue == null)
					{
						if (currentPage == 1)
						{
							_logger.Warning($"No table data returned for element {element.Key}, parameter {_config.ParameterId}");
						}
						else
						{
							_logger.Information($"No more data returned for page {currentPage} of session {sessionId} - end of pages reached");
						}

						break;
					}

					// Extract session ID from first response (DataCookie)
					if (sessionId < 0)
					{
						sessionId = pageResponse.DataCookie;
						_logger.Information($"Started session {sessionId} for element {element.Key}");
					}

					// Parse primary keys from this page
					var pageKeys = ParseTablePrimaryKeys(pageResponse, element, (int)currentPage);

					if (pageKeys.Length == 0)
					{
						_logger.Information($"Page {currentPage}: No keys returned - end of pages reached");
						hasMorePages = false;
					}
					else
					{
						allPrimaryKeys.AddRange(pageKeys);
						_logger.Information($"Page {currentPage}: Retrieved {pageKeys.Length} primary keys");

						// Check if there are more pages using PartialDataInfo
						// PartialDataInfo.Pages contains the total number of pages
						var totalPages = pageResponse.PartialDataInfo?.Pages?.Length ?? 0;

						if (totalPages > 0 && currentPage >= totalPages)
						{
							_logger.Information($"Page {currentPage}: Reached last page ({totalPages} total pages)");
							hasMorePages = false;

							// Immediately close the session as there are no more pages
							CloseSession(elementId, sessionId);
							sessionId = -1; // Prevent closing again in finally block
						}
						else
						{
							// Move to next page
							currentPage++;
						}
					}
				}

				_logger.Information($"Retrieved {allPrimaryKeys.Count} total trended primary keys from element {element.Key} across {currentPage} page(s)");
				return allPrimaryKeys.ToArray();
			}
			catch (DataMinerCOMException ex)
			{
				_logger.Error($"COM exception while getting table primary keys for element {element.Key}: {ex.Message} (HR: 0x{ex.ErrorCode:X})");
				return Array.Empty<string>();
			}
			catch (Exception ex)
			{
				_logger.Error($"Failed to get table primary keys for element {element.Key}: {ex.Message}");
				return Array.Empty<string>();
			}
			finally
			{
				// Close the session if still open
				if (sessionId >= 0)
				{
					CloseSession(elementId, sessionId);
				}
			}
		}

		private void CloseSession(ElementID element, int sessionId)
		{
			try
			{
				var closeSessionRequest = new SessionTableMessage(element, sessionId)
				{
					CloseSession = true,
				};

				_dms.SendMessage(closeSessionRequest);
				_logger.Information($"Closed session {sessionId} for element {element.ToString()}");
			}
			catch (Exception ex)
			{
				_logger.Warning($"Failed to close session {sessionId}: {ex.Message}");
			}
		}

		private string[] BuildFiltersArray()
		{
			var filtersList = new List<string>();

			// Always add trend filter to get only rows with trending enabled
			// Format: trend=avg,{parameterId}|rt,{parameterId} (both average and real-time)
			filtersList.Add($"trend=avg,{_config.ParameterId}|rt,{_config.ParameterId}");

			// Add user-provided subscription filter if specified
			if (!string.IsNullOrWhiteSpace(_config.SubscriptionFilter))
			{
				filtersList.Add(_config.SubscriptionFilter);
			}

			return filtersList.ToArray();
		}

		private string[] ParseTablePrimaryKeys(ParameterChangeEventMessage tableMessage, IElementReference element, int pageNumber)
		{
			// Table structure: ArrayValue contains columns (not rows)
			// By DataMiner convention, columns[0] is the primary key column
			var columns = tableMessage.NewValue.ArrayValue;

			if (columns == null || columns.Length == 0)
			{
				_logger.Warning($"Table page {pageNumber} has no columns for element {element.Key}");
				return Array.Empty<string>();
			}

			// Extract primary keys from the first column (standard PK column position)
			var primaryKeyColumn = columns[0];

			if (primaryKeyColumn?.ArrayValue == null || primaryKeyColumn.ArrayValue.Length == 0)
			{
				_logger.Warning($"Primary key column is empty on page {pageNumber} for element {element.Key}");
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

			_logger.Information($"Page {pageNumber}: Parsed {primaryKeys.Count} primary keys (total rows in page: {primaryKeyColumn.ArrayValue.Length})");
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