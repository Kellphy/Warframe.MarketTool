namespace Kellphy.Warframe.MarketTool
{

	class ItemList
	{
		public class Item
		{
			public string? id { get; set; }
			public string? url_name { get; set; }
			public string? thumb { get; set; }
			public string? item_name { get; set; }
		}

		public class Payload
		{
			public List<Item>? items { get; set; }
		}

		public class Root
		{
			public Payload? payload { get; set; }
		}
	}
	class Request
	{
		public class OrderMod
		{
			public string? order_type { get; set; }
			public string? item_id { get; set; }
			public int? platinum { get; set; }
			public int? quantity { get; set; }
			public bool? visible { get; set; }
			public int? rank { get; set; }
		}

		public class UserLogin
		{
			public string? auth_type { get; set; }
			public string? email { get; set; }
			public string? password { get; set; }
			public string? device_id { get; set; }
		}
	}
	class WFMOrder
	{
		public class Item
		{
			public string? thumb { get; set; }
			public string? id { get; set; }
			public List<string>? tags { get; set; }
			public string? icon_format { get; set; }
			public object? sub_icon { get; set; }
			public string? url_name { get; set; }
			public string? icon { get; set; }
			public int? mod_max_rank { get; set; }
		}

		public class Order
		{
			public DateTime? last_update { get; set; }
			public string? id { get; set; }
			public Item? item { get; set; }
			public int? quantity { get; set; }
			public string? region { get; set; }
			public string? platform { get; set; }
			public bool? visible { get; set; }
			public int? mod_rank { get; set; }
			public string? order_type { get; set; }
			public int? platinum { get; set; }
			public DateTime? creation_date { get; set; }
		}

		public class Payload
		{
			public Order? order { get; set; }
		}

		public class Root
		{
			public Payload? payload { get; set; }
		}
	}
	class WFMods
	{
		public class Drop
		{
			public double? chance { get; set; }
			public string? location { get; set; }
			public string? rarity { get; set; }
			public string? type { get; set; }
		}

		public class Root
		{
			public string? category { get; set; }
			public string? compatName { get; set; }
			public List<Drop>? drops { get; set; }
			public string? imageName { get; set; }
			public bool? isAugment { get; set; }
			public bool? isExilus { get; set; }
			public bool? isUtility { get; set; }
			public bool? isPrime { get; set; }
			public string? name { get; set; }
			public string? polarity { get; set; }
			public string? rarity { get; set; }
			public bool? tradable { get; set; }
			public bool? transmutable { get; set; }
			public string? type { get; set; }
			public string? uniqueName { get; set; }
			public string? wikiaThumbnail { get; set; }
			public string? wikiaUrl { get; set; }
		}
	}
}
