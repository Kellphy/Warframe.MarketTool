using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Kellphy.Warframe.MarketTool
{
	internal class Program
	{

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

				Console.WriteLine("--- Completed! Press any key to reset.");
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

			var response = await _client.GetAsync("https://api.warframe.market/v1/items");
			var responseContent = await response.Content.ReadAsStringAsync();
			var responseJson = JsonConvert.DeserializeObject<ItemList.Root>(responseContent);

			if (responseJson is null)
			{
				"Null Response".WriteErrorMessage();
				return;
			}

			await Login(_email, _password);

			var modList = await GetModList();

			Console.WriteLine(
				"[1] New Loka > Perrin Seq" +
				"\n[2] Perrin Seq > New Loka" +
				"\n[3] New Loka" +
				"\n[4] Perrin Seq" +
				"\n[.] Clear");
			var choice = Console.ReadKey();
			Console.WriteLine();

			await DeleteOrders();

			switch (choice.Key)
			{
				case ConsoleKey.D1:
				case ConsoleKey.D2:
				case ConsoleKey.D3:
				case ConsoleKey.D4:
					Console.Write("Platinum for mods: ");
					if (int.TryParse(Console.ReadLine(), out int newPlatinum))
					{
						_platinum = newPlatinum;
					}
					Console.WriteLine("--- Using price: " + _platinum);
					break;
			}

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
			}

			File.WriteAllText("orders.txt", JsonConvert.SerializeObject(orderIds));
			$"Added Orders: {orderIds.Count}".WriteGoodMessage();
		}

		private static async Task<Dictionary<string, string[]>> GetModList()
		{
			Dictionary<string, string[]> syndicates = new();
			HttpClient client = new HttpClient()
			{
				BaseAddress = new Uri($"https://api.warframestat.us/mods")
			};
			client.DefaultRequestHeaders.Accept.Clear();

			HttpResponseMessage response = new();
			response = await client.GetAsync(client.BaseAddress.ToString());
			var responseJson = JsonConvert.DeserializeObject<WFMods.Root[]>(await response.Content.ReadAsStringAsync());

			var newLokaList = new List<string>();
			var perrinSeqList = new List<string>();

			if (responseJson != null)
			{
				foreach (var json in responseJson)
				{
					if (json.drops?.Any(t => t.location?.Contains(_syndicates[Syndicate.NewLoka], StringComparison.CurrentCultureIgnoreCase) is true) is true)
					{
						if (json.name != null && json.name.ToLower() != "sacrifice")
						{
							newLokaList.Add(json.name);
						}
					}
					if (json.drops?.Any(t => t.location?.Contains(_syndicates[Syndicate.PerrinSequence], StringComparison.CurrentCultureIgnoreCase) is true) is true)
					{
						if (json.name != null)
						{
							perrinSeqList.Add(json.name);
						}
					}
				}
			}

			syndicates.Add(_syndicates[Syndicate.NewLoka], newLokaList.ToArray());
			$"Found New Loka: {newLokaList.Count}".WriteGoodMessage();
			syndicates.Add(_syndicates[Syndicate.PerrinSequence], perrinSeqList.ToArray());
			$"Found Perrin Sequence: {perrinSeqList.Count}".WriteGoodMessage();
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

			var content = JsonConvert.SerializeObject(new Request.UserLogin()
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
			var response = await _client.SendAsync(request);
			var responseBody = await response.Content.ReadAsStringAsync();
			Regex rgxBody = new Regex("\"check_code\": \".*?\"");
			string censoredResponse = rgxBody.Replace(responseBody, "\"check_code\": \"REDACTED\"");

			Console.WriteLine("Login " + response.StatusCode);

			if (response.IsSuccessStatusCode)
			{
				SetJWT(response.Headers);
			}
			else
			{
				Regex rgxEmail = new Regex("[a-zA-Z0-9]");
				string censoredEmail = rgxEmail.Replace(email, "*");
				throw new Exception("GetUserLogin, " + responseBody + $"Email: {censoredEmail}, Pw length: {password.Length}");
			}
			request.Dispose();
		}

		private static async Task<List<string>> AddItems(ItemList.Root? responseJson, string[] syndicateMods)
		{
			var itemList = new Dictionary<string, string>();
			if (responseJson?.payload?.items != null)
			{
				foreach (var item in responseJson.payload.items)
				{
					if (item.id != null && item.item_name != null)
					{
						if (syndicateMods.Any(i => i.Equals(item.item_name, StringComparison.CurrentCultureIgnoreCase)))
						{
							itemList.Add(item.id, item.item_name);
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
				}
				await Task.Delay(TimeSpan.FromSeconds(1)); //I don't want to spam WFM. Default: 250ms
			}
			return orderIds;
		}

		public static async Task<string?> AddOrder(string itemId)
		{
			try
			{
				using (var request = new HttpRequestMessage()
				{
					RequestUri = new Uri("https://api.warframe.market/v1/profile/orders"),
					Method = HttpMethod.Post,
				})
				{
					var json = JsonConvert.SerializeObject(new Request.OrderMod()
					{
						item_id = itemId,
						order_type = "sell",
						platinum = _platinum,
						quantity = 1,
						rank = 0,
						visible = true
					});

					request.Content = new StringContent(json, Encoding.UTF8, "application/json");
					request.Headers.Add("Authorization", "JWT " + _jwt);
					request.Headers.Add("language", "en");
					request.Headers.Add("accept", "application/json");
					request.Headers.Add("platform", "pc");
					request.Headers.Add("auth_type", "header");

					var response = await _client.SendAsync(request);
					var responseBody = await response.Content.ReadAsStringAsync();

					if (!response.IsSuccessStatusCode) throw new Exception(responseBody);
					SetJWT(response.Headers);

					Console.WriteLine("Add Order: " + response.StatusCode);

					var responseOrder = JsonConvert.DeserializeObject<WFMOrder.Root>(responseBody);
					if (responseOrder != null)
					{
						var responseOrderId = responseOrder?.payload?.order?.id;
						Console.WriteLine("Order Id: " + responseOrderId);
						return responseOrderId;
					}
				}
			}
			catch (Exception e)
			{
				e.Message.WriteErrorMessage();
				return null;
			}

			return null;
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
							RequestUri = new Uri($"https://api.warframe.market/v1/profile/orders/{previousOrder}"),
							Method = HttpMethod.Delete,
						})
						{
							request.Headers.Add("Authorization", "JWT " + _jwt);
							request.Headers.Add("language", "en");
							request.Headers.Add("accept", "application/json");
							request.Headers.Add("platform", "pc");
							request.Headers.Add("auth_type", "header");

							var response = await _client.SendAsync(request);
							var responseBody = await response.Content.ReadAsStringAsync();

							if (!response.IsSuccessStatusCode) throw new Exception(responseBody);
							SetJWT(response.Headers);

							Console.WriteLine("Delete Order: " + response.StatusCode);
							await Task.Delay(500);
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