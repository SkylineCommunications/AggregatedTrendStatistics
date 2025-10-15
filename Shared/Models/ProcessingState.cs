namespace TrendStatisticsShared.Models
{
	using System.Collections.Generic;

	public class ProcessingState
	{
		public int ElementIndex { get; set; } = 0;

		public int IndexPosition { get; set; } = 0;

		public void MoveToNextElement()
		{
			ElementIndex++;
			IndexPosition = 0;
		}
	}
}