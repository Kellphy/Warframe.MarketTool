# Warframe Items Server

A local Docker server that provides Warframe item data via REST API using the [@wfcd/items](https://github.com/WFCD/warframe-items) package.

## Quick Start

```bash
# Build and run with Docker Compose
docker-compose up -d

# Or build manually
docker build -t warframe-items-server .
docker run -d -p 3000:3000 --name warframe-items-server warframe-items-server
```

## API Endpoints

### Items
- `GET /api/items` - Get all items
  - Query params: `category`, `name`, `tradable`
- `GET /api/items/:name` - Get item by name

### Mods
- `GET /api/mods` - Get all mods
  - Query params: `name`, `type`, `tradable`
- `GET /api/mods/:name` - Get mod by name
- `GET /api/mods/syndicate` - Get syndicate augments
  - Query params: `syndicate` (new_loka, perrin_sequence, etc.)

### Utility
- `GET /health` - Health check

## Examples

```bash
# Get all mods with "purity" in name
curl "http://localhost:3000/api/mods?name=purity"

# Get New Loka syndicate mods
curl "http://localhost:3000/api/mods/syndicate?syndicate=new_loka"

# Get Perrin Sequence syndicate mods
curl "http://localhost:3000/api/mods/syndicate?syndicate=perrin_sequence"

# Get specific mod
curl "http://localhost:3000/api/mods/winds-of-purity"
```
