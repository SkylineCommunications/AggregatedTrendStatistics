namespace TrendStatisticsShared.Models
{
	/// <summary>
	/// Represents an element for trend statistics processing.
	/// </summary>
	public interface IElementReference
	{
		/// <summary>
		/// Gets the DataMiner Agent ID.
		/// </summary>
		int DataMinerID { get; }

		/// <summary>
		/// Gets the Element ID.
		/// </summary>
		int ElementID { get; }

		/// <summary>
		/// Gets a string representation of the element for logging and display.
		/// </summary>
		string Key { get; }
	}
}