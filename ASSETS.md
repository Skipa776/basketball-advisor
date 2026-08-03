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

**None.** No binary image ships today and `wwwroot/img/` does not exist. Row
`D-34` is vacuously green, which is the intended state: the check was in place
before the first asset needed it, not after.

Everything visual in the app is drawn. The favicon, the court backdrop and every
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
