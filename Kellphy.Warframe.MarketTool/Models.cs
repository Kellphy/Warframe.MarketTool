namespace Kellphy.Warframe.MarketTool;

public class WFMarketProfileData
{
	public WFProfileDataProfile data { get; set; }
}
public class WFProfileDataProfile
{
	public bool banned { get; set; }

	public string id { get; set; }

	public int reputation { get; set; }

	public object check_code { get; set; }

	public string platform { get; set; }

	public bool crossplay { get; set; }

	public string role { get; set; }

	public int unread_messages { get; set; }

	public bool hasEmail { get; set; }

	public bool verification { get; set; }

	public string checkCode { get; set; }

	public string ingameName { get; set; }

	public string slug { get; set; }
}

#region API Base

/// <summary>
/// Base class for Warframe Market API v2 responses
/// </summary>
public class ApiResponse
{
	public string? apiVersion { get; set; }
	public object? error { get; set; }

	public bool HasError => error != null;
	public string? GetErrorMessage() => error?.ToString();
}

public class ApiResponse<T> : ApiResponse
{
	public T? data { get; set; }
}

public class ApiException : Exception
{
	public string? ApiVersion { get; }
	public object? ApiError { get; }

	public ApiException(string message, string? apiVersion = null, object? apiError = null)
		: base(message)
	{
		ApiVersion = apiVersion;
		ApiError = apiError;
	}
}

#endregion

#region Warframe Market API

public class WfmItem
{
	public string? id { get; set; }
	public string? slug { get; set; }
	public string? gameRef { get; set; }
	public List<string>? tags { get; set; }
	public int? maxRank { get; set; }
	public WfmI18n? i18n { get; set; }

	public string? name => i18n?.en?.name;
}

public class WfmI18n
{
	public WfmI18nLanguage? en { get; set; }
}

public class WfmI18nLanguage
{
	public string? name { get; set; }
	public string? icon { get; set; }
	public string? thumb { get; set; }
}

public class WfmOrder
{
	public string? id { get; set; }
	public string? type { get; set; }
	public int? platinum { get; set; }
	public int? quantity { get; set; }
	public int? perTrade { get; set; }
	public int? rank { get; set; }
	public bool? visible { get; set; }
	public DateTime? createdAt { get; set; }
	public DateTime? updatedAt { get; set; }
	public string? itemId { get; set; }
}

public class WfmUser
{
	public string? id { get; set; }
	public string? role { get; set; }
	public string? tier { get; set; }
	public bool subscription { get; set; }
	public string? ingameName { get; set; }
	public string? slug { get; set; }
	public string? avatar { get; set; }
	public string? about { get; set; }
	public string? aboutRaw { get; set; }
	public int reputation { get; set; }
	public int masteryRank { get; set; }
	public int credits { get; set; }
	public DateTime? lastSeen { get; set; }
	public string? platform { get; set; }
	public bool crossplay { get; set; }
	public string? locale { get; set; }
	public string? theme { get; set; }
	public bool syncLocale { get; set; }
	public bool syncTheme { get; set; }
	public bool verification { get; set; }
	public string? checkCode { get; set; }
	public DateTime? createdAt { get; set; }
	public int reviewsLeft { get; set; }
	public int unreadNotifications { get; set; }
	public WfmLinkedAccounts? linkedAccounts { get; set; }
	public bool hasEmail { get; set; }
}

public class WfmLinkedAccounts
{
	public bool steam { get; set; }
	public bool discord { get; set; }
	public bool xbox { get; set; }
	public bool playstation { get; set; }
	public bool github { get; set; }
	public bool patreon { get; set; }
}

#endregion

#region Request Models

internal class CreateOrderRequest
{
	public string? itemId { get; set; }
	public string? type { get; set; }
	public bool? visible { get; set; }
	public int? platinum { get; set; }
	public int? quantity { get; set; }
	public int? rank { get; set; }
}

internal class LoginRequest
{
	public string? auth_type { get; set; }
	public string? email { get; set; }
	public string? password { get; set; }
	public string? device_id { get; set; }
}

#endregion

#region Warframestat.us API

internal class WfStatMod
{
	public string? name { get; set; }
	public string? uniqueName { get; set; }
	public List<WfStatLevelStats>? levelStats { get; set; }
	public List<WfStatDrop>? drops { get; set; }
}

internal class WfStatLevelStats
{
	public List<string>? stats { get; set; }
}

internal class WfStatDrop
{
	public string? location { get; set; }
	public string? type { get; set; }
	public double? chance { get; set; }
	public string? rarity { get; set; }
}

#endregion

#region Warframe-Items (localhost)

internal class WfItemMod
{
	public string? name { get; set; }
	public string? uniqueName { get; set; }
	public string? type { get; set; }
	public string? description { get; set; }
}

#endregion

