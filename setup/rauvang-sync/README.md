# rauvang.com catalog sync

Loads rauvang.com's iPhone, iPad, Mac, and Watch catalog (models, configuration
cards, colour and region prices in VND) into this app's seed data, and reprices
the AirPods, accessory, and TV & Home products we kept. Captured 2026-09-29.

rauvang changes prices during the day. The snapshot is exact for the moment it
was crawled, not a live feed; rerun the steps below to refresh it.

## Files

| File | What it is |
|---|---|
| `crawl-models.js`, `crawl-flat.js` | Playwright `run-code` scripts, run on a rauvang.com page |
| `raw-models-<date>.json`, `raw-flat-<date>.json` | What the crawl saw, unedited |
| `normalize.py` | Turns the raw crawl into `snapshot-<date>.json` and reports anything it could not parse |
| `photos.json` | Which licensed photo each new model uses (same model only; Wikimedia Commons, credited on /Home/Credits) |
| `generate_migration.py` | Writes the EF Core data migration from the snapshot |
| `verify.py` | Compares the running app with rauvang.com live: model rows, cards, and every colour/region price |

## Rules the scripts follow

- A missing or zero price is stored as null and shown as "Contact for price",
  never as 0.
- rauvang publishes no stock. Each SKU gets a demo quantity from 3 to 40,
  derived from the SKU, chosen at the user's request. It is not rauvang data.
- Models rauvang does not sell are deactivated, not deleted.
- rauvang's photos are not used. A model without a verified Commons photo of
  that exact model shows no photo.

## Refresh

```bash
playwright-cli -s=sync open https://rauvang.com/
playwright-cli -s=sync --raw run-code --filename=setup/rauvang-sync/crawl-models.js > /tmp/raw-models.txt
playwright-cli -s=sync --raw run-code --filename=setup/rauvang-sync/crawl-flat.js > /tmp/raw-flat.txt
# save each result's JSON as raw-*-<date>.json, then:
python3 setup/rauvang-sync/normalize.py
python3 setup/rauvang-sync/generate_migration.py <path to the SyncCatalogWithRauvang .cs> src/AppleStore.Web/AppleStore.db
```

Then apply the migration, start the app on port 5199, and run
`python3 setup/rauvang-sync/verify.py`. It lists every difference; a
difference right after a fresh crawl is a bug, a difference days later is
usually rauvang changing a price.

`generate_migration.py` reads the dev database only to learn the Ids the
earlier seed migrations created. Run it with the database migrated up to the
migration just before `SyncCatalogWithRauvang`.
