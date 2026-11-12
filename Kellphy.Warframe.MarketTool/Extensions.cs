using System.Diagnostics;

namespace Kellphy.Warframe.MarketTool
{
	public static class Extensions
	{
		private static DateTime _lastRequestDate;
		private static int _msDelay = 10;

		public static async Task<HttpResponseMessage> GetAsyncWaited(this HttpClient client, string requestUri, TimeSpan waitTime)
		{
			var retryCounter = 10;
			HttpResponseMessage? response;

			do
			{
				await Wait(waitTime);
				Stopwatch stopwatch = Stopwatch.StartNew();
				response = await client.GetAsync(requestUri);
				stopwatch.Stop();
				$"[Responsed in {stopwatch.Elapsed.TotalMilliseconds}ms] {response.StatusCode}".WriteDebugMessage();
				retryCounter--;
				if (response.IsSuccessStatusCode)
				{
					return response;
				}

				var statusCode = response.StatusCode;
				response.Dispose();
				if (retryCounter == 0 || (statusCode != System.Net.HttpStatusCode.RequestTimeout &&
					statusCode != System.Net.HttpStatusCode.TooManyRequests && (int)statusCode < 500))
				{
					throw new HttpRequestException(
						$"GET {requestUri} failed: HTTP {(int)statusCode} ({statusCode}).", null, statusCode);
				}
			}
			while (true);
		}

		public static async Task<HttpResponseMessage> SendAsyncWaited(this HttpClient client, HttpRequestMessage request, TimeSpan waitTime)
		{
			// An HttpRequestMessage can only be sent once, so retries send a copy.
			var body = request.Content == null ? null : await request.Content.ReadAsByteArrayAsync();
			var retryCounter = 10;
			var attempt = request;

			while (true)
			{
				await Wait(waitTime);
				Stopwatch stopwatch = Stopwatch.StartNew();
				var response = await client.SendAsync(attempt);
				stopwatch.Stop();
				$"[Responsed in {stopwatch.Elapsed.TotalMilliseconds}ms] {response.StatusCode}".WriteDebugMessage();
				retryCounter--;

				var statusCode = response.StatusCode;
				var isTransient = statusCode == System.Net.HttpStatusCode.RequestTimeout ||
					statusCode == System.Net.HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
				if (response.IsSuccessStatusCode || !isTransient || retryCounter == 0)
				{
					return response;
				}

				response.Dispose();
				attempt = CloneRequest(request, body);
			}
		}

		private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[]? body)
		{
			var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
			foreach (var header in request.Headers)
			{
				clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			if (body != null)
			{
				clone.Content = new ByteArrayContent(body);
				foreach (var header in request.Content!.Headers)
				{
					clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}
			}

			return clone;
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
