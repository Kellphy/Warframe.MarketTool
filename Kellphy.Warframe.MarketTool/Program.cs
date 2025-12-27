using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using SharpCompress.Compressors.LZMA;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Kellphy.Warframe.MarketTool
{
	internal class Program
	{
		private static TimeSpan _minApiTime = TimeSpan.FromMilliseconds(350);
		private static TimeSpan _maxApiTime = TimeSpan.FromSeconds(1);
#pragma warning disable CS8618 // Added in custom constructor
		private static HttpClient _client;
		private static string _jwt; // JWT is the security key, store this as email+pw combo
		private static string _email;
		private static string _password;
#pragma warning restore CS8618

		private enum Syndicate
		{
			NewLoka,
			PerrinSequence
		};

		private static Dictionary<Syndicate, string> _syndicates = new()
		{
			{ Syndicate.NewLoka, "New Loka" },
			{ Syndicate.PerrinSequence, "Perrin Sequence" }
		};
		private static int _platinum = 25;

		private Program()
		{
			HttpClientHandler handler = new HttpClientHandler()
			{
				UseCookies = false
			};
			_client = new HttpClient(handler);
			_jwt = string.Empty;

			var configuration = new ConfigurationBuilder()
				.AddUserSecrets<Program>()
				.Build();

			_email = configuration["email"];
			_password = configuration["password"];
		}

		private static async Task Main(string[] args)
		{
			var program = new Program();
			await program.Invoke();
		}

		public async Task Invoke()
		{
			while (true)
			{
				await Logic();

				"Completed! Press any key to restart.".WriteInfoMessage();
				Console.ReadKey();
				Console.Clear();
			}
		}

		private static async Task Logic()
		{
			if (!File.Exists("orders.txt"))
			{
				File.Create("orders.txt");
			}

			var response = await _client.GetAsyncWaited("https://api.warframe.market/v2/items", _minApiTime);
			var responseContent = await response.Content.ReadAsStringAsync();
			var responseJson = JsonConvert.DeserializeObject<ApiResponse<List<WfmItem>>>(responseContent);

			if (responseJson is null)
			{
				"Null Response".WriteErrorMessage();
				return;
			}

			if (responseJson.HasError)
			{
				$"API Error: {responseJson.GetErrorMessage()}".WriteErrorMessage();
				return;
			}

			await Login(_email, _password);

			var modList = await GetModList();

			Console.WriteLine(
				$"[1] {_syndicates[Syndicate.NewLoka]} > {_syndicates[Syndicate.PerrinSequence]}" +
				$"\n[2] {_syndicates[Syndicate.PerrinSequence]} > {_syndicates[Syndicate.NewLoka]}" +
				$"\n[3] {_syndicates[Syndicate.NewLoka]}" +
				$"\n[4] {_syndicates[Syndicate.PerrinSequence]}" +
				$"\n[5] Custom Orders" +
				"\n[.] Clear");
			Console.Write("Choice: ");
			var choice = Console.ReadKey();
			Console.WriteLine();

			switch (choice.Key)
			{
				case ConsoleKey.D1:
				case ConsoleKey.D2:
				case ConsoleKey.D3:
				case ConsoleKey.D4:
				case ConsoleKey.D5:
					Console.Write("Platinum price: ");
					if (int.TryParse(Console.ReadLine(), out int newPlatinum))
					{
						_platinum = newPlatinum;
					}
					$"Using price: {_platinum}".WriteInfoMessage();
					break;
			}

			await DeleteOrders();

			var orderIds = new List<string>();
			switch (choice.Key)
			{
				case ConsoleKey.D1:
					orderIds.AddRange(await AddItems(responseJson, modList[_syndicates[Syndicate.NewLoka]]));
					orderIds.AddRange(await AddItems(responseJson, modList[_syndicates[Syndicate.PerrinSequence]]));
					break;
				case ConsoleKey.D2:
					orderIds.AddRange(await AddItems(responseJson, modList[_syndicates[Syndicate.PerrinSequence]]));
					orderIds.AddRange(await AddItems(responseJson, modList[_syndicates[Syndicate.NewLoka]]));
					break;
				case ConsoleKey.D3:
					orderIds.AddRange(await AddItems(responseJson, modList[_syndicates[Syndicate.NewLoka]]));
					break;
				case ConsoleKey.D4:
					orderIds.AddRange(await AddItems(responseJson, modList[_syndicates[Syndicate.PerrinSequence]]));
					break;
				case ConsoleKey.D5:
					var customOrdersFile = "custom-orders.txt";
					if (!File.Exists(customOrdersFile))
					{
						File.Create(customOrdersFile).Close();
					}
					orderIds.AddRange(await AddItems(responseJson, File.ReadAllLines(customOrdersFile)));
					break;
			}

			$"Added Orders: {orderIds.Count}".WriteGoodMessage();
		}

		private static async Task<Dictionary<string, string[]>> GetModList()
		{
			Dictionary<string, string[]> syndicates = new();

			try
			{
				// Try api.warframestat.us first
				var response = await _client.GetStringAsync("https://api.warframestat.us/mods");
				var allMods = JsonConvert.DeserializeObject<List<WfStatMod>>(response) ?? new();

				var newLokaList = new List<string>();
				var perrinSeqList = new List<string>();

				foreach (var mod in allMods)
				{
					if (mod.drops?.Any(d => d.location?.Contains(_syndicates[Syndicate.NewLoka], StringComparison.CurrentCultureIgnoreCase) == true) == true)
					{
						if (mod.name != null && !mod.name.Equals("Sacrifice", StringComparison.OrdinalIgnoreCase))
						{
							newLokaList.Add(mod.name);
						}
					}
					if (mod.drops?.Any(d => d.location?.Contains(_syndicates[Syndicate.PerrinSequence], StringComparison.CurrentCultureIgnoreCase) == true) == true)
					{
						if (mod.name != null)
						{
							perrinSeqList.Add(mod.name);
						}
					}
				}

				if (newLokaList.Count > 0 || perrinSeqList.Count > 0)
				{
					syndicates.Add(_syndicates[Syndicate.NewLoka], newLokaList.ToArray());
					$"Found New Loka: {newLokaList.Count} (warframestat.us)".WriteGoodMessage();
					syndicates.Add(_syndicates[Syndicate.PerrinSequence], perrinSeqList.ToArray());
					$"Found Perrin Sequence: {perrinSeqList.Count} (warframestat.us)".WriteGoodMessage();
					return syndicates;
				}
			}
			catch (Exception ex)
			{
				$"warframestat.us failed: {ex.Message}".WriteErrorMessage();
			}

			// Fallback to local Docker server (warframe-items)
			try
			{
				"Falling back to localhost:3000...".WriteErrorMessage();

				var newLokaResponse = await _client.GetStringAsync("http://localhost:3000/api/mods/syndicate?syndicate=new_loka");
				var newLokaMods = JsonConvert.DeserializeObject<List<WfItemMod>>(newLokaResponse) ?? new();
				var newLokaList = newLokaMods.Select(m => m.name!).Where(n => !string.IsNullOrEmpty(n)).ToList();

				var perrinResponse = await _client.GetStringAsync("http://localhost:3000/api/mods/syndicate?syndicate=perrin_sequence");
				var perrinMods = JsonConvert.DeserializeObject<List<WfItemMod>>(perrinResponse) ?? new();
				var perrinSeqList = perrinMods.Select(m => m.name!).Where(n => !string.IsNullOrEmpty(n)).ToList();

				syndicates.Add(_syndicates[Syndicate.NewLoka], newLokaList.ToArray());
				$"Found New Loka: {newLokaList.Count} (localhost)".WriteGoodMessage();
				syndicates.Add(_syndicates[Syndicate.PerrinSequence], perrinSeqList.ToArray());
				$"Found Perrin Sequence: {perrinSeqList.Count} (localhost)".WriteGoodMessage();
				return syndicates;
			}
			catch (Exception ex)
			{
				$"localhost:3000 failed: {ex.Message}".WriteErrorMessage();
			}

			// Return empty if all sources fail
			"All mod sources failed!".WriteErrorMessage();
			syndicates.Add(_syndicates[Syndicate.NewLoka], Array.Empty<string>());
			syndicates.Add(_syndicates[Syndicate.PerrinSequence], Array.Empty<string>());
			return syndicates;
		}

		public static async Task Login(string email, string password)
		{
			Console.WriteLine($"Logging in {email} ...");
			var request = new HttpRequestMessage()
			{
				RequestUri = new Uri("https://api.warframe.market/v1/auth/signin"),
				Method = HttpMethod.Post,
			};

			var content = JsonConvert.SerializeObject(new LoginRequest()
			{
				email = email,
				password = password,
				auth_type = "header"
			});
			request.Content = new StringContent(content, Encoding.UTF8, "application/json");
			request.Headers.Add("Authorization", "JWT");
			request.Headers.Add("language", "en");
			request.Headers.Add("accept", "application/json");
			request.Headers.Add("platform", "pc");
			request.Headers.Add("auth_type", "header");
			var response = await _client.SendAsyncWaited(request, _minApiTime);
			var responseBody = await response.Content.ReadAsStringAsync();
			Regex rgxBody = new Regex("\"check_code\": \".*?\"");
			string censoredResponseBody = rgxBody.Replace(responseBody, "\"check_code\": \"REDACTED\"");

			Console.WriteLine("Login " + response.StatusCode);

			if (response.IsSuccessStatusCode)
			{
				SetJWT(response.Headers);

				if (!string.IsNullOrEmpty(_jwt))
				{
					var requestMe = new HttpRequestMessage()
					{
						RequestUri = new Uri("https://api.warframe.market/v2/me"),
						Method = HttpMethod.Get,
					};

					requestMe.Headers.Add("Authorization", "Bearer " + _jwt);
					requestMe.Headers.Add("language", "en");
					requestMe.Headers.Add("accept", "application/json");
					requestMe.Headers.Add("platform", "pc");
					requestMe.Headers.Add("auth_type", "header");

					var responseMe = await _client.SendAsyncWaited(requestMe, _minApiTime);
					var responseMeBody = await responseMe.Content.ReadAsStringAsync();
					var lastWFMarketProfileData = JsonConvert.DeserializeObject<WFMarketProfileData>(responseMeBody);
					if (lastWFMarketProfileData?.data?.verification != true)
					{
						throw new Exception("GetUserLogin, Account not verified.");
					}
					else
					{
						Console.WriteLine("Status " + response.StatusCode);
					}
						requestMe.Dispose();
				}
			}
			else
			{
				Regex rgxEmail = new Regex("[a-zA-Z0-9]");
				string censoredEmail = rgxEmail.Replace(email, "*");
				throw new Exception("GetUserLogin, " + censoredResponseBody + $"Email: {censoredEmail}, Pw length: {password.Length}");
			}

			request.Dispose();
		}

		private static async Task<List<string>> AddItems(ApiResponse<List<WfmItem>>? responseJson, string[] syndicateMods)
		{
			var itemList = new Dictionary<string, string>();
			if (responseJson?.data != null)
			{
				foreach (var item in responseJson.data)
				{
					if (item.id != null && item.name != null)
					{
						if (syndicateMods.Any(i => i.Equals(item.name, StringComparison.CurrentCultureIgnoreCase)))
						{
							itemList.Add(item.id, item.name);
						}
					}
				}
			}

			if (itemList.Count != syndicateMods.Length)
			{
				$"Difference of {(syndicateMods.Length - itemList.Count)}".WriteWarningMessage();

				foreach (var newItem in syndicateMods)
				{
					if (!itemList.Any(t => t.Value.Equals(newItem, StringComparison.CurrentCultureIgnoreCase)))
					{
						newItem.WriteWarningMessage();
					}
				}
			}
			else
			{
				$"All mods found!".WriteGoodMessage();
			}

			var orderIds = new List<string>();
			foreach (var item in itemList)
			{
				var orderId = await AddOrder(item.Key);
				if (orderId != null)
				{
					orderIds.Add(orderId);
					SaveOrders(orderIds);
				}
			}
			return orderIds;
		}

		private static void SaveOrders(List<string> orderIds)
		{
			File.WriteAllText("orders.txt", JsonConvert.SerializeObject(orderIds));
		}

		public static async Task<string?> AddOrder(string itemId)
		{
			try
			{
				using (var request = new HttpRequestMessage()
				{
					RequestUri = new Uri("https://api.warframe.market/v2/order"),
					Method = HttpMethod.Post,
				})
				{
					var json = JsonConvert.SerializeObject(new CreateOrderRequest()
					{
						itemId = itemId,
						type = "sell",
						visible = true,
						platinum = _platinum,
						quantity = 5,
						rank = 0
					});

					request.Content = new StringContent(json, Encoding.UTF8, "application/json");
					request.Headers.Add("Authorization", "Bearer " + _jwt);
					request.Headers.Add("language", "en");
					request.Headers.Add("accept", "application/json");
					request.Headers.Add("platform", "pc");
					request.Headers.Add("auth_type", "header");

					var response = await _client.SendAsyncWaited(request, _maxApiTime);
					var responseBody = await response.Content.ReadAsStringAsync();

					if (!response.IsSuccessStatusCode) throw new Exception(responseBody);
					SetJWT(response.Headers);

					var responseOrder = JsonConvert.DeserializeObject<ApiResponse<WfmOrder>>(responseBody);
					if (responseOrder is null)
					{
						throw new Exception($"Failed to deserialize: {responseBody}");
					}

					if (responseOrder.HasError)
					{
						throw new ApiException($"API Error: {responseOrder.GetErrorMessage()}", responseOrder.apiVersion, responseOrder.error);
					}

					var responseOrderId = responseOrder.data?.id;
					Console.WriteLine($"Added Order: {response.StatusCode} | {responseOrderId}");

					return responseOrderId;
				}
			}
			catch (Exception e)
			{
				e.Message.WriteErrorMessage();
				return null;
			}
		}

		public static async Task DeleteOrders()
		{
			var previousOrders = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("orders.txt"));
			if (previousOrders != null)
			{
				foreach (var previousOrder in previousOrders)
				{
					try
					{
						using (var request = new HttpRequestMessage()
						{
							RequestUri = new Uri($"https://api.warframe.market/v2/order/{previousOrder}"),
							Method = HttpMethod.Delete,
						})
						{
							request.Headers.Add("Authorization", "JWT " + _jwt);
							request.Headers.Add("language", "en");
							request.Headers.Add("accept", "application/json");
							request.Headers.Add("platform", "pc");
							request.Headers.Add("auth_type", "header");

							var response = await _client.SendAsyncWaited(request, _minApiTime);
							var responseBody = await response.Content.ReadAsStringAsync();

							if (!response.IsSuccessStatusCode) throw new Exception(responseBody);
							SetJWT(response.Headers);

							Console.WriteLine($"Deleted Order: {response.StatusCode} | {previousOrder}");
						}
					}
					catch (Exception e)
					{
						e.Message.WriteErrorMessage();
					}
				}
			}
		}

		public static void SetJWT(HttpResponseHeaders headers)
		{
			foreach (var item in headers)
			{
				if (!item.Key.ToLower().Contains("authorization")) continue;
				var temp = item.Value.First();
				_jwt = temp[4..];
				return;
			}
		}

	}
}