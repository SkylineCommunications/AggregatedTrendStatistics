# AggregatedTrendStatistics

## About

This data source lets you enter a single column parameter ID and a protocol name. It then generates a row with the average trend value over the last month for each table row of every element on the production version of the specified protocol.

> [!IMPORTANT]
> Every row in this data source requires trend statistics and data to be retrieved. We highly recommended configuring a scheduled query through the DataMiner Aggregator DxM.

## Key Features

- **Single Column Parameter ID Selection:** Select a column parameter ID.
- **Protocol Name Selection:** Specify the name of a protocol.
- **Average Trend Value Calculation:** Generates average trend values for the last month.
- **Comprehensive Data Coverage:** Applies to every table row of every element on the production version of the specified protocol.

## Prerequisites

- **DataMiner Aggregator DxM:** Ensure the DxM is configured for scheduled queries.

## Technical Reference

> [!CAUTION]
> If you want to make adjustments to the code of the data source, proceed with caution as it directly executes heavy messages on your system.
