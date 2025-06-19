### AggregatedTrendStatistics

**Disclaimer:** Every row in this data source requires retrieval of trend statistics and data. It is highly recommended to configure a scheduled query through the DataMiner Aggregator DxM.

#### About
This data source enables you to enter a single column parameter ID and a protocol name. It generates a row with the average trend value over the last month for each table row of every element on the production version of the chosen protocol.

#### Key Features
- **Single Column Parameter ID Selection:** Choose a specific column parameter ID.
- **Protocol Name Selection:** Specify the protocol name.
- **Average Trend Value Calculation:** Generates average trend values for the last month.
- **Comprehensive Data Coverage:** Applies to every table row of every element on the production version of the chosen protocol.

#### Prerequisites
- **DataMiner Aggregator DxM:** Ensure it is configured for scheduled queries.

#### Technical Reference
If you want to make adjustments to the data source code, proceed with caution as it directly executes heavy messages on your system.