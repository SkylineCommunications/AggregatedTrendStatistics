# Aggregated Trend Statistics Data Source

---

**Disclaimer**: This data source performs intensive trend data operations. Always test in a development environment before deploying to production systems.

## About

The **Aggregated Trend Statistics Data Source** is a GQI (Generic Query Interface) ad hoc data source for DataMiner that enables you to retrieve and analyze trend statistics across multiple elements of a specific protocol. This data source generates rows containing average trend values over a configurable time period for each table row of every element on the production version of a chosen protocol.

## Key Features

- **Protocol-Based Filtering**: Select any production protocol to analyze trend data across all its elements
- **Column Parameter Selection**: Choose a specific table column parameter to retrieve trend statistics for
- **Automatic Element Discovery**: Automatically discovers all elements using the specified protocol
- **Optimized Performance**: Includes timeout handling, progressive fallback, and pagination for reliable operation
- **Real-time Data**: Retrieves the most recent trend data for comprehensive analysis

## Use Cases

- **Performance Monitoring**: Monitor average performance metrics across all elements of a specific protocol
- **Reporting and Dashboards**: Create comprehensive reports showing trend data aggregated by protocol
- **Troubleshooting**: Quickly identify elements with abnormal trend patterns

## Installation and Setup

### Requirements

- **DataMiner version**: 10.3.0.0-12752 or higher
- **Framework**: .NET Framework 4.8
- **Recommended**: DataMiner Aggregator DxM for scheduled queries (see Performance Considerations)

## Usage

### Input Parameters

The data source requires two input parameters:

| Parameter | Type | Description | Example |
|-----------|------|-------------|---------|
| **Production Protocol Name** | String | The exact name of the protocol as it appears in DataMiner | `"Microsoft Platform"` |
| **Column Parameter ID** | Integer | The parameter ID of the table column to analyze | `1004` |

### Output Columns

The data source returns the following columns:

| Column | Type | Description |
|--------|------|-------------|
| **Element key** | String | Unique identifier for the element (DMA ID/Element ID) |
| **Table PK** | String | Primary key of the table row |
| **Average** | Double | Average trend value over the specified time period |

## Performance Considerations

> **⚠️ Important Performance Notice**
> 
> Every row returned by this data source requires retrieval of trend statistics and data, which can be resource-intensive operations. For optimal performance:

### Recommended Practices

- **Use DataMiner Aggregator DxM**: Configure scheduled queries to pre-calculate and cache results
- **Monitor System Load**: Be aware that setups with many elements may require significant processing time
- **Schedule During Off-Peak Hours**: Run aggregator jobs or other during periods of lower system activity

### Optimization Features

The data source includes several built-in optimizations:

- **Request Throttling**: Synchronously requests data from the server to spread the load over time
- **Pagination**: Processes data in manageable chunks (10 rows per page)
- **Error Recovery**: Continues processing even if individual element queries fail

## Technical Implementation

### Architecture

- **Type**: GQI Ad Hoc Data Source
- **Interface**: `IGQIDataSource`, `IGQIOnPrepareFetch`, `IGQIOnInit`, `IGQIInputArguments`
- **Data Retrieval**: Uses DataMiner's `GetHistogramTrendDataMessage` message
- **Element Discovery**: Leverages `GetLiteElementInfo.ByProtocol` for automatic element detection

### Supported Trend Types

- **Histogram Data**: Retrieves histogram-based trend statistics
- **Auto Trending**: Automatically selects the most appropriate trending type

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| No data returned | Protocol name doesn't match exactly | Verify the exact protocol name in DataMiner Cube |
| Timeout errors | Large dataset or system load | Use DataMiner Aggregator DxM or schedule queries during off-peak hours |
| Missing elements | Elements not in production version | Ensure elements are using the production protocol version |
| Parameter not found | Incorrect parameter ID | Verify the parameter ID exists and is a table column |

### Logging

The data source provides detailed logging for troubleshooting:

- **Information**: Row processing progress and successful operations
- **Warnings**: Timeout events and fallback operations
- **Errors**: Failed API calls and critical issues

Check the DataMiner logs for detailed error information when issues occur.

## Support and Contributing

### Documentation
- [DataMiner Documentation](https://docs.dataminer.services/)
- [GQI Data Sources Guide](https://docs.dataminer.services/user-guide/Advanced_Modules/Dashboards_and_Low_Code_Apps/GQI/Extensions/GQI_DataSource.html)

### Technical Support
For technical support and questions, please contact Skyline Communications support or visit the DataMiner Community.

