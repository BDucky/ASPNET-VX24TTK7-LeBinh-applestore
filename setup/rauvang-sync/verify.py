"""Compare the running app with rauvang.com, live, for every synced model.

Usage (app running on http://localhost:5199, playwright-cli installed):
  python3 setup/rauvang-sync/verify.py

Checks, all read from both sites at the moment the script runs:
  1. Each category's model row: same models, same order.
  2. Each model page: same cards, same names, same "From" price.
  3. Each configuration page: same colour, region, and price rows.
A rauvang price shown as its phone number, "Giá Tốt Nhất", or 0 counts as
matching our "Contact for price".

rauvang changes prices during the day, so a difference can mean the snapshot
is older than rauvang's latest edit rather than a bug. The output lists every
difference so it can be told apart.
"""
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).parent
APP = "http://localhost:5199"
CATEGORIES = {"iphone": "danh-muc/14-iphone.htm", "ipad": "danh-muc/7-ipad.htm", "mac": "danh-muc/1-mac.htm", "watch": "danh-muc/20-watch.htm"}

SCRIPT = r"""
async page => {
  const models = __MODELS__, configs = __CONFIGS__, cats = __CATS__, app = __APP__;
  await page.goto('https://rauvang.com/', {waitUntil: 'load', timeout: 60000});
  const rv = await page.evaluate(async ({models, configs, cats}) => {
    const doc = async u => new DOMParser().parseFromString(await (await fetch('/' + u)).text(), 'text/html');
    const out = {strips: {}, cards: {}, configs: {}};
    for (const [c, u] of Object.entries(cats)) {
      const d = await doc(u);
      out.strips[c] = [...new Map([...d.querySelectorAll('a[href*="danh-muc-san-pham/"]')].map(a => [a.getAttribute('href'), a.textContent.trim().replace(/\s+/g, ' ')])).values()]
        .filter(n => n && n !== 'Airpods' && n !== 'Accessories');
    }
    for (const [c, slug, u] of models) {
      const d = await doc(u); d.querySelectorAll('script,style').forEach(e => e.remove());
      const cards = [];
      d.querySelectorAll('a[href*="chi-tiet/"]').forEach(a => {
        const t = a.textContent.trim().replace(/\s+/g, ' ');
        if (!t || /tr[aả] tr[uư][oớ]c/i.test(t) || cards.some(x => x.name === t)) return;
        let el = a, price = null;
        for (let i = 0; i < 5 && el && !price; i++) { el = el.parentElement; const m = el.textContent.replace(/\s+/g, ' ').match(/Giá từ:?\s*([0-9.]{5,}|[0-9.]+ Giá Tốt Nhất)/); if (m) price = m[1]; }
        cards.push({name: t, price});
      });
      out.cards[slug] = cards;
    }
    for (const [slug, name, u] of configs) {
      const d = await doc(u);
      const sel = [...d.querySelectorAll('select')].find(s => [...s.options].some(o => / - /.test(o.textContent)));
      const pe = d.querySelector('#pdPriceNumber');
      out.configs[slug + '|' + name] = {rows: sel ? [...sel.options].map(o => o.textContent.replace(/\s+/g, ' ').trim()) : [], page: pe ? pe.textContent.trim() : null};
    }
    return out;
  }, {models, configs, cats});
  await page.goto(app + '/', {waitUntil: 'load'});
  const me = await page.evaluate(async ({models, configs, cats}) => {
    const slugify = s => s.replace(/đ/g, 'd').replace(/Đ/g, 'D').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
    const doc = async u => { const r = await fetch(u); return r.ok ? new DOMParser().parseFromString(await r.text(), 'text/html') : null; };
    const out = {strips: {}, cards: {}, configs: {}};
    for (const c of Object.keys(cats)) out.strips[c] = [...(await doc('/Products?category=' + c)).querySelectorAll('.model-tile-name')].map(e => e.textContent.trim());
    for (const [c, slug] of models) out.cards[slug] = [...(await doc('/Products/' + slug)).querySelectorAll('.product-card')].map(e => ({name: e.querySelector('h3').textContent.trim(), price: e.querySelector('.price').textContent.replace('From:', '').trim()}));
    for (const [slug, name] of configs) {
      const d = await doc('/Products/' + slug + '/' + slugify(name));
      out.configs[slug + '|' + name] = d ? JSON.parse(d.querySelector('[data-config-choices]').getAttribute('data-config-choices')) : 'HTTP error';
    }
    return out;
  }, {models, configs, cats});
  return JSON.stringify({rv, me});
}
"""


def digits(text):
    return re.sub(r"[^0-9]", "", text or "")


def is_contact(price):
    return price is None or price.startswith("0963") or "Giá Tốt" in price


def main():
    snapshot = sorted(HERE.glob("snapshot-*.json"))[-1]
    snap = json.loads(snapshot.read_text())
    models = [[c, m["slug"], m["rauvangUrl"]] for c, ms in snap["synced"].items() for m in ms]
    configs = [[m["slug"], cf["name"], cf["rauvangUrl"]] for ms in snap["synced"].values() for m in ms for cf in m["configs"]]
    script = (SCRIPT.replace("__MODELS__", json.dumps(models)).replace("__CONFIGS__", json.dumps(configs))
              .replace("__CATS__", json.dumps(CATEGORIES)).replace("__APP__", json.dumps(APP)))
    with tempfile.NamedTemporaryFile("w", suffix=".js", delete=False) as f:
        f.write(script)
    subprocess.run(["playwright-cli", "-s=verify", "open", "about:blank"], capture_output=True)
    raw = subprocess.run(["playwright-cli", "-s=verify", "--raw", "run-code", f"--filename={f.name}"], capture_output=True, text=True).stdout
    raw = "\n".join(line for line in raw.splitlines() if not line.startswith(("║", "╔", "╚"))).strip()
    data = json.loads(json.loads(raw))
    rv, me = data["rv"], data["me"]

    diffs = []
    for c in rv["strips"]:
        if rv["strips"][c] != me["strips"][c]:
            diffs.append(f"model row {c}: rauvang {rv['strips'][c]} vs ours {me['strips'][c]}")
    cards_total = cards_same = 0
    for slug, theirs in rv["cards"].items():
        ours = me["cards"][slug]
        cards_total += max(len(theirs), len(ours))
        if len(theirs) != len(ours):
            diffs.append(f"{slug}: {len(theirs)} rauvang cards vs {len(ours)} ours")
        for x, y in zip(theirs, ours):
            price_ok = (is_contact(x["price"]) and y["price"] == "Contact for price") or (not is_contact(x["price"]) and digits(x["price"]) == digits(y["price"]))
            if x["name"] == y["name"] and price_ok:
                cards_same += 1
            else:
                diffs.append(f"card {slug}: rauvang [{x['name']} | {x['price']}] vs ours [{y['name']} | {y['price']}]")
    rows_total = rows_same = 0
    for key, r in rv["configs"].items():
        want = []
        for option in r["rows"]:
            m = re.match(r"^(.+?) - (\d*)$", option)
            parts = m[1].split(" / ")
            want.append((parts[0], parts[1] if len(parts) > 1 else None, m[2] if m[2] and int(m[2]) else ""))
        if not want:
            want = [(None, None, digits(r["page"]) if r["page"] and any(ch.isdigit() for ch in r["page"]) else "")]
        ours = me["configs"][key]
        got = [] if isinstance(ours, str) else [(c["color"], c["region"], "" if c["price"] == "Contact for price" else digits(c["price"])) for c in ours]
        rows_total += len(want)
        if sorted(want, key=str) == sorted(got, key=str):
            rows_same += len(want)
        else:
            diffs.append(f"config {key}: rauvang {sorted(want, key=str)} vs ours {sorted(got, key=str)}")

    print(f"snapshot: {snapshot.name}")
    print(f"model rows identical: {sum(rv['strips'][c] == me['strips'][c] for c in rv['strips'])}/{len(rv['strips'])}")
    print(f"cards identical (name + price): {cards_same}/{cards_total} across {len(rv['cards'])} models")
    print(f"colour/region/price rows identical: {rows_same}/{rows_total} on {len(rv['configs'])} configuration pages")
    for d in diffs:
        print("  DIFF " + d)
    return 1 if diffs else 0


if __name__ == "__main__":
    sys.exit(main())
