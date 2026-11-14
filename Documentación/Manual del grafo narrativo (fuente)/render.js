const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); const p = await b.newPage();
  await p.goto('file://' + process.cwd() + '/manual.html', { waitUntil: 'networkidle' });
  await p.pdf({ path: 'manual.pdf', format: 'A4', printBackground: true, preferCSSPageSize: true });
  // overflow check
  const over = await p.$$eval('section.page', ps => ps.map((s,i)=>{ const f=s.querySelector('.folio'); let maxB=0; for (const c of s.children){ if(c.classList.contains('folio')) continue; const r=c.getBoundingClientRect(); maxB=Math.max(maxB,r.bottom);} const top=s.getBoundingClientRect().top; return [i+1, Math.round(maxB-top), f? Math.round(f.getBoundingClientRect().top-top):null]; }));
  console.log(JSON.stringify(over));
  await b.close();
})();
