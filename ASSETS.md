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

### `img/player-placeholder.jpg`

| | |
|---|---|
| **Used by** | `Components/Design/PlayerAvatar.razor`, when a player has no portrait |
| **Source** | Supplied by the repository owner at `resources/nophotoforplayer.jpg` |
| **Author** | Unknown |
| **Licence** | **Unconfirmed** |
| **Retrieved** | 2026-07-31 |

> **This entry is incomplete and is the one thing in this file that does not
> meet its own standard.** The image was provided without provenance. It has the
> visual signature of a commercial stock avatar, and if it came from a stock
> library it cannot ship in a public repository.
>
> **Before this repository is published or released, either confirm the licence
> and complete this entry, or delete the file.** `PlayerAvatar` already falls
> back to drawn initials when no image is set, so removing it costs one line and
> breaks nothing — the component treats a missing portrait as an ordinary state,
> not an error.
>
> A drawn SVG silhouette would remove the question entirely and would theme with
> the palette, which this neutral grey does not.
