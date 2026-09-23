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

Every photo is CC0 or public domain, so attribution is not legally required; it
is recorded anyway. None shows an identifiable person or a team mark. Each was
resized and recompressed for the web; nothing else was changed.

| File | Used by | Source | Licence | Author | Retrieved |
|---|---|---|---|---|---|
| `img/hoop-sunset.jpg` | React `/app` image strip | [Wikimedia Commons: 2009-365-10 Sunset on the Hoop](https://commons.wikimedia.org/wiki/File:2009-365-10_Sunset_on_the_Hoop_(3186039611).jpg) | CC0 1.0 | cogdogblog (Alan Levine), via Flickr | 2026-09-22 |
| `img/court-aerial.jpg` | React `/app` image strip | [Wikimedia Commons: Espace de basket 2](https://commons.wikimedia.org/wiki/File:Espace_de_basket_2.jpg) | CC0 1.0 | Abdoulayelelewal237 | 2026-09-22 |
| `img/hoop-angular.jpg` | React `/app` image strip | [Wikimedia Commons: Angular Basketball Hoop](https://commons.wikimedia.org/wiki/File:Angular_Basketball_Hoop.jpg) | CC0 1.0 | MarkBuckawicki | 2026-09-22 |
| `img/ball-in-flight.jpg` | React `/app` image strip | [Wikimedia Commons: Basketball hitting a basketball goal](https://commons.wikimedia.org/wiki/File:Basketball_hitting_a_basketball_goal.jpg) | CC0 1.0 | noahsilliman, via Unsplash | 2026-09-22 |
| `img/net-freestanding.jpg` | React `/app` image strip | [Wikimedia Commons: Freestanding basketball net (Unsplash)](https://commons.wikimedia.org/wiki/File:Freestanding_basketball_net_(Unsplash).jpg) | CC0 1.0 | Andy Hu, via Unsplash | 2026-09-22 |
| `img/net-torn.jpg` | React `/app` image strip | [Wikimedia Commons: Basketball hoop](https://commons.wikimedia.org/wiki/File:Basketball_hoop.JPG) | Public domain | HTO | 2026-09-22 |
| `img/ball-through-hoop.jpg` | React `/app` sign-in section | [Wikimedia Commons: Basketball through hoop](https://commons.wikimedia.org/wiki/File:Basketball_through_hoop.jpg) | Public domain (U.S. Air Force work) | Airman 1st Class Kerelin Molina | 2026-09-22 |
| `img/streetball-court.jpg` | React `/app` image strip | [Wikimedia Commons: Streetball court Ornskoldsvik](https://commons.wikimedia.org/wiki/File:Streetball_court_Ornskoldsvik.jpg) | Public domain | Petey21 | 2026-09-22 |

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
