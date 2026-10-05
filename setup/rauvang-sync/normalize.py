"""Turn the raw rauvang.com crawl into one dated catalog snapshot.

Input (captured 2026-09-29 with crawl-models.js and crawl-flat.js, run in a
Playwright page on rauvang.com):
  raw-models-2026-09-29.json  iPhone, iPad, Mac, Watch: model -> config pages,
                              each config page listing "Colour / Region - price"
  raw-flat-2026-09-29.json    AirPods and accessory list pages, one price each

Output: snapshot-2026-09-29.json, read by generate_migration.py.

Rules (no value is guessed):
  - One option row becomes one variant: colour, region, price in VND.
  - A missing or zero price is stored as null and shown as "Contact for price".
  - A config page with no option rows becomes one variant priced from the
    page's own price element, or null when it shows none.
  - A row that does not parse is reported by name and skipped, never dropped
    silently.
  - Stock is not published by rauvang. At the user's request (2026-09-25) each
    SKU gets a varied demo quantity, 3 to 40, derived from its SKU so reruns
    give the same number. It is demo data, not rauvang data.
"""
import json
import re
import sys
import unicodedata
import zlib
from pathlib import Path

HERE = Path(__file__).parent
DATE = "2026-09-29"

# Our categories that take rauvang's model list as-is.
SYNCED = ["iphone", "ipad", "mac", "watch"]
# rauvang puts these link tiles in every model row; they are list pages, not models.
NOT_MODELS = {"Airpods", "Accessories"}

# Products we keep in AirPods, Accessories, and TV & Home, with the rauvang
# item each one was matched to by name. None means rauvang does not sell it,
# so it becomes "Contact for price". Colour lists come from rauvang's own
# separate per-colour listings (AirPods Max 2 is sold as five items).
FLAT_MATCHES = {
    "airpods-5": ["phu-kien-san-pham/1219-airpods-5.htm"],
    "airpods-pro-3": ["phu-kien-san-pham/1172-airpods-pro-3-chinh-hang-fullbox-.htm"],
    "airpods-max-2": [
        ("Midnight", "phu-kien-san-pham/606-airpods-max-2-2026-midnight-chinh-hang-fullbox-.htm"),
        ("Starlight", "phu-kien-san-pham/605-airpods-max-2-2026-starlight-chinh-hang-fullbox-.htm"),
        ("Blue", "phu-kien-san-pham/604-airpods-max-2-2026-blue-chinh-hang-fullbox-.htm"),
        ("Purple", "phu-kien-san-pham/603-airpods-max-2-2026-purple-chinh-hang-fullbox-.htm"),
        ("Orange", "phu-kien-san-pham/602-airpods-max-2-2026-orange-chinh-hang-fullbox-.htm"),
    ],
    "airpods": None,
    "airpods-max-silver": None,
    "magsafe-charger": ["phu-kien-san-pham/599-magsafe-charger-fullbox-chinh-hang-.htm"],
    "magic-keyboard": None,  # resolved by name below: "Apple Magic Keyboard ( Chính hãng , Full Box )"
    "magic-mouse": None,  # resolved by name below: "Magic Mouse USB-C ( Chính hãng , FullBox )"
    "usb-c-digital-av-multiport-adapter": None,  # resolved by name below
    "airtag": None,  # our 1-pack is the original AirTag; handled per variant below
    "magsafe-battery-pack": None,
    "magic-trackpad": None,
    "apple-pencil-pro": None,
    "studio-display": None,
    "homepod-mini": None,
    "apple-tv-4k": None,
}
FLAT_BY_NAME = {
    "magic-keyboard": "Apple Magic Keyboard ( Chính hãng , Full Box )",
    "magic-mouse": "Magic Mouse USB-C ( Chính hãng , FullBox )",
    "usb-c-digital-av-multiport-adapter": "Cáp USB-C Digital AV Multiport Adapter( Chính hãng , Full Box )",
}
# Our AirTag product has a 1-pack and a 4-pack of the original AirTag.
# rauvang sells the original only as a 4-pack.
AIRTAG_4PK = "AirTag - Bộ 4 cái"

ROW = re.compile(r"^(?P<opts>.+?) - (?P<price>\d*)$")


def slugify(text: str) -> str:
    text = text.replace("đ", "d").replace("Đ", "D")
    text = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


def demo_stock(sku: str) -> int:
    return zlib.crc32(sku.encode()) % 38 + 3


def vnd(text):
    if not text:
        return None
    value = int(re.sub(r"[^0-9]", "", text))
    return value or None


def make_sku(*parts) -> str:
    sku = "-".join(slugify(p).upper() for p in parts if p)
    if len(sku) > 80:
        sku = sku[:71] + "-" + format(zlib.crc32(sku.encode()), "08X")
    return sku


def main():
    raw = json.loads((HERE / f"raw-models-{DATE}.json").read_text())
    flat = json.loads((HERE / f"raw-flat-{DATE}.json").read_text())
    problems = []
    out = {"source": "https://rauvang.com", "capturedAt": DATE, "synced": {}, "kept": {}}

    for cat in SYNCED:
        models = []
        for m in raw[cat]:
            if m["model"] in NOT_MODELS:
                continue
            model_slug = slugify(m["model"])
            configs = []
            for v in m["variants"]:
                config_slug = slugify(v["name"])
                rows, seen = [], set()
                for opt in v["options"]:
                    match = ROW.match(opt)
                    if not match:
                        problems.append(f"unparsed row: {m['model']} / {v['name']} / {opt!r}")
                        continue
                    parts = [p.strip() for p in match["opts"].split(" / ")]
                    color, region = (parts + [None])[:2] if len(parts) <= 2 else (None, None)
                    if len(parts) > 2:
                        problems.append(f"more than colour/region: {v['name']} / {opt!r}")
                        continue
                    if (color, region) in seen:
                        problems.append(f"duplicate row kept once: {v['name']} / {opt!r}")
                        continue
                    seen.add((color, region))
                    rows.append({"color": color, "region": region, "price": vnd(match["price"])})
                if not rows:
                    # No colour list: the page shows one price of its own, or
                    # none ("call for best price").
                    rows = [{"color": None, "region": None, "price": vnd(v.get("pagePrice"))}]
                for r in rows:
                    r["sku"] = make_sku(config_slug, r["color"], r["region"])
                    r["stock"] = demo_stock(r["sku"])
                configs.append({"name": v["name"], "slug": config_slug, "rauvangUrl": v["url"], "rows": rows})
            models.append({"name": m["model"], "slug": model_slug, "rauvangUrl": m["url"], "configs": configs})
        out["synced"][cat] = models

    by_url = {i["url"]: i for items in flat.values() for i in items}
    by_name = {i["name"]: i for items in flat.values() for i in items}
    for slug, match in FLAT_MATCHES.items():
        rows = []
        if slug in FLAT_BY_NAME:
            item = by_name.get(FLAT_BY_NAME[slug])
            if item is None:
                problems.append(f"flat item not found by name: {FLAT_BY_NAME[slug]}")
            rows = [{"color": None, "region": None, "price": vnd(item and item["pagePrice"]), "rauvangUrl": item and item["url"]}]
        elif slug == "airtag":
            item = by_name.get(AIRTAG_4PK)
            rows = [
                {"variantSku": "AIRTAG-1PK", "price": None, "rauvangUrl": None},
                {"variantSku": "AIRTAG-4PK", "price": vnd(item and item["pagePrice"]), "rauvangUrl": item and item["url"]},
            ]
        elif match is None:
            rows = [{"color": None, "region": None, "price": None, "rauvangUrl": None}]
        else:
            for entry in match:
                color, url = entry if isinstance(entry, tuple) else (None, entry)
                item = by_url.get(url)
                if item is None:
                    problems.append(f"flat item not found: {url}")
                    continue
                rows.append({"color": color, "region": None, "price": vnd(item["pagePrice"]), "rauvangUrl": url})
        for r in rows:
            r["sku"] = r.pop("variantSku", None) or make_sku(slug, r.get("color"), r.get("region"))
            r["stock"] = demo_stock(r["sku"])
        out["kept"][slug] = rows

    (HERE / f"snapshot-{DATE}.json").write_text(json.dumps(out, ensure_ascii=False, indent=1))

    all_rows = [r for c in out["synced"].values() for m in c for cf in m["configs"] for r in cf["rows"]]
    skus = [r["sku"] for r in all_rows] + [r["sku"] for rows in out["kept"].values() for r in rows]
    dupes = {s for s in skus if skus.count(s) > 1}
    for s in sorted(dupes):
        problems.append(f"duplicate SKU: {s}")
    for cat, models in out["synced"].items():
        print(f"{cat}: {len(models)} models, {sum(len(m['configs']) for m in models)} configs, "
              f"{sum(len(cf['rows']) for m in models for cf in m['configs'])} variants")
    print(f"kept products: {len(out['kept'])}, priced variants: "
          f"{sum(1 for rows in out['kept'].values() for r in rows if r['price'])}")
    print(f"synced variants with a price: {sum(1 for r in all_rows if r['price'])}, contact for price: "
          f"{sum(1 for r in all_rows if r['price'] is None)}")
    print(f"problems: {len(problems)}")
    for p in problems:
        print("  " + p)
    return 1 if dupes else 0


if __name__ == "__main__":
    sys.exit(main())
