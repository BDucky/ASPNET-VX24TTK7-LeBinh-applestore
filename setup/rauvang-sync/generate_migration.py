"""Write the EF Core data migration that loads snapshot-<date>.json.

Usage:
  python3 setup/rauvang-sync/generate_migration.py <migration .cs path> <dev database path>

The database is read only to learn the Ids and statuses the earlier seed
migrations left behind, so the generated Up() can reuse or deactivate them by
Id and Down() can restore them exactly. Nothing is written to the database.

Rules:
  - iPhone, iPad, Mac, Watch take rauvang's model list. A rauvang model whose
    slug matches an active product in the same category reuses that row (and
    so keeps its photo); every other active product there is deactivated.
  - AirPods, Accessories, TV & Home keep their products. Their old USD
    variants are deactivated and the snapshot's VND variants are added.
    Products listed in RETIRED are deactivated instead.
  - Every active product must end up reused, kept, or retired; anything left
    over stops the script, so no product silently keeps a stale USD price.
  - photos.json can also replace the photo of a reused or kept product, or
    remove it with null (a photo of a different model, or an unlicensed
    hotlink). Down() puts the original image rows back with their Ids.
"""
import json
import re
import sqlite3
import sys
from pathlib import Path

HERE = Path(__file__).parent
DATE = "2026-09-29"
SYNCED = {"iphone": 1, "mac": 2, "ipad": 3, "watch": 4}
KEPT_CATEGORIES = {5, 6, 7}
# rauvang sells official bands as a Watch model ("Dây Apple Watch"), so our
# separate accessory listing for one band is retired rather than repriced.
RETIRED = {"apple-watch-sport-band"}
OPTION_TYPES = [(1, "config"), (2, "color"), (3, "region")]


def cs(value):
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, float):
        return f"{value}m"
    return '"' + str(value).replace("\\", "\\\\").replace('"', '\\"') + '"'


def money(value):
    return "null" if value is None else f"{value}.00m"


def insert(table, columns, rows):
    if not rows:
        return ""
    body = "\n".join("                    { " + ", ".join(r) + " }," for r in rows)
    cols = ", ".join(f'"{c}"' for c in columns)
    return (f"            migrationBuilder.InsertData(\n"
            f"                table: \"{table}\",\n"
            f"                columns: new[] {{ {cols} }},\n"
            f"                values: new object[,]\n                {{\n{body}\n                }});\n\n")


def id_list(ids):
    return ", ".join(str(i) for i in sorted(ids))


def main(migration_path, db_path):
    snap = json.loads((HERE / f"snapshot-{DATE}.json").read_text())
    photos = {k: v for k, v in json.loads((HERE / "photos.json").read_text()).items() if not k.startswith("_")}
    db = sqlite3.connect(db_path)
    products = [dict(zip(("Id", "CategoryId", "Slug", "Name", "Status", "SortOrder"), r)) for r in
                db.execute("select Id, CategoryId, Slug, Name, Status, SortOrder from Products")]
    variants = [dict(zip(("Id", "ProductId", "Status"), r)) for r in db.execute("select Id, ProductId, Status from ProductVariants")]
    if db.execute("select count(*) from OptionTypes").fetchone()[0]:
        sys.exit("OptionTypes already has rows; this generator assumes it is the first to add options.")
    next_product = max(p["Id"] for p in products) + 1
    next_variant = max(v["Id"] for v in variants) + 1
    next_image = db.execute("select max(Id) from ProductImages").fetchone()[0] + 1
    old_images = [dict(zip(("Id", "ProductId", "VariantId", "ImageUrl", "SortOrder"), r))
                  for r in db.execute("select Id, ProductId, VariantId, ImageUrl, SortOrder from ProductImages")]
    removed_images = []

    def replace_photo(pid, slug):
        """Swap an existing product's photo for photos.json's entry, if it has one."""
        nonlocal next_image
        if slug not in photos:
            return
        removed_images.extend(i for i in old_images if i["ProductId"] == pid)
        if photos[slug]:
            new_images.append([cs(next_image), cs(pid), "null", cs(photos[slug]), "0"])
            next_image += 1

    active = {p["Id"]: p for p in products if p["Status"]}
    handled = set()
    reused, new_products, deactivate_products = [], [], set()
    new_variants, variant_options, new_images = [], [], []
    deactivate_variants = set()
    option_values = {}

    def value_id(type_id, value):
        key = (type_id, value)
        if key not in option_values:
            option_values[key] = len(option_values) + 1
        return option_values[key]

    def add_variant(product_id, row, config=None):
        nonlocal next_variant
        vid = next_variant
        next_variant += 1
        new_variants.append([cs(vid), cs(product_id), cs(row["sku"]), money(row["price"]), cs(row["stock"]), "true", "SeededAt", "SeededAt"])
        for type_id, value in ((1, config), (2, row.get("color")), (3, row.get("region"))):
            if value:
                variant_options.append([cs(vid), cs(type_id), cs(value_id(type_id, value))])

    for cat, cat_id in SYNCED.items():
        by_slug = {p["Slug"]: p for p in active.values() if p["CategoryId"] == cat_id}
        for order, model in enumerate(snap["synced"][cat]):
            prices = [r["price"] for c in model["configs"] for r in c["rows"] if r["price"]]
            base = min(prices) if prices else None  # null = no price anywhere ("Contact for price")
            existing = by_slug.get(model["slug"])
            if existing:
                pid = existing["Id"]
                reused.append((pid, model["name"], order, existing["Name"], existing["SortOrder"]))
                replace_photo(pid, model["slug"])
                deactivate_variants.update(v["Id"] for v in variants if v["ProductId"] == pid and v["Status"])
                handled.add(pid)
            else:
                pid = next_product
                next_product += 1
                new_products.append([cs(pid), cs(cat_id), cs(model["name"]), cs(model["slug"]), "null", money(base), "true", cs(order), "SeededAt", "SeededAt"])
                if photos.get(model["slug"]):
                    new_images.append([cs(next_image), cs(pid), "null", cs(photos[model["slug"]]), "0"])
                    next_image += 1
            for config in model["configs"]:
                for row in config["rows"]:
                    add_variant(pid, row, config["name"])
        for p in by_slug.values():
            if p["Id"] not in handled:
                deactivate_products.add(p["Id"])
                handled.add(p["Id"])

    kept_by_slug = {p["Slug"]: p for p in active.values() if p["CategoryId"] in KEPT_CATEGORIES}
    for slug, rows in snap["kept"].items():
        p = kept_by_slug.get(slug)
        if p is None:
            sys.exit(f"kept product not found or inactive: {slug}")
        deactivate_variants.update(v["Id"] for v in variants if v["ProductId"] == p["Id"] and v["Status"])
        replace_photo(p["Id"], slug)
        for row in rows:
            add_variant(p["Id"], row)
        handled.add(p["Id"])
    for slug in RETIRED:
        p = kept_by_slug[slug]
        deactivate_products.add(p["Id"])
        handled.add(p["Id"])

    leftover = [p["Slug"] for pid, p in active.items() if pid not in handled]
    if leftover:
        sys.exit(f"active products not accounted for: {leftover}")

    first_product = max(p["Id"] for p in products) + 1
    first_variant = max(v["Id"] for v in variants) + 1
    first_image = db.execute("select max(Id) from ProductImages").fetchone()[0] + 1

    up = []
    up.append(insert("OptionTypes", ["Id", "Code"], [[cs(i), cs(c)] for i, c in OPTION_TYPES]))
    up.append(insert("OptionValues", ["Id", "OptionTypeId", "Value"],
                     [[cs(vid), cs(t), cs(v)] for (t, v), vid in sorted(option_values.items(), key=lambda kv: kv[1])]))
    up.append(f'            migrationBuilder.Sql("UPDATE Products SET Status = 0 WHERE Id IN ({id_list(deactivate_products)});");\n')
    up.append(f'            migrationBuilder.Sql("UPDATE ProductVariants SET Status = 0 WHERE Id IN ({id_list(deactivate_variants)});");\n')
    for pid, name, order, _, _ in reused:
        up.append(f'            migrationBuilder.Sql("UPDATE Products SET Name = {sql_str(name)}, SortOrder = {order} WHERE Id = {pid};");\n')
    kept_orders = sorted((p for p in active.values() if p["CategoryId"] in KEPT_CATEGORIES and p["Slug"] not in RETIRED), key=lambda p: p["Id"])
    for order, p in enumerate(kept_orders):
        up.append(f'            migrationBuilder.Sql("UPDATE Products SET SortOrder = {order} WHERE Id = {p["Id"]};");\n')
    up.append("\n")
    up.append(insert("Products", ["Id", "CategoryId", "Name", "Slug", "Description", "BasePrice", "Status", "SortOrder", "CreatedAt", "UpdatedAt"], new_products))
    up.append(insert("ProductVariants", ["Id", "ProductId", "SKU", "Price", "StockQty", "Status", "CreatedAt", "UpdatedAt"], new_variants))
    up.append(insert("VariantOptions", ["VariantId", "OptionTypeId", "OptionValueId"], variant_options))
    if removed_images:
        up.append(f'            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id IN ({id_list(i["Id"] for i in removed_images)});");\n\n')
    up.append(insert("ProductImages", ["Id", "ProductId", "VariantId", "ImageUrl", "SortOrder"], new_images))

    down = [
        f'            migrationBuilder.Sql("DELETE FROM VariantOptions WHERE VariantId >= {first_variant};");\n',
        f'            migrationBuilder.Sql("DELETE FROM ProductImages WHERE Id >= {first_image};");\n',
        f'            migrationBuilder.Sql("DELETE FROM ProductVariants WHERE Id >= {first_variant};");\n',
        f'            migrationBuilder.Sql("DELETE FROM Products WHERE Id >= {first_product};");\n',
        '            migrationBuilder.Sql("DELETE FROM OptionValues;");\n',
        '            migrationBuilder.Sql("DELETE FROM OptionTypes;");\n',
        f'            migrationBuilder.Sql("UPDATE Products SET Status = 1 WHERE Id IN ({id_list(deactivate_products)});");\n',
        f'            migrationBuilder.Sql("UPDATE ProductVariants SET Status = 1 WHERE Id IN ({id_list(deactivate_variants)});");\n',
    ]
    if removed_images:
        down.append(insert("ProductImages", ["Id", "ProductId", "VariantId", "ImageUrl", "SortOrder"],
                           [[cs(i["Id"]), cs(i["ProductId"]), cs(i["VariantId"]), cs(i["ImageUrl"]), cs(i["SortOrder"])] for i in removed_images]))
    for pid, _, _, old_name, old_order in reused:
        down.append(f'            migrationBuilder.Sql("UPDATE Products SET Name = {sql_str(old_name)}, SortOrder = {old_order} WHERE Id = {pid};");\n')
    for p in kept_orders:
        down.append(f'            migrationBuilder.Sql("UPDATE Products SET SortOrder = {p["SortOrder"]} WHERE Id = {p["Id"]};");\n')

    source = Path(migration_path).read_text()
    class_name = re.search(r"public partial class (\w+) : Migration", source).group(1)
    body = f'''using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppleStore.Infrastructure.Migrations
{{
    // Generated by setup/rauvang-sync/generate_migration.py from
    // setup/rauvang-sync/snapshot-{DATE}.json (rauvang.com, captured {DATE}).
    // Do not edit by hand: change the snapshot or the generator and rerun it.
    // Prices are VND. Stock quantities are demo data chosen at the user's
    // request, not rauvang data (rauvang publishes none).
    /// <inheritdoc />
    public partial class {class_name} : Migration
    {{
        private static readonly DateTime SeededAt = new(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {{
{"".join(up).rstrip()}
        }}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {{
{"".join(down).rstrip()}
        }}
    }}
}}
'''
    Path(migration_path).write_text(body)
    print(f"reused {len(reused)} products, added {len(new_products)}, deactivated {len(deactivate_products)} products "
          f"and {len(deactivate_variants)} old variants; added {len(new_variants)} variants, {len(option_values)} option values, "
          f"{len(variant_options)} variant options, {len(new_images)} images")
    with_photo = set()
    for row in new_images:
        with_photo.add(int(row[1]))
    kept_ids = {i["ProductId"] for i in old_images} - {i["ProductId"] for i in removed_images}
    all_active = [(m["slug"]) for c in snap["synced"].values() for m in c] + list(snap["kept"])
    print(f"removed {len(removed_images)} old image rows")
    print("products with no photo: " + ", ".join(
        slug for slug in all_active
        if not (photos.get(slug) or (slug not in photos and any(p["Slug"] == slug and p["Status"] and p["Id"] in kept_ids for p in products)))))


def sql_str(text):
    return "'" + text.replace("'", "''").replace('"', '\\"') + "'"


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
