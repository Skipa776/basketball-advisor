// Free-licensed Wikimedia Commons headshots for the landing strip; credits mirror ASSETS.md.
export type PlayerPhoto = { name: string; file: string; author: string; license: string; source: string };

export const PLAYER_PHOTOS: PlayerPhoto[] = [
  { name: "Nikola Jokić", file: "nikola-jokic.jpg", author: "All-Pro Reels", license: "CC BY-SA 2.0", source: "https://commons.wikimedia.org/wiki/File:Nikola_Jokic_free_throw_(cropped).jpg" },
  { name: "Shai Gilgeous-Alexander", file: "shai-gilgeous-alexander.jpg", author: "Sandro Halank, Wikimedia Commons", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:2023-08-09_Deutschland_gegen_Kanada_(Basketball-L%C3%A4nderspiel)_by_Sandro_Halank%E2%80%93109.jpg" },
  { name: "Victor Wembanyama", file: "victor-wembanyama.jpg", author: "Frenchieinportland", license: "CC BY 4.0", source: "https://commons.wikimedia.org/wiki/File:Victor_Wembanyama_San_Antonio_Spurs_2024.jpg" },
  { name: "Luka Dončić", file: "luka-doncic.jpg", author: "U.S. Embassy Ljubljana", license: "Public domain", source: "https://commons.wikimedia.org/wiki/File:Luka_Don%C4%8Di%C4%87_and_Marines,_2026_(cropped).jpg" },
  { name: "Giannis Antetokounmpo", file: "giannis-antetokounmpo.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Giannis_Antetokounmpo_(51915153421)_(cropped).jpg" },
  { name: "Anthony Davis", file: "anthony-davis.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Anthony_Davis_pre-game_(cropped).jpg" },
  { name: "Cade Cunningham", file: "cade-cunningham.jpg", author: "Chensiyuan", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:1_cade_cunningham_2024.jpg" },
  { name: "Anthony Edwards", file: "anthony-edwards.jpg", author: "Bryan Berlin", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:Anthony_Edwards_Argentina_v_Egypt_7_July_2026-069_(cropped).jpg" },
  { name: "Karl-Anthony Towns", file: "karl-anthony-towns.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Karl-Anthony_Towns_(51914283512)_(cropped)_(cropped).jpg" },
  { name: "Domantas Sabonis", file: "domantas-sabonis.jpg", author: "Augustas Didžgalvis", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:Domantas_Sabonis_by_Augustas_Didzgalvis_(cropped).jpg" },
  { name: "Trae Young", file: "trae-young.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Trae_Young_(2022_All-Star_Weekend)_(cropped).jpg" },
  { name: "LeBron James", file: "lebron-james.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:LeBron_James_(51959977144)_(cropped2).jpg" },
  { name: "Stephen Curry", file: "stephen-curry.jpg", author: "Clément Bardot", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:Stephen_Curry,_Olympic_Games_2024_(cropped).jpg" },
  { name: "Kevin Durant", file: "kevin-durant.jpg", author: "Clément Bardot", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:Kevin_Durant,_Paris_2024_(cropped).jpg" },
  { name: "Devin Booker", file: "devin-booker.jpg", author: "Clément Bardot", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:Devin_Booker,_Olympic_Games_2024_(cropped).jpg" },
  { name: "Donovan Mitchell", file: "donovan-mitchell.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Donovan_Mitchell_Pregame.jpg" },
  { name: "Jalen Brunson", file: "jalen-brunson.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Jalen_Brunson_2023_(cropped).jpg" },
  { name: "Tyrese Maxey", file: "tyrese-maxey.jpg", author: "Chensiyuan", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:1_tyrese_maxey_2026.jpg" },
  { name: "James Harden", file: "james-harden.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Harden_dribbling_midcourt,_Cavaliers_vs_Nets_on_January_17,_2022_(cropped).jpg" },
  { name: "Jayson Tatum", file: "jayson-tatum.jpg", author: "Hameltion", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:Celtics_at_Wizards_2024-12-044_(cropped_2).jpg" },
  { name: "Tyrese Haliburton", file: "tyrese-haliburton.jpg", author: "Chensiyuan", license: "CC BY-SA 4.0", source: "https://commons.wikimedia.org/wiki/File:1_tyrese_haliburton_2025_(cropped_2).jpg" },
  { name: "Jalen Johnson", file: "jalen-johnson.jpg", author: "Unknown authorUnknown author", license: "CC BY 4.0", source: "https://commons.wikimedia.org/wiki/File:Jalen-Johnson.jpg" },
  { name: "Scottie Barnes", file: "scottie-barnes.jpg", author: "All-Pro Reels from District of Columbia, USA", license: "CC BY-SA 2.0", source: "https://commons.wikimedia.org/wiki/File:Scottie_Barnes,_Wizards_vs_Raptors_on_October_12,_2021.jpg" },
  { name: "Amen Thompson", file: "amen-thompson.jpg", author: "Pikraken", license: "CC BY 4.0", source: "https://commons.wikimedia.org/wiki/File:Amen_Thompson_2026.jpg" },
  { name: "Alperen Şengün", file: "alperen-sengun.jpg", author: "Zafer", license: "CC BY 4.0", source: "https://commons.wikimedia.org/wiki/File:Alperen_%C5%9Eeng%C3%BCn_23_T%C3%BCrkiye_20250823_(1).jpg" },
  { name: "Evan Mobley", file: "evan-mobley.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Evan_Mobley_(cropped).jpg" },
  { name: "Chet Holmgren", file: "chet-holmgren.jpg", author: "Steve Cheng, Bruin Report Online", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Chet_Holmgren.jpg" },
  { name: "Jamal Murray", file: "jamal-murray.jpg", author: "All-Pro Reels", license: "CC BY-SA 2.0", source: "https://commons.wikimedia.org/wiki/File:Jamal_Murray_free_throw_(cropped).jpg" },
  { name: "Josh Giddey", file: "josh-giddey.jpg", author: "Erik Drost", license: "CC BY 2.0", source: "https://commons.wikimedia.org/wiki/File:Josh_Giddey_2022.jpg" },
  { name: "Paolo Banchero", file: "paolo-banchero.png", author: "Al Ward", license: "CC BY 3.0", source: "https://commons.wikimedia.org/wiki/File:Paolo_Banchero.png" },
];

const key = (name: string) => name.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
const byName = new Map(PLAYER_PHOTOS.map(photo => [key(photo.name), photo]));
export const photoFor = (name: string) => byName.get(key(name));
