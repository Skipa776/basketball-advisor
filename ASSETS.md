# Shipped assets

Every binary under `src/FantasyBasketball.Api/wwwroot/img/` must have an entry
here naming its source, licence, author, and retrieval date. Row `D-34` fails
the build on a file that does not.

The rule exists because an unattributed binary is a licensing problem that stays
invisible until it is expensive, and this repository is public and MIT licensed.

## Rules for adding one

- **No NBA, team, or identifiable player likeness** unless the photographer has
  released the image under a licence that permits reuse, and that licence is
  recorded below. Wikimedia Commons contributors who publish under CC BY-SA are
  the intended route.
- **Commercial stock is not usable.** Getty, iStock, Shutterstock and similar
  license their catalogue; neither private use nor a small audience makes
  copying lawful, and a public repository publishes whatever it contains.
- **"Found online" is not a licence.** If the terms cannot be named, the asset
  does not ship.
- Prefer drawn SVG in the component. It costs nothing, themes with the tokens,
  and has no licence to track. Every icon in this design system is drawn for
  exactly that reason.

## Assets

The court photo is public domain, so attribution is not legally
required; it is recorded anyway. None shows an identifiable person or a team
mark. Each was resized and recompressed for the web; nothing else was changed.

| File | Used by | Source | Licence | Author | Retrieved |
|---|---|---|---|---|---|
| `img/ball-through-hoop.jpg` | React `/app` sign-in section | [Wikimedia Commons: Basketball through hoop](<https://commons.wikimedia.org/wiki/File:Basketball_through_hoop.jpg>) | Public domain (U.S. Air Force work) | Airman 1st Class Kerelin Molina | 2026-09-22 |

Everything else visual in the app is drawn. The favicon, the court backdrop and every
icon are SVG written against the tokens, so they theme with the palette and
carry no attribution burden. The one self-hosted binary, the Anton display face
under `wwwroot/fonts/`, is a typeface rather than imagery; it ships under the SIL
OFL and `PRODUCT.md` records the commitment to self-host it.

### Removed: `img/player-placeholder.jpg`

A grey silhouette avatar, added on 2026-07-31 for `PlayerAvatar`'s no-photo
state and removed on 2026-08-03 without ever having been wired up. Recorded here
rather than deleted quietly, because both reasons apply to the next one somebody
adds:

- **Its licence could not be named.** It arrived without provenance and has the
  visual signature and the square dimensions of a commercial stock avatar. An
  asset whose terms cannot be stated fails the third rule above, and this
  repository is public.
- **Nothing referenced it.** `PlayerAvatar` already draws initials when no
  portrait is set, and initials carry more than a silhouette does — they
  identify the row. The file was shipped weight and legal exposure buying
  nothing.

When a portrait set exists, `PlayerAvatar` takes an `ImageUrl` and falls back to
initials per player, so portraits can land one at a time as their licences are
confirmed. Each one gets an entry here.

### Player photos — 2026-09-23

Owner-requested headshots for the landing strip. Each is the lead image of the
player's English Wikipedia article, hosted on Wikimedia Commons, with its licence
read from the Commons API (`LicenseShortName`) and accepted only if CC0, public
domain, CC BY or CC BY-SA. **CC BY and CC BY-SA require attribution**: the landing
page links to these credits, and the files are the 400 px Commons thumbnails,
otherwise unmodified. No NBA, team or league-hosted image is used.

| File | Used by | Source | Licence | Author | Retrieved |
|---|---|---|---|---|---|
| `img/players/nikola-jokic.jpg` | React landing player strip | [Wikimedia Commons: Nikola Jokic free throw (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Nikola_Jokic_free_throw_(cropped).jpg>) | CC BY-SA 2.0 | All-Pro Reels | 2026-09-23 |
| `img/players/shai-gilgeous-alexander.jpg` | React landing player strip | [Wikimedia Commons: 2023-08-09 Deutschland gegen Kanada (Basketball-Länderspiel) by Sandro Halank–109.jpg](<https://commons.wikimedia.org/wiki/File:2023-08-09_Deutschland_gegen_Kanada_(Basketball-L%C3%A4nderspiel)_by_Sandro_Halank%E2%80%93109.jpg>) | CC BY-SA 4.0 | Sandro Halank, Wikimedia Commons | 2026-09-23 |
| `img/players/victor-wembanyama.jpg` | React landing player strip | [Wikimedia Commons: Victor Wembanyama San Antonio Spurs 2024.jpg](<https://commons.wikimedia.org/wiki/File:Victor_Wembanyama_San_Antonio_Spurs_2024.jpg>) | CC BY 4.0 | Frenchieinportland | 2026-09-23 |
| `img/players/luka-doncic.jpg` | React landing player strip | [Wikimedia Commons: Luka Dončić and Marines, 2026 (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Luka_Don%C4%8Di%C4%87_and_Marines,_2026_(cropped).jpg>) | Public domain | U.S. Embassy Ljubljana | 2026-09-23 |
| `img/players/giannis-antetokounmpo.jpg` | React landing player strip | [Wikimedia Commons: Giannis Antetokounmpo (51915153421) (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Giannis_Antetokounmpo_(51915153421)_(cropped).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/anthony-davis.jpg` | React landing player strip | [Wikimedia Commons: Anthony Davis pre-game (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Anthony_Davis_pre-game_(cropped).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/cade-cunningham.jpg` | React landing player strip | [Wikimedia Commons: 1 cade cunningham 2024.jpg](<https://commons.wikimedia.org/wiki/File:1_cade_cunningham_2024.jpg>) | CC BY-SA 4.0 | Chensiyuan | 2026-09-23 |
| `img/players/anthony-edwards.jpg` | React landing player strip | [Wikimedia Commons: Anthony Edwards Argentina v Egypt 7 July 2026-069 (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Anthony_Edwards_Argentina_v_Egypt_7_July_2026-069_(cropped).jpg>) | CC BY-SA 4.0 | Bryan Berlin | 2026-09-23 |
| `img/players/karl-anthony-towns.jpg` | React landing player strip | [Wikimedia Commons: Karl-Anthony Towns (51914283512) (cropped) (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Karl-Anthony_Towns_(51914283512)_(cropped)_(cropped).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/domantas-sabonis.jpg` | React landing player strip | [Wikimedia Commons: Domantas Sabonis by Augustas Didzgalvis (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Domantas_Sabonis_by_Augustas_Didzgalvis_(cropped).jpg>) | CC BY-SA 4.0 | Augustas Didžgalvis | 2026-09-23 |
| `img/players/trae-young.jpg` | React landing player strip | [Wikimedia Commons: Trae Young (2022 All-Star Weekend) (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Trae_Young_(2022_All-Star_Weekend)_(cropped).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/lebron-james.jpg` | React landing player strip | [Wikimedia Commons: LeBron James (51959977144) (cropped2).jpg](<https://commons.wikimedia.org/wiki/File:LeBron_James_(51959977144)_(cropped2).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/stephen-curry.jpg` | React landing player strip | [Wikimedia Commons: Stephen Curry, Olympic Games 2024 (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Stephen_Curry,_Olympic_Games_2024_(cropped).jpg>) | CC BY-SA 4.0 | Clément Bardot | 2026-09-23 |
| `img/players/kevin-durant.jpg` | React landing player strip | [Wikimedia Commons: Kevin Durant, Paris 2024 (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Kevin_Durant,_Paris_2024_(cropped).jpg>) | CC BY-SA 4.0 | Clément Bardot | 2026-09-23 |
| `img/players/devin-booker.jpg` | React landing player strip | [Wikimedia Commons: Devin Booker, Olympic Games 2024 (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Devin_Booker,_Olympic_Games_2024_(cropped).jpg>) | CC BY-SA 4.0 | Clément Bardot | 2026-09-23 |
| `img/players/donovan-mitchell.jpg` | React landing player strip | [Wikimedia Commons: Donovan Mitchell Pregame.jpg](<https://commons.wikimedia.org/wiki/File:Donovan_Mitchell_Pregame.jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/jalen-brunson.jpg` | React landing player strip | [Wikimedia Commons: Jalen Brunson 2023 (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Jalen_Brunson_2023_(cropped).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/tyrese-maxey.jpg` | React landing player strip | [Wikimedia Commons: 1 tyrese maxey 2026.jpg](<https://commons.wikimedia.org/wiki/File:1_tyrese_maxey_2026.jpg>) | CC BY-SA 4.0 | Chensiyuan | 2026-09-23 |
| `img/players/james-harden.jpg` | React landing player strip | [Wikimedia Commons: Harden dribbling midcourt, Cavaliers vs Nets on January 17, 2022 (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Harden_dribbling_midcourt,_Cavaliers_vs_Nets_on_January_17,_2022_(cropped).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/jayson-tatum.jpg` | React landing player strip | [Wikimedia Commons: Celtics at Wizards 2024-12-044 (cropped 2).jpg](<https://commons.wikimedia.org/wiki/File:Celtics_at_Wizards_2024-12-044_(cropped_2).jpg>) | CC BY-SA 4.0 | Hameltion | 2026-09-23 |
| `img/players/tyrese-haliburton.jpg` | React landing player strip | [Wikimedia Commons: 1 tyrese haliburton 2025 (cropped 2).jpg](<https://commons.wikimedia.org/wiki/File:1_tyrese_haliburton_2025_(cropped_2).jpg>) | CC BY-SA 4.0 | Chensiyuan | 2026-09-23 |
| `img/players/jalen-johnson.jpg` | React landing player strip | [Wikimedia Commons: Jalen-Johnson.jpg](<https://commons.wikimedia.org/wiki/File:Jalen-Johnson.jpg>) | CC BY 4.0 | Unknown authorUnknown author | 2026-09-23 |
| `img/players/scottie-barnes.jpg` | React landing player strip | [Wikimedia Commons: Scottie Barnes, Wizards vs Raptors on October 12, 2021.jpg](<https://commons.wikimedia.org/wiki/File:Scottie_Barnes,_Wizards_vs_Raptors_on_October_12,_2021.jpg>) | CC BY-SA 2.0 | All-Pro Reels from District of Columbia, USA | 2026-09-23 |
| `img/players/amen-thompson.jpg` | React landing player strip | [Wikimedia Commons: Amen Thompson 2026.jpg](<https://commons.wikimedia.org/wiki/File:Amen_Thompson_2026.jpg>) | CC BY 4.0 | Pikraken | 2026-09-23 |
| `img/players/alperen-sengun.jpg` | React landing player strip | [Wikimedia Commons: Alperen Şengün 23 Türkiye 20250823 (1).jpg](<https://commons.wikimedia.org/wiki/File:Alperen_%C5%9Eeng%C3%BCn_23_T%C3%BCrkiye_20250823_(1).jpg>) | CC BY 4.0 | Zafer | 2026-09-23 |
| `img/players/evan-mobley.jpg` | React landing player strip | [Wikimedia Commons: Evan Mobley (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Evan_Mobley_(cropped).jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/chet-holmgren.jpg` | React landing player strip | [Wikimedia Commons: Chet Holmgren.jpg](<https://commons.wikimedia.org/wiki/File:Chet_Holmgren.jpg>) | CC BY 2.0 | Steve Cheng, Bruin Report Online | 2026-09-23 |
| `img/players/jamal-murray.jpg` | React landing player strip | [Wikimedia Commons: Jamal Murray free throw (cropped).jpg](<https://commons.wikimedia.org/wiki/File:Jamal_Murray_free_throw_(cropped).jpg>) | CC BY-SA 2.0 | All-Pro Reels | 2026-09-23 |
| `img/players/josh-giddey.jpg` | React landing player strip | [Wikimedia Commons: Josh Giddey 2022.jpg](<https://commons.wikimedia.org/wiki/File:Josh_Giddey_2022.jpg>) | CC BY 2.0 | Erik Drost | 2026-09-23 |
| `img/players/paolo-banchero.png` | React landing player strip | [Wikimedia Commons: Paolo Banchero.png](<https://commons.wikimedia.org/wiki/File:Paolo_Banchero.png>) | CC BY 3.0 | Al Ward | 2026-09-23 |
