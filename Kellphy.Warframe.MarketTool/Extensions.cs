using System.Diagnostics;

namespace Kellphy.Warframe.MarketTool
{
	public static class Extensions
	{
		private static DateTime _lastRequestDate;
		private static int _msDelay = 10;

		public static async Task<HttpResponseMessage> GetAsyncWaited(this HttpClient client, string requestUri, TimeSpan waitTime)
		{
			await Wait(waitTime);

			var retryCounter = 10;
			HttpResponseMessage? response;

			do
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				response = await client.GetAsync(requestUri);
				stopwatch.Stop();
				$"[Responsed in {stopwatch.Elapsed.TotalMilliseconds}ms] {response.StatusCode}".WriteDebugMessage();
				retryCounter--;
			}
			while (retryCounter > 0 && !response.IsSuccessStatusCode);

			return response;
		}

		public static async Task<HttpResponseMessage> SendAsyncWaited(this HttpClient client, HttpRequestMessage request, TimeSpan waitTime)
		{
			await Wait(waitTime);

			var retryCounter = 10;
			HttpResponseMessage? response;

			do
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				response = await client.SendAsync(request);
				stopwatch.Stop();
				$"[Responsed in {stopwatch.Elapsed.TotalMilliseconds}ms] {response.StatusCode}".WriteDebugMessage();
				retryCounter--;
			}
			while (retryCounter > 0 && !response.IsSuccessStatusCode);

			return response;
		}

		private static async Task Wait(TimeSpan waitTime)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			while (DateTime.Now - _lastRequestDate < waitTime)
			{
				await Task.Delay(_msDelay);
			}
			stopwatch.Stop();

			if (stopwatch.Elapsed >= TimeSpan.FromMilliseconds(_msDelay))
			{
				$"[Delayed for {stopwatch.Elapsed.TotalMilliseconds}ms]".WriteDebugMessage();
			}

			_lastRequestDate = DateTime.Now;
		}

		public static void WriteInfoMessage(this string message)
		{
			$"# {message}".WriteColoredMessage(ConsoleColor.Blue);
		}

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

		public static void WriteDebugMessage(this string message)
		{
			message.WriteColoredMessage(ConsoleColor.DarkGray);
		}

		private static void WriteColoredMessage(this string message, ConsoleColor consoleColor)
		{
			Console.ForegroundColor = consoleColor;
			Console.WriteLine(message);
			Console.ForegroundColor = ConsoleColor.Gray;
		}
	}
}
