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
			_client.DefaultRequestHeaders.UserAgent.ParseAdd("Kellphy.Warframe.MarketTool/1.0");
			_client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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
				try
				{
					await Logic();
					"Completed! Press any key to restart.".WriteInfoMessage();
				}
				catch (Exception ex)
				{
					$"Error: {ex.Message}".WriteErrorMessage();
					"Press any key to restart.".WriteInfoMessage();
				}

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

			var modList = await GetModList(responseJson.data?.Select(i => i.name).OfType<string>() ?? Enumerable.Empty<string>());

			Console.WriteLine(
				$"[1] {_syndicates[Syndicate.NewLoka]} + {_syndicates[Syndicate.PerrinSequence]}" +
				$"\n[2] {_syndicates[Syndicate.NewLoka]}" +
				$"\n[3] {_syndicates[Syndicate.PerrinSequence]}" +
				$"\n[4] Custom Orders" +
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
					Console.Write("Platinum price: ");
					if (int.TryParse(Console.ReadLine(), out int newPlatinum))
					{
						_platinum = newPlatinum;
					}
					$"Using price: {_platinum}".WriteInfoMessage();
					break;
			}

			await DeleteOrders();

			IEnumerable<string>? modsToList = null;
			switch (choice.Key)
			{
				case ConsoleKey.D1:
					modsToList = modList[_syndicates[Syndicate.NewLoka]]
						.Concat(modList[_syndicates[Syndicate.PerrinSequence]]);
					break;
				case ConsoleKey.D2:
					modsToList = modList[_syndicates[Syndicate.NewLoka]];
					break;
				case ConsoleKey.D3:
					modsToList = modList[_syndicates[Syndicate.PerrinSequence]];
					break;
				case ConsoleKey.D4:
					var customOrdersFile = "custom-orders.txt";
					if (!File.Exists(customOrdersFile))
					{
						File.Create(customOrdersFile).Close();
					}
					modsToList = File.ReadAllLines(customOrdersFile).Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim());
					break;
			}

			var orderIds = new List<string>();
			if (modsToList != null)
			{
				var items = ResolveItems(responseJson.data, modsToList);
				var sortedItems = await SortByMarketPrice(items);
				orderIds.AddRange(await AddItems(sortedItems.Take(100)));
			}

			$"Added Orders: {orderIds.Count}".WriteGoodMessage();
		}

		private const string _priceCacheFile = "prices.json";
		private static readonly TimeSpan _priceCacheDuration = TimeSpan.FromDays(7);

		private static List<WfmItem> ResolveItems(List<WfmItem>? marketItems, IEnumerable<string> modNames)
		{
			var itemsByName = new Dictionary<string, WfmItem>(StringComparer.OrdinalIgnoreCase);
			foreach (var item in marketItems ?? new())
			{
				if (item.id != null && item.slug != null && item.name != null)
				{
					itemsByName.TryAdd(item.name, item);
				}
			}

			var resolved = new List<WfmItem>();
			var missing = new List<string>();
			foreach (var name in modNames.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (itemsByName.TryGetValue(name, out var item))
				{
					resolved.Add(item);
				}
				else
				{
					missing.Add(name);
				}
			}

			if (missing.Count > 0)
			{
				$"Not found on warframe.market ({missing.Count}): {string.Join(", ", missing)}".WriteWarningMessage();
			}
			else
			{
				"All mods found!".WriteGoodMessage();
			}

			return resolved;
		}

		private static Dictionary<string, PriceCacheEntry> LoadPriceCache()
		{
			try
			{
				if (File.Exists(_priceCacheFile))
				{
					var cache = JsonConvert.DeserializeObject<Dictionary<string, PriceCacheEntry>>(File.ReadAllText(_priceCacheFile));
					if (cache != null)
					{
						return new Dictionary<string, PriceCacheEntry>(cache, StringComparer.OrdinalIgnoreCase);
					}
				}
			}
			catch (Exception ex)
			{
				$"Price cache unreadable, rebuilding: {ex.Message}".WriteWarningMessage();
			}

			return new Dictionary<string, PriceCacheEntry>(StringComparer.OrdinalIgnoreCase);
		}

		private static async Task<List<WfmItem>> SortByMarketPrice(List<WfmItem> items)
		{
			var cache = LoadPriceCache();
			// Items without a cached price (new or never listed) are re-checked every run.
			var stale = items
				.Where(i => !cache.TryGetValue(i.slug!, out var entry)
					|| entry.platinum == null
					|| DateTime.UtcNow - entry.updatedAt > _priceCacheDuration)
				.ToList();

			if (stale.Count > 0)
			{
				$"Fetching market prices for {stale.Count} mod(s)...".WriteInfoMessage();
				var progress = 0;
				foreach (var item in stale)
				{
					progress++;
					try
					{
						var price = await GetMarketPrice(item.slug!, GetOrderSubtype(item));
						cache[item.slug!] = new PriceCacheEntry { platinum = price, updatedAt = DateTime.UtcNow };
						var line = $"[{progress}/{stale.Count}] {item.name}: {(price?.ToString() ?? "no sell orders")}";
						if (price != null)
						{
							line.WriteGoodMessage();
						}
						else
						{
							line.WriteWarningMessage();
						}
					}
					catch (Exception ex)
					{
						$"Price lookup failed for {item.name}: {ex.Message}".WriteErrorMessage();
					}
				}

				File.WriteAllText(_priceCacheFile, JsonConvert.SerializeObject(cache, Formatting.Indented));
			}
			else
			{
				$"Using cached market prices for {items.Count} mod(s)".WriteInfoMessage();
			}

			int PriceOf(WfmItem item) => cache.TryGetValue(item.slug!, out var entry) && entry.platinum != null ? entry.platinum.Value : -1;

			var sorted = items.OrderByDescending(PriceOf).ThenBy(i => i.name).ToList();
			var unpriced = sorted.Where(i => PriceOf(i) < 0).Select(i => i.name).ToList();
			if (unpriced.Count > 0)
			{
				$"No market price, listed last: {string.Join(", ", unpriced)}".WriteWarningMessage();
			}

			return sorted;
		}

		// Items with variants (e.g. "regular"/"atragraph" augments) require a subtype on orders.
		private static string? GetOrderSubtype(WfmItem item)
		{
			if (item.subtypes == null || item.subtypes.Count == 0)
			{
				return null;
			}

			return item.subtypes.FirstOrDefault(s => s.Equals("regular", StringComparison.OrdinalIgnoreCase)) ?? item.subtypes[0];
		}

		private static async Task<int?> GetMarketPrice(string slug, string? subtype)
		{
			using var response = await _client.GetAsyncWaited($"https://api.warframe.market/v2/orders/item/{slug}", _minApiTime);
			var responseContent = await response.Content.ReadAsStringAsync();
			var orders = JsonConvert.DeserializeObject<ApiResponse<List<WfmItemOrder>>>(responseContent);
			if (orders?.HasError == true)
			{
				throw new ApiException($"API Error: {orders.GetErrorMessage()}", orders.apiVersion, orders.error);
			}

			var sellOrders = orders?.data?
				.Where(o => o.type == "sell" && o.visible != false && (o.rank ?? 0) == 0 && o.platinum != null
					&& (subtype == null || o.subtype == null || o.subtype.Equals(subtype, StringComparison.OrdinalIgnoreCase)))
				.ToList() ?? new();
			var onlineSellOrders = sellOrders
				.Where(o => o.user?.status == "ingame" || o.user?.status == "online")
				.ToList();

			var source = onlineSellOrders.Count > 0 ? onlineSellOrders : sellOrders;
			// Median of the cheapest few listings, so a single troll/lowball order doesn't skew the price.
			var cheapest = source.Select(o => o.platinum!.Value).OrderBy(p => p).Take(5).ToList();
			if (cheapest.Count == 0)
			{
				return null;
			}

			var middle = cheapest.Count / 2;
			return cheapest.Count % 2 == 1
				? cheapest[middle]
				: (int)Math.Round((cheapest[middle - 1] + cheapest[middle]) / 2.0, MidpointRounding.AwayFromZero);
		}

		private static readonly Dictionary<string, HashSet<string>> _syndicateAugments = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "New Loka", new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
				"Elusive Retribution", "Endless Lullaby", "Reactive Storm",
				"Duality", "Calm & Frenzy", "Peaceful Provocation", "Energy Transfer",
				"Shattered Storm", "Mending Splinters", "Spectrosiphon",
				"Viral Tempest", "Tidal Impunity", "Rousing Plunder", "Pilfering Swarm",
				"Omikuji's Fortune",
				"Volatile Recompense", "Wrath of Ukko",
				"Valence Formation", "Swift Bite", "Lingering Transmutation",
				"Greedy Pull", "Magnetized Discharge", "Counter Pulse", "Fracturing Crush",
				"Mind Freak", "Pacifying Bolts", "Chaos Sphere", "Assimilate",
				"Smite Infusion", "Hallowed Eruption", "Phoenix Renewal", "Hallowed Reckoning",
				"Partitioned Mallet", "Conductor",
				"Axios Javelineers", "Tharros Lethality", "Intrepid Stand",
				"Spellbound Harvest", "Beguiling Lantern", "Razorwing Blitz", "Ironclad Flight",
				"Pool of Life", "Vampire Leech", "Abating Link", "Champion's Blessing",
				"Swing Line", "Eternal War", "Prolonged Paralysis", "Enraged", "Hysterical Assault",
				"Fused Reservoir", "Critical Surge", "Cataclysmic Gate",
				"Celestial Stomp", "Enveloping Cloud", "Primal Rage",
				"Merulina Guardian", "Loyal Merulina", "Surging Blades",
				"Target Fixation", "Airburst Rounds", "Jet Stream", "Funnel Clouds", "Anchored Glide",
				"Kumihimo Loading", "Rhythm Guard",
				"Winds of Purity", "Disarming Purity", "Bright Purity", "Lasting Purity",
			}},
			{ "Perrin Sequence", new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
				"Sonic Siphon", "Resonance", "Savage Silence", "Gaseous Quake",
				"Razor Mortar",
				"Everlasting Ward", "Guardian Armor", "Vexing Retaliation", "Guided Effigy",
				"Spectral Spirit",
				"Mach Crash", "Thermal Transfer",
				"Coil Recharge", "Cathode Current", "Conductive Sphere", "Reverse Rotorswell",
				"Balefire Surge", "Blazing Pillage", "Aegis Gale",
				"Elemental Sandstorm", "Negation Armor", "Desiccation's Curse",
				"Empowered Quiver", "Piercing Navigator", "Infiltrate", "Concentrated Arrow",
				"Greedy Pull", "Magnetized Discharge", "Counter Pulse", "Fracturing Crush",
				"Soul Survivor", "Creeping Terrify", "Despoil", "Shield of Shadows",
				"Abundant Mutation", "Teeming Virulence", "Larva Burst", "Parasitic Vitality", "Insatiable",
				"Repair Dispensary", "Temporal Erosion", "Temporal Artillery",
				"Thrall Pact", "Mesmer Shield", "Blinding Reave",
				"Ironclad Charge", "Iron Shrapnel", "Piercing Roar", "Reinforcing Stomp",
				"Shadow Haze", "Dark Propagation",
				"Pool of Life", "Vampire Leech", "Abating Link", "Champion's Blessing",
				"Swing Line", "Eternal War", "Prolonged Paralysis", "Enraged", "Hysterical Assault",
				"Tesla Bank", "Photon Repeater", "Enduring Bastille",
				"Afterburn", "Reroot Rampage",
				"Toxic Sequence", "Deadly Sequence", "Voltage Sequence", "Sequence Burn",
			}},
		};

		private static readonly Dictionary<Syndicate, string> _wikiPages = new()
		{
			{ Syndicate.NewLoka, "New_Loka" },
			{ Syndicate.PerrinSequence, "The_Perrin_Sequence" }
		};

		// Wiki offering names that differ from warframe.market item names.
		private static readonly Dictionary<string, string> _wikiNameAliases = new(StringComparer.OrdinalIgnoreCase)
		{
			{ "Repelling Bastille", "Enduring Bastille" },
			{ "Resonating Quake", "Gaseous Quake" }
		};

		private static readonly Regex _wikiModOffer = new(@"\{\{SynOfferBox\|[^|}]*Mod\.png\|([^|}]+)\|", RegexOptions.IgnoreCase);

		private static async Task<Dictionary<string, string[]>?> GetWikiModList(IEnumerable<string> marketItemNames)
		{
			var marketNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var name in marketItemNames)
			{
				marketNames.TryAdd(name, name);
			}

			var syndicates = new Dictionary<string, string[]>();
			foreach (var (syndicate, page) in _wikiPages)
			{
				var wikitext = await _client.GetStringAsync($"https://wiki.warframe.com/w/{page}?action=raw");
				var mods = new List<string>();
				var unmatched = new List<string>();

				foreach (Match match in _wikiModOffer.Matches(wikitext))
				{
					var name = match.Groups[1].Value.Replace('_', ' ').Trim();
					if (_wikiNameAliases.TryGetValue(name, out var alias))
					{
						name = alias;
					}

					if (marketNames.TryGetValue(name, out var marketName))
					{
						if (!mods.Contains(marketName))
						{
							mods.Add(marketName);
						}
					}
					else
					{
						unmatched.Add(name);
					}
				}

				if (mods.Count == 0)
				{
					$"wiki: no {_syndicates[syndicate]} mod offerings found".WriteErrorMessage();
					return null;
				}

				if (unmatched.Count > 0)
				{
					$"wiki: {_syndicates[syndicate]} offerings not on warframe.market: {string.Join(", ", unmatched)}".WriteWarningMessage();
				}

				syndicates.Add(_syndicates[syndicate], mods.ToArray());
				$"Found {_syndicates[syndicate]}: {mods.Count} (wiki)".WriteGoodMessage();
			}

			return syndicates;
		}

		private static async Task<Dictionary<string, string[]>> GetModList(IEnumerable<string> marketItemNames)
		{
			Dictionary<string, string[]> syndicates = new();

			try
			{
				var wikiMods = await GetWikiModList(marketItemNames);
				if (wikiMods != null)
				{
					return wikiMods;
				}
			}
			catch (Exception ex)
			{
				$"wiki failed: {ex.Message}".WriteErrorMessage();
			}

			try
			{
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

				// Fallback: if drops data is missing, use static syndicate mapping with isAugment check
				if (newLokaList.Count == 0 && perrinSeqList.Count == 0 && allMods.Count > 0)
				{
					"warframestat.us has no drops data, using static syndicate mapping...".WriteErrorMessage();

					foreach (var mod in allMods)
					{
						if (mod.name == null) continue;
						if (mod.name.Equals("Sacrifice", StringComparison.OrdinalIgnoreCase)) continue;

						if (_syndicateAugments[_syndicates[Syndicate.NewLoka]].Contains(mod.name))
							newLokaList.Add(mod.name);
						if (_syndicateAugments[_syndicates[Syndicate.PerrinSequence]].Contains(mod.name))
							perrinSeqList.Add(mod.name);
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

		private static async Task<List<string>> AddItems(IEnumerable<WfmItem> items)
		{
			var orderIds = new List<string>();
			foreach (var item in items)
			{
				var orderId = await AddOrder(item.id!, item.name, GetOrderSubtype(item));
				if (orderId != null)
				{
					orderIds.Add(orderId);
					SaveOrder(orderId);
				}
			}
			return orderIds;
		}

		private static void SaveOrder(string orderId)
		{
			File.AppendAllText("orders.txt", orderId + Environment.NewLine);
		}

		public static async Task<string?> AddOrder(string itemId, string? itemName = null, string? subtype = null)
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
						rank = 0,
						subtype = subtype
					});

					request.Content = new StringContent(json, Encoding.UTF8, "application/json");
					request.Headers.Add("Authorization", "Bearer " + _jwt);
					request.Headers.Add("language", "en");
					request.Headers.Add("accept", "application/json");
					request.Headers.Add("platform", "pc");
					request.Headers.Add("auth_type", "header");

					var response = await _client.SendAsyncWaited(request, _maxApiTime);
					var responseBody = await response.Content.ReadAsStringAsync();

					if (!response.IsSuccessStatusCode) throw new Exception($"HTTP {(int)response.StatusCode} {response.StatusCode}: {responseBody}");
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
					Console.WriteLine($"Added Order: {response.StatusCode} | {responseOrderId} | {itemName}");

					return responseOrderId;
				}
			}
			catch (Exception e)
			{
				$"Failed Order: {itemName ?? itemId} | {e.Message}".WriteErrorMessage();
				return null;
			}
		}

		public static async Task DeleteOrders()
		{
			var previousOrders = File.ReadAllLines("orders.txt").Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
			if (previousOrders.Count > 0)
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
							request.Headers.Add("Authorization", "Bearer " + _jwt);
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

				File.WriteAllText("orders.txt", string.Empty);
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