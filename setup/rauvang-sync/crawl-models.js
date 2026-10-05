async page => {
  await page.goto('https://rauvang.com/', {waitUntil:'load', timeout:60000});
  return await page.evaluate(async () => {
    const get = async u => new DOMParser().parseFromString(await (await fetch(u)).text(), 'text/html');
    const abs = h => new URL(h, 'https://rauvang.com/').href;
    const cats = {iphone:'danh-muc/14-iphone.htm', ipad:'danh-muc/7-ipad.htm', mac:'danh-muc/1-mac.htm', watch:'danh-muc/20-watch.htm'};
    const out = {};
    for (const [c, u] of Object.entries(cats)) {
      const d = await get(abs(u));
      const models = [...new Map([...d.querySelectorAll('a[href*="danh-muc-san-pham/"]')].map(a => [abs(a.getAttribute('href')), a.textContent.trim().replace(/\s+/g,' ')])).entries()].filter(([,n])=>n);
      out[c] = [];
      for (const [mu, mn] of models) {
        const md = await get(mu);
        const vlinks = [...new Map([...md.querySelectorAll('a[href*="chi-tiet/"]')].map(a => [abs(a.getAttribute('href')), a.textContent.trim().replace(/\s+/g,' ')])).entries()];
        const names = {}; vlinks.forEach(([h,t]) => { if (t && !/tr[aả] tr[uư][oớ]c/i.test(t)) names[h] = t; });
        const variants = [];
        for (const vu of Object.keys(names)) {
          const vd = await get(vu);
          const sel = [...vd.querySelectorAll('select')].find(s => [...s.options].some(o => / - /.test(o.textContent)));
          const opts = sel ? [...sel.options].map(o => o.textContent.replace(/\s+/g,' ').trim()) : [];
          const imgs = [...vd.querySelectorAll('img')].map(i=>i.getAttribute('src')||'').filter(s=>/data\/Product/i.test(s)).length;
          // Config pages with no colour list print one price in #pdPriceNumber.
          const priceEl = vd.querySelector('#pdPriceNumber');
          const pagePrice = priceEl ? priceEl.textContent.trim() : null;
          variants.push({ name: names[vu], url: vu.replace('https://rauvang.com/',''), options: opts, pagePrice, productImgs: imgs });
        }
        out[c].push({ model: mn, url: mu.replace('https://rauvang.com/',''), variants });
      }
    }
    return JSON.stringify(out);
  });
}
