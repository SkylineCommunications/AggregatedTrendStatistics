# Aggregated Trend Statistics Data Sources

---

**Disclaimer**: These data sources perform intensive trend data operations. Always test in a development environment before deploying to production systems.

## About

This package provides **two complementary GQI (Generic Query Interface) ad hoc data sources** for DataMiner that enable you to retrieve and analyze trend statistics across elements. Choose the right data source based on your specific analysis needs:

### 1. Protocol-Based Trend Statistics
**Name**: `Aggregated Trend Statistics`

Automatically analyzes trend data across **all elements** of a specific protocol, generating rows with average trend values over a configurable time period for each table row of every element on the production version of a chosen protocol.

### 2. Element-Specific Trend Statistics  
**Name**: `Element-Specific Trend Statistics`

Analyzes trend data from **manually specified elements**, allowing you to target specific elements for analysis rather than processing entire protocols.

## Key Features

### Protocol-Based Data Source
- **Automatic Element Discovery**: Discovers all elements using the specified protocol
- **Protocol-Wide Analysis**: Comprehensive monitoring across all protocol elements
- **Production Version Targeting**: Focuses on production protocol versions

### Element-Specific Data Source
- **Targeted Analysis**: Specify exact elements to analyze using DMA ID/Element ID format
- **Flexible Input**: Supports comma or semicolon-separated element lists

### Common Features (Both Data Sources)
- **Table Column Parameter Support**: Analyze any table column parameter with trending enabled
- **30-Day Trend Analysis**: Retrieves average values over the last 30 days
- **Optimized Performance**: Includes pagination, request throttling, and error recovery
- **Comprehensive Logging**: Detailed logging for troubleshooting and monitoring

## Use Cases

### Protocol-Based Data Source
- **Performance Monitoring**: Monitor average performance metrics across all elements of a specific protocol
- **Comprehensive Reporting**: Create reports showing trend data aggregated by protocol
- **Automatic Discovery**: Handle new elements automatically as they're added
- **Protocol Health Monitoring**: Overall health assessment of protocol implementations

### Element-Specific Data Source
- **Critical Element Monitoring**: Focus on your most important elements
- **Troubleshooting**: Deep-dive analysis of specific problematic elements
- **Targeted Optimization**: Analyze specific element subsets for optimization

## Installation and Setup

### Requirements

- **DataMiner version**: 10.3.0.0-12752 or higher
- **Framework**: .NET Framework 4.8
- **Recommended**: DataMiner Aggregator DxM for scheduled queries (see Performance Considerations)

### Installation

Deploy this package to your DataMiner system to install both data sources. They will appear as separate options in the GQI data source selection.

## Usage

### Protocol-Based Data Source

**Input Parameters:**

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| **Production Protocol Name** | String | The exact name of the protocol as it appears in DataMiner | `"Microsoft Platform"` |
| **Column Parameter ID** | Integer | The parameter ID of the table column to analyze | `1004` |

**Output Columns:**

| Column | Type | Description |
|--------|------|-------------|
| **Element key** | String | Unique identifier for the element (DMA ID/Element ID) |
| **Table PK** | String | Primary key of the table row |
| **Average** | Double | Average trend value over the specified time period |

### Element-Specific Data Source

**Input Parameters:**

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| **Element Keys** | String | Comma or semicolon-separated list of element keys in format `DmaId/ElementId` | `"1/123,1/456,2/789"` |
| **Column Parameter ID** | Integer | The parameter ID of the table column to analyze | `1004` |

**Element Key Format:**
- Format: `DmaId/ElementId` (e.g., `"1/123"`)
- Multiple elements: `"1/123,1/456,2/789"`
- Separators: Use commas (`,`) or semicolons (`;`)
- Whitespace is automatically trimmed

**Output Columns:**

| Column | Type | Description |
|--------|------|-------------|
| **Element Key** | String | Element identifier in DMA ID/Element ID format |
| **Table PK** | String | Primary key of the table row |
| **Average** | Double | Average trend value over the specified time period |

