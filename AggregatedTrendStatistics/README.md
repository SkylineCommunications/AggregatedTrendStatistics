# Technical Documentation for AggregatedTrendStatistics

This data source allows you to choose a single column parameter ID along with a protocol name. From there, the data source will generate a row with the average trend value over the last month for every table row of every element on the production version of the protocol chosen.

Given that every single row in this data source requires retrieval of trend statistics and thus trend data, it is strongly recommended to configure a scheduled query through the DataMiner Aggregator DxM.