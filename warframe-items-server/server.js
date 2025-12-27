import express from 'express';
import Items from '@wfcd/items';

const app = express();
const PORT = process.env.PORT || 3000;

// Load all items at startup
console.log('Loading Warframe items data...');
const allItems = new Items({ category: ['All'] });
const mods = new Items({ category: ['Mods'] });
console.log(`Loaded ${allItems.length} total items, ${mods.length} mods`);

// Get all items
app.get('/api/items', (req, res) => {
  const { category, name, tradable } = req.query;
  
  let results = [...allItems];
  
  if (category) {
    results = results.filter(item => 
      item.category?.toLowerCase() === category.toLowerCase()
    );
  }
  
  if (name) {
    results = results.filter(item => 
      item.name?.toLowerCase().includes(name.toLowerCase())
    );
  }
  
  if (tradable === 'true') {
    results = results.filter(item => item.tradable === true);
  }
  
  res.json(results);
});

// Get all mods
app.get('/api/mods', (req, res) => {
  const { name, type, tradable } = req.query;
  
  let results = [...mods];
  
  if (name) {
    results = results.filter(mod => 
      mod.name?.toLowerCase().includes(name.toLowerCase())
    );
  }
  
  if (type) {
    results = results.filter(mod => 
      mod.type?.toLowerCase().includes(type.toLowerCase())
    );
  }
  
  if (tradable === 'true') {
    results = results.filter(mod => mod.tradable === true);
  }
  
  res.json(results);
});

// Get syndicate mods (sold by syndicate - check drops field)
app.get('/api/mods/syndicate', (req, res) => {
  const { syndicate } = req.query;
  
  // Syndicate names as they appear in drops location
  const syndicateNames = {
    'new_loka': 'new loka',
    'perrin_sequence': 'perrin sequence',
    'steel_meridian': 'steel meridian',
    'arbiters_of_hexis': 'arbiters of hexis',
    'cephalon_suda': 'cephalon suda',
    'red_veil': 'red veil'
  };
  
  const searchName = syndicate ? syndicateNames[syndicate.toLowerCase()] : null;
  
  if (!searchName) {
    // Return all augments if no valid syndicate specified
    const augments = mods.filter(mod => 
      mod.type?.toLowerCase().includes('augment') ||
      mod.isAugment === true
    );
    return res.json(augments);
  }
  
  // Filter mods by drops location containing the syndicate name
  const syndicateMods = mods.filter(mod => {
    if (!mod.drops || !Array.isArray(mod.drops)) return false;
    
    return mod.drops.some(drop => {
      const location = (drop.location || '').toLowerCase();
      return location.includes(searchName);
    });
  });
  
  res.json(syndicateMods);
});

// Get mod by exact name
app.get('/api/mods/:name', (req, res) => {
  const name = req.params.name.toLowerCase().replace(/-/g, ' ');
  
  const mod = mods.find(m => 
    m.name?.toLowerCase() === name ||
    m.name?.toLowerCase().replace(/[^a-z0-9]/g, '') === name.replace(/[^a-z0-9]/g, '')
  );
  
  if (mod) {
    res.json(mod);
  } else {
    res.status(404).json({ error: 'Mod not found', searched: name });
  }
});

// Get item by exact name
app.get('/api/items/:name', (req, res) => {
  const name = req.params.name.toLowerCase().replace(/-/g, ' ');
  
  const item = allItems.find(i => 
    i.name?.toLowerCase() === name ||
    i.name?.toLowerCase().replace(/[^a-z0-9]/g, '') === name.replace(/[^a-z0-9]/g, '')
  );
  
  if (item) {
    res.json(item);
  } else {
    res.status(404).json({ error: 'Item not found', searched: name });
  }
});

// Health check
app.get('/health', (req, res) => {
  res.json({ 
    status: 'ok', 
    totalItems: allItems.length,
    totalMods: mods.length 
  });
});

app.listen(PORT, () => {
  console.log(`Warframe Items API running on http://localhost:${PORT}`);
  console.log('Endpoints:');
  console.log('  GET /api/items - All items (query: category, name, tradable)');
  console.log('  GET /api/items/:name - Single item by name');
  console.log('  GET /api/mods - All mods (query: name, type, tradable)');
  console.log('  GET /api/mods/:name - Single mod by name');
  console.log('  GET /api/mods/syndicate - Syndicate augments (query: syndicate)');
  console.log('  GET /health - Health check');
});