## Performance Considerations

> **⚠️ Important Performance Notice**
> 
> Every row returned by these data sources requires retrieval of trend statistics and data, which can be resource-intensive operations. For optimal performance:

### Recommended Practices

- **Use DataMiner Aggregator DxM**: Configure scheduled queries to pre-calculate and cache results
- **Monitor System Load**: Be aware that setups with many elements may require significant processing time
- **Schedule During Off-Peak Hours**: Run aggregator jobs or other during periods of lower system activity
- **Choose the Right Data Source**: Use Element-Specific for targeted analysis to reduce processing load

> **Note**: Writing DataMiner Aggregator results to CSV? Use the [GQI Data Source from Data Aggregator CSV](https://catalog.dataminer.services/details/a0df7df3-1423-49c3-8c55-ae73154a535c).

### Optimization Features

Both data sources include several built-in optimizations:

- **Request Throttling**: Synchronously requests data from the server to spread the load over time
- **Pagination**: Processes data in manageable chunks (10 rows per page)
- **Error Recovery**: Continues processing even if individual element queries fail

## When to Use Which Data Source

### Choose Protocol-Based When:
- ✅ You need comprehensive data from all elements of a specific protocol
- ✅ You want automatic discovery of new elements
- ✅ You're doing protocol-wide performance monitoring
- ✅ You don't know specific element IDs or want full coverage

### Choose Element-Specific When:
- ✅ You need data from specific elements only
- ✅ You want to avoid processing all elements of a protocol
- ✅ You have a predefined list of critical elements to monitor
- ✅ You want faster processing for a subset of elements

## Technical Implementation

### Architecture
- **Type**: GQI Ad Hoc Data Sources
- **Interface**: `IGQIDataSource`, `IGQIOnPrepareFetch`, `IGQIOnInit`, `IGQIInputArguments`
- **Data Retrieval**: Uses DataMiner's `GetHistogramTrendDataMessage` message
- **Element Discovery**: Protocol-based uses `GetLiteElementInfo.ByProtocol`, Element-specific uses manual specification

### Supported Trend Types
- **Histogram Data**: Retrieves histogram-based trend statistics
- **Auto Trending**: Automatically selects the most appropriate trending type

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| No data returned | Protocol name doesn't match exactly or invalid element keys | Verify exact protocol name or element IDs in DataMiner Cube |
| Timeout errors | Large dataset or system load | Use DataMiner Aggregator DxM or reduce scope with Element-Specific |
| Missing elements | Elements not in production version or not accessible | Ensure elements are using production protocol version and are accessible |
| Parameter not found | Incorrect parameter ID | Verify the parameter ID exists and is a table column |

### Logging

Both data sources provide detailed logging for troubleshooting:

- **Information**: Row processing progress and successful operations
- **Warnings**: Timeout events, invalid inputs, and fallback operations
- **Errors**: Failed API calls and critical issues

Check the DataMiner logs for detailed error information when issues occur.

## Integration Examples

### DataMiner Aggregator Integration
Both data sources work excellently with DataMiner Aggregator DxM for:
- **Scheduled Processing**: Automate data collection during off-peak hours
- **Performance Optimization**: Pre-calculate and cache results
- **Historical Analysis**: Build time-series data for trend analysis

### Dashboard Integration
- **Real-time Dashboards**: Use Element-Specific for focused, fast-loading dashboards
- **Comprehensive Reports**: Use Protocol-Based for complete protocol health overviews
- **Comparative Analysis**: Combine both data sources for different dashboard panels

## Support and Contributing

### Documentation
- [DataMiner Documentation](https://docs.dataminer.services/)
- [GQI Data Sources Guide](https://docs.dataminer.services/user-guide/Advanced_Modules/Dashboards_and_Low_Code_Apps/GQI/Extensions/GQI_DataSource.html)

### Technical Support
For technical support and questions, please contact Skyline Communications support or visit the DataMiner Community.

