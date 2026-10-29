// Static engineering diagram from extracted data, not a game renderer.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..');
const escape=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
const parts=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1440 2020" role="img" aria-label="Atlas dos 36 campos extraídos de WiniGolf"><rect width="1440" height="2020" fill="#fafafa"/><g font-family="sans-serif"><text x="24" y="30" font-size="22">WiniGolf — 36 campos: geometria extraída</text><text x="24" y="53" font-size="13">Plantas x/y, y positivo para cima. Escala ajustada por campo; azul mais escuro indica maior altura média da superfície.</text>'];
for(let h=1;h<=36;h++) {
  const f=JSON.parse(fs.readFileSync(path.join(root,'dados/campos',String(h).padStart(2,'0')+'.json')));
  const vs=f.vertices.filter(v=>v.x.value!==null&&v.y.value!==null);
  const minX=Math.min(...vs.map(v=>v.x.value)),maxX=Math.max(...vs.map(v=>v.x.value)),minY=Math.min(...vs.map(v=>v.y.value)),maxY=Math.max(...vs.map(v=>v.y.value));
  const cellX=((h-1)%4)*360,cellY=Math.floor((h-1)/4)*215+70;
  const scale=Math.min(310/(maxX-minX),145/(maxY-minY));
  const tx=x=>cellX+180+(x-(minX+maxX)/2)*scale,ty=y=>cellY+112-(y-(minY+maxY)/2)*scale;
  parts.push(`<rect x="${cellX+7}" y="${cellY}" width="346" height="208" rx="6" fill="white" stroke="#cbd5e1"/><text x="${cellX+18}" y="${cellY+23}" font-size="15">${h}. ${escape(f.name)} · par ${f.par.value}</text>`);
  for(const s of f.surfaces){
    const pts=s.vertices.map(v=>f.vertices[v.value-1]);
    if(pts.some(v=>v.x.value===null||v.y.value===null||v.z.value===null))throw Error('Surface with unknown coordinate');
    const z=pts.reduce((a,v)=>a+v.z.value,0)/pts.length;
    parts.push(`<polygon points="${pts.map(v=>`${tx(v.x.value)},${ty(v.y.value)}`).join(' ')}" fill="hsl(200,45%,${87-Math.min(160,z)*.23}%)" stroke="#334155" stroke-width="1"/>`);
  }
  for(const s of f.surfaces) s.neighbors.forEach((n,i)=>{if(n.value>s.id){const a=f.vertices[s.vertices[i].value-1],b=f.vertices[s.vertices[(i+1)%s.vertices.length].value-1];parts.push(`<path d="M${tx(a.x.value)} ${ty(a.y.value)}L${tx(b.x.value)} ${ty(b.y.value)}" stroke="#f59e0b" stroke-width="2" stroke-dasharray="3 2"/>`);}});
  parts.push(`<circle cx="${tx(f.start[0].value)}" cy="${ty(f.start[1].value)}" r="3.5" fill="#16a34a" stroke="white"/><circle cx="${tx(f.target[0].value)}" cy="${ty(f.target[1].value)}" r="3.5" fill="#111827" stroke="white"/><text x="${cellX+18}" y="${cellY+197}" font-size="11">${f.vertices.length} vértices · ${f.surfaces.length} superfícies · z ${f.heightRange.join('…')}</text>`);
}
parts.push('<text x="24" y="2020" font-size="12">Verde: partida. Preto: alvo. Tracejado laranja: passagem. Fonte: dados/campos/*.json. Não é captura do original.</text></g></svg>');
fs.writeFileSync(path.join(root,'diagramas/campos-atlas.svg'),parts.join('\n')+'\n');
console.log('Atlas generated from all 36 JSON files.');
