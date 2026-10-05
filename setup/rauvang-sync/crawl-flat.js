async page => {
  await page.goto('https://rauvang.com/', {waitUntil:'load', timeout:60000});
  return await page.evaluate(async () => {
    const get = async u => new DOMParser().parseFromString(await (await fetch(u)).text(), 'text/html');
    const lists = {airpods:'danh-muc-san-pham/134-airpods.htm', accessories:'danh-muc-san-pham/19-accessories.htm', homepod:'danh-muc-san-pham/137-homepod.htm', balo:'danh-muc-san-pham/64-balo-tui-chong-soc.htm', cap:'danh-muc-san-pham/58-cap.htm', sac:'danh-muc-san-pham/59-sac.htm', khac:'danh-muc-san-pham/60-khac.htm'};
    const out = {};
    for (const [k,u] of Object.entries(lists)) {
      const d = await get('https://rauvang.com/'+u);
      const items = new Map();
      d.querySelectorAll('a[href*="phu-kien-san-pham/"], a[href*="chi-tiet/"]').forEach(a => { const t=a.textContent.trim().replace(/\s+/g,' '); const h=a.getAttribute('href'); if (t && !/tr[aả] tr[uư][oớ]c/i.test(t)) items.set(h,t); });
      out[k] = [];
      for (const [h,t] of items) {
        const vd = await get(new URL(h,'https://rauvang.com/').href);
        const sel = [...vd.querySelectorAll('select')].find(s => [...s.options].some(o => / - /.test(o.textContent)));
        const opts = sel ? [...sel.options].map(o => o.textContent.replace(/\s+/g,' ').trim()) : [];
        const priceEl = vd.querySelector('#pdPriceNumber, .pdPrice, .price');
        const body = vd.body.innerText || vd.body.textContent;
        const txt=(vd.body.textContent||'').replace(/\s+/g,' '); const m=(txt.match(/([0-9]{1,3}(?:[.,][0-9]{3}){1,3})\s*VN[ĐD]/)||[])[1]; const w=(txt.match(/Bảo hành:\s*([^|]{1,60}?)(?= [A-ZĐ]|$)/)||[])[1];
        out[k].push({name:t, url:h, options:opts, pagePrice: m||null, warranty: w||null});
      }
    }
    return JSON.stringify(out);
  });
}
