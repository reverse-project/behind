# Sport

**Sport** is the module for teams, players and their statistics. It is made for soccer first, but every team, player, season and statistic belongs to a sport, so other sports can be added later without any change.

Everybody can browse **Sport** in the main menu (`#/sport`). You manage the data under **Administration > Sport** (`#/manage/sport`).

| Action | You need |
|--------|----------|
| See the management pages | the `Sport.Read` grant |
| Create and edit anything, upload logos and pictures | the `Sport.Write` grant |
| Delete rows | the `Sport.Delete` grant |
| See, restore and permanently delete deleted rows | the `RawDatabase.Manage` grant (developers) |

Administrators have all the grants by default, users can read.

## Browsing

- **Sports**: a sport lists its positions, stat types and seasons.
- **Teams**: a team has an optional logo, its place and founding year and, per season, its players and its statistics.
- **Players**: a player has a picture, age, position, nationality, height, weight, dominant side and biography. The season history shows a different picture, team, shirt number and statistics for every season.
- **Seasons**: the teams that played in a season, with links to its leaderboards and statistics.
- **Leaderboards**: the best players or teams of a season for every stat type (top 10), and the full ranking of one stat type. Ties share a rank, and stat types where less is better (for example goals against) are ranked ascending.
- **Statistics**: every statistic of a season, optionally of one stat type.

Teams, players, seasons, sports, positions and stat types are links everywhere they appear. Player names follow the language of the page: English shows **first name last name** (Ada Lovelace), Hungarian shows **last name first name** (Lovelace Ada). The search finds a player with the name in either order.

## Managing

The management hub links to eight pages: sports, positions, stat types, seasons, teams, players, season memberships and statistics. Create a sport first (a fresh installation starts with **Soccer**, its positions and its stat types), then seasons, teams and players. Everything that is referenced must belong to the same sport.

- A **season membership** puts a player in a team for a season with a shirt number and the picture used for that season. A player can be in one team per season.
- A **statistic** is one value of a stat type for a player or a team (the stat type decides which) in a season; there is one value per stat type, subject and season.
- A **stat type** cannot change between player and team statistics while statistics use it.

Search looks in the names, the letter bar filters by the first letter and lists are paged. **Delete** only hides a row. Developers switch on the deleted rows, then **Restore** a row or **Delete permanently** to remove it for good, which is refused while another row still uses it. Every change appears in the [audit log](./audit-log.md) and the tables can be edited in the [raw database](./managing-raw-database.md).
