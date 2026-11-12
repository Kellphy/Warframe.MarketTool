# Warframe.MarketTool
Currently automatically lists Syndicate mods on the Warframe.Market

**Email** and **Password** have to be added to Secrets or replaced in code

Orders are posted at the platinum price you enter, highest market value first (max 100). Market prices (median of the 5 cheapest rank 0 sell orders from online sellers) are cached in `prices.json` for 7 days; new or unpriced mods are fetched on each run.

![image](https://github.com/Kellphy/Warframe.MarketTool/assets/46824693/2925dc38-743c-45b6-8a62-4fba87a87a07)
