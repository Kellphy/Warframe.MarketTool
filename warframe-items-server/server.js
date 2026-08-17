import express from 'express';
import Items from '@wfcd/items';

const app = express();
const PORT = process.env.PORT || 3000;

// Load all items at startup
console.log('Loading Warframe items data...');
const allItems = new Items({ category: ['All'] });
const mods = new Items({ category: ['Mods'] });
console.log(`Loaded ${allItems.length} total items, ${mods.length} mods`);

// Static syndicate-to-augment mapping (from Warframe Wiki)
// Used as fallback when @wfcd/items doesn't include drops data
const syndicateAugments = {
  'new loka': [
    // Baruuk
    'Elusive Retribution', 'Endless Lullaby', 'Reactive Storm',
    // Equinox
    'Duality', 'Calm & Frenzy', 'Peaceful Provocation', 'Energy Transfer',
    // Gara
    'Shattered Storm', 'Mending Splinters', 'Spectrosiphon',
    // Hydroid
    'Viral Tempest', 'Tidal Impunity', 'Rousing Plunder', 'Pilfering Swarm',
    // Koumei
    "Omikuji's Fortune",
    // Kullervo
    'Volatile Recompense', 'Wrath of Ukko',
    // Lavos
    'Valence Formation', 'Swift Bite', 'Lingering Transmutation',
    // Mag
    'Greedy Pull', 'Magnetized Discharge', 'Counter Pulse', 'Fracturing Crush',
    // Nyx
    'Mind Freak', 'Pacifying Bolts', 'Chaos Sphere', 'Assimilate',
    // Oberon
    'Smite Infusion', 'Hallowed Eruption', 'Phoenix Renewal', 'Hallowed Reckoning',
    // Octavia
    'Partitioned Mallet', 'Conductor',
    // Styanax
    'Axios Javelineers', 'Tharros Lethality', 'Intrepid Stand',
    // Titania
    'Spellbound Harvest', 'Beguiling Lantern', 'Razorwing Blitz', 'Ironclad Flight',
    // Trinity
    'Pool of Life', 'Vampire Leech', 'Abating Link', "Champion's Blessing",
    // Valkyr
    'Swing Line', 'Eternal War', 'Prolonged Paralysis', 'Enraged', 'Hysterical Assault',
    // Wisp
    'Fused Reservoir', 'Critical Surge', 'Cataclysmic Gate',
    // Wukong
    'Celestial Stomp', 'Enveloping Cloud', 'Primal Rage',
    // Yareli
    'Merulina Guardian', 'Loyal Merulina', 'Surging Blades',
    // Zephyr
    'Target Fixation', 'Airburst Rounds', 'Jet Stream', 'Funnel Clouds', 'Anchored Glide',
  ],
  'perrin sequence': [
    // Banshee
    'Sonic Fracture', 'Resonance', 'Savage Silence', 'Resonating Quake',
    // Caliban
    'Razor Mortar',
    // Chroma
    'Everlasting Ward', 'Guardian Armor', 'Vexing Retaliation', 'Guided Effigy',
    // Dagath
    'Spectral Spirit',
    // Gauss
    'Mach Crash', 'Thermal Transfer',
    // Gyre
    'Coil Recharge', 'Cathode Current', 'Conductive Sphere', 'Reverse Rotorswell',
    // Hildryn
    'Balefire Surge', 'Blazing Pillage', 'Aegis Gale',
    // Inaros
    'Elemental Sandstorm', 'Negation Armor', "Desiccation's Curse",
    // Ivara
    'Empowered Quiver', 'Piercing Navigator', 'Infiltrate', 'Concentrated Arrow',
    // Mag
    'Greedy Pull', 'Magnetized Discharge', 'Counter Pulse', 'Fracturing Crush',
    // Nekros
    'Soul Survivor', 'Creeping Terrify', 'Despoil', 'Shield of Shadows',
    // Nidus
    'Abundant Mutation', 'Teeming Virulence', 'Larva Burst', 'Parasitic Vitality', 'Insatiable',
    // Protea
    'Repair Dispensary', 'Temporal Erosion', 'Temporal Artillery',
    // Revenant
    'Thrall Pact', 'Mesmer Shield', 'Blinding Reave',
    // Rhino
    'Ironclad Charge', 'Iron Shrapnel', 'Piercing Roar', 'Reinforcing Stomp',
    // Sevagoth
    'Shadow Haze', 'Dark Propagation',
    // Trinity
    'Pool of Life', 'Vampire Leech', 'Abating Link', "Champion's Blessing",
    // Valkyr
    'Swing Line', 'Eternal War', 'Prolonged Paralysis', 'Enraged', 'Hysterical Assault',
    // Vauban
    'Tesla Bank', 'Photon Repeater', 'Enduring Bastille',
  ],
  'steel meridian': [
    // Atlas
    'Rubble Heap', 'Path of Statues', 'Tectonic Fracture', 'Ore Gaze', 'Titanic Rumbler',
    // Citrine
    'Prismatic Companion', 'Recrystalize',
    // Ember
    'Fireball Frenzy', 'Immolated Radiance', 'Healing Flame', 'Exothermic',
    // Excalibur
    'Surging Dash', 'Radiant Finish', 'Furious Javelin', 'Chromatic Blade',
    // Frost
    'Biting Frost', 'Freeze Force', 'Ice Wave Impedance', 'Chilling Globe', 'Icy Avalanche',
    // Garuda
    'Dread Ward', 'Blood Forge', 'Blending Talons',
    // Grendel
    'Gourmand', 'Hearty Nourishment', 'Catapult', 'Gastro',
    // Khora
    'Accumulating Whipclaw', 'Venari Bodyguard', 'Pilfering Strangledome',
    // Kullervo
    'Volatile Recompense', 'Wrath of Ukko',
    // Mesa
    "Ballistic Bullseye", 'Muzzle Flash', 'Staggering Shield', "Mesa's Waltz",
    // Nezha
    'Pyroclastic Flow', 'Reaping Chakram', 'Safeguard', 'Controlled Slide', 'Divine Retribution',
    // Nidus
    'Abundant Mutation', 'Teeming Virulence', 'Larva Burst', 'Parasitic Vitality', 'Insatiable',
    // Nova
    'Neutron Star', 'Antimatter Absorb', 'Escape Velocity', 'Molecular Fission',
    // Oberon
    'Smite Infusion', 'Hallowed Eruption', 'Phoenix Renewal', 'Hallowed Reckoning',
    // Qorvex
    'Wrecking Wall', 'Fused Crucible',
    // Rhino
    'Ironclad Charge', 'Iron Shrapnel', 'Piercing Roar', 'Reinforcing Stomp',
    // Saryn
    'Revealing Spores', 'Venom Dose', 'Regenerative Molt', 'Contagion Cloud',
    // Voruna
    'Prey of Dynar', "Ulfrun's Endurance",
    // Xaku
    'Vampiric Grasp', 'The Relentless Lost', 'Untime Rift',
  ],
  'arbiters of hexis': [
    // Ash
    'Seeking Shuriken', 'Smoke Shadow', 'Teleport Rush', 'Rising Storm',
    // Baruuk
    'Elusive Retribution', 'Endless Lullaby', 'Reactive Storm',
    // Equinox
    'Duality', 'Calm & Frenzy', 'Peaceful Provocation', 'Energy Transfer',
    // Excalibur
    'Surging Dash', 'Radiant Finish', 'Furious Javelin', 'Chromatic Blade',
    // Excalibur Umbra
    "Warrior's Rest",
    // Gara
    'Shattered Storm', 'Mending Splinters', 'Spectrosiphon',
    // Gauss
    'Mach Crash', 'Thermal Transfer',
    // Gyre
    'Coil Recharge', 'Cathode Current', 'Conductive Sphere', 'Reverse Rotorswell',
    // Harrow
    'Tribunal', 'Warding Thurible', 'Lasting Covenant',
    // Inaros
    'Elemental Sandstorm', 'Negation Armor', "Desiccation's Curse",
    // Jade
    "Jade's Judgment",
    // Koumei
    "Omikuji's Fortune",
    // Limbo
    'Rift Haven', 'Rift Torrent', 'Cataclysmic Continuum',
    // Loki
    'Savior Decoy', 'Hushed Invisibility', 'Safeguard Switch', 'Irradiating Disarm', 'Damage Decoy',
    // Mirage
    'Hall of Malevolence', 'Explosive Legerdemain', 'Total Eclipse',
    // Nyx
    'Mind Freak', 'Pacifying Bolts', 'Chaos Sphere', 'Assimilate',
    // Protea
    'Repair Dispensary', 'Temporal Erosion', 'Temporal Artillery',
    // Styanax
    'Axios Javelineers', 'Tharros Lethality', 'Intrepid Stand',
    // Volt
    'Shock Trooper', 'Shocking Speed', 'Transistor Shield', 'Capacitance',
    // Wukong
    'Celestial Stomp', 'Enveloping Cloud', 'Primal Rage',
  ],
  'cephalon suda': [
    // Banshee
    'Sonic Fracture', 'Resonance', 'Savage Silence', 'Resonating Quake',
    // Caliban
    'Razor Mortar',
    // Chroma
    'Everlasting Ward', 'Guardian Armor', 'Vexing Retaliation', 'Guided Effigy',
    // Frost
    'Biting Frost', 'Freeze Force', 'Ice Wave Impedance', 'Chilling Globe', 'Icy Avalanche',
    // Hildryn
    'Balefire Surge', 'Blazing Pillage', 'Aegis Gale',
    // Hydroid
    'Viral Tempest', 'Tidal Impunity', 'Rousing Plunder', 'Pilfering Swarm',
    // Ivara
    'Empowered Quiver', 'Piercing Navigator', 'Infiltrate', 'Concentrated Arrow',
    // Limbo
    'Rift Haven', 'Rift Torrent', 'Cataclysmic Continuum',
    // Mirage
    'Hall of Malevolence', 'Explosive Legerdemain', 'Total Eclipse',
    // Nezha
    'Pyroclastic Flow', 'Reaping Chakram', 'Safeguard', 'Controlled Slide', 'Divine Retribution',
    // Nova
    'Neutron Star', 'Antimatter Absorb', 'Escape Velocity', 'Molecular Fission',
    // Octavia
    'Partitioned Mallet', 'Conductor',
    // Qorvex
    'Wrecking Wall', 'Fused Crucible',
    // Revenant
    'Thrall Pact', 'Mesmer Shield', 'Blinding Reave',
    // Sevagoth
    'Shadow Haze', 'Dark Propagation',
    // Vauban
    'Tesla Bank', 'Photon Repeater', 'Enduring Bastille',
    // Wisp
    'Fused Reservoir', 'Critical Surge', 'Cataclysmic Gate',
    // Xaku
    'Vampiric Grasp', 'The Relentless Lost', 'Untime Rift',
    // Yareli
    'Merulina Guardian', 'Loyal Merulina', 'Surging Blades',
  ],
  'red veil': [
    // Ash
    'Seeking Shuriken', 'Smoke Shadow', 'Teleport Rush', 'Rising Storm',
    // Atlas
    'Rubble Heap', 'Path of Statues', 'Tectonic Fracture', 'Ore Gaze', 'Titanic Rumbler',
    // Citrine
    'Prismatic Companion', 'Recrystalize',
    // Dagath
    'Spectral Spirit',
    // Ember
    'Fireball Frenzy', 'Immolated Radiance', 'Healing Flame', 'Exothermic',
    // Excalibur Umbra
    "Warrior's Rest",
    // Garuda
    'Dread Ward', 'Blood Forge', 'Blending Talons',
    // Grendel
    'Gourmand', 'Hearty Nourishment', 'Catapult', 'Gastro',
    // Harrow
    'Tribunal', 'Warding Thurible', 'Lasting Covenant',
    // Jade
    "Jade's Judgment",
    // Khora
    'Accumulating Whipclaw', 'Venari Bodyguard', 'Pilfering Strangledome',
    // Lavos
    'Valence Formation', 'Swift Bite', 'Lingering Transmutation',
    // Loki
    'Savior Decoy', 'Hushed Invisibility', 'Safeguard Switch', 'Irradiating Disarm', 'Damage Decoy',
    // Mesa
    "Ballistic Bullseye", 'Muzzle Flash', 'Staggering Shield', "Mesa's Waltz",
    // Nekros
    'Soul Survivor', 'Creeping Terrify', 'Despoil', 'Shield of Shadows',
    // Saryn
    'Revealing Spores', 'Venom Dose', 'Regenerative Molt', 'Contagion Cloud',
    // Titania
    'Spellbound Harvest', 'Beguiling Lantern', 'Razorwing Blitz', 'Ironclad Flight',
    // Volt
    'Shock Trooper', 'Shocking Speed', 'Transistor Shield', 'Capacitance',
    // Voruna
    'Prey of Dynar', "Ulfrun's Endurance",
  ],
};

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

// Get syndicate mods (sold by syndicate - check drops field, fallback to static mapping)
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
  
  // Try filtering mods by drops location containing the syndicate name
  let syndicateMods = mods.filter(mod => {
    if (!mod.drops || !Array.isArray(mod.drops)) return false;
    
    return mod.drops.some(drop => {
      const location = (drop.location || '').toLowerCase();
      return location.includes(searchName);
    });
  });
  
  // Fallback to static mapping if drops data is unavailable
  if (syndicateMods.length === 0 && syndicateAugments[searchName]) {
    const augmentNames = syndicateAugments[searchName].map(n => n.toLowerCase());
    syndicateMods = mods.filter(mod => 
      mod.name && augmentNames.includes(mod.name.toLowerCase())
    );
    console.log(`Used static mapping for ${searchName}: found ${syndicateMods.length} mods`);
  }
  
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
