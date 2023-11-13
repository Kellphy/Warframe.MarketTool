namespace Kellphy.Warframe.MarketTool
{
	public static class Extensions
	{
		public static void WriteWarningMessage(this string message)
		{
			message.WriteColoredMessage(ConsoleColor.Yellow);
		}

		public static void WriteGoodMessage(this string message)
		{
			message.WriteColoredMessage(ConsoleColor.Green);
		}

		public static void WriteErrorMessage(this string message)
		{
			message.WriteColoredMessage(ConsoleColor.Red);
		}

		private static void WriteColoredMessage(this string message, ConsoleColor consoleColor)
		{
			Console.ForegroundColor = consoleColor;
			Console.WriteLine(message);
			Console.ForegroundColor = ConsoleColor.Gray;
		}
	}
}
