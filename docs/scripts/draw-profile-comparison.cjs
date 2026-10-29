const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'artifacts/simulator');
const pairs=[['corner-reference-v1.json','corner-traversal-v2.json'],['plateau-reference.json','plateau-traversal.json']];
const svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 920" role="img" aria-label="Comparação de dois perfis do simulador"><rect width="1000" height="920" fill="#fafafa"/><g font-family="sans-serif" fill="#17202a"><text x="24" y="30" font-size="22">Validação de cantos — comparação dos simuladores</text><text x="24" y="53" font-size="13">Azul: modern-reference-1. Vermelho: surface-traversal-2. Não são gravações do jogo original.</text></g>'];
for(let panel=0;panel<pairs.length;panel++){
 const [a,b]=pairs[panel].map(file=>JSON.parse(fs.readFileSync(path.join(out,file))));
 const f=JSON.parse(fs.readFileSync(path.join(root,'docs/dados/campos',String(a.courseId).padStart(2,'0')+'.json')));
 const vertices=f.vertices.filter(v=>v.x.value!==null).map(v=>({x:v.x.value,y:v.y.value}));
 const pts=[...vertices,...a.frames.map(f=>f.state.position),...b.frames.map(f=>f.state.position)];
 const minX=Math.min(...pts.map(p=>p.x)),maxX=Math.max(...pts.map(p=>p.x)),minY=Math.min(...pts.map(p=>p.y)),maxY=Math.max(...pts.map(p=>p.y));
 const scale=Math.min(880/(maxX-minX),280/(maxY-minY)),top=panel*415+80;
 const pxy=p=>`${500+(p.x-(minX+maxX)/2)*scale},${top+200-(p.y-(minY+maxY)/2)*scale}`;
 svg.push(`<text x="24" y="${top+15}" font-family="sans-serif" font-size="18">${a.courseId}. ${a.courseName} · ${panel===0?'passagem e parede no mesmo passo':'diferença de trajeto após cruzamento de passagem'}</text>`);
 for(const s of f.surfaces)svg.push(`<polygon points="${s.vertices.map(i=>pxy({x:f.vertices[i.value-1].x.value,y:f.vertices[i.value-1].y.value})).join(' ')}" fill="#e2ece6" stroke="#50685a" stroke-width="1.5"/>`);
 for(const [r,color,dash] of [[a,'#2563eb','6 3'],[b,'#dc2626','none']]){
  svg.push(`<polyline points="${r.frames.map(f=>pxy(f.state.position)).join(' ')}" fill="none" stroke="${color}" stroke-width="2.3" stroke-dasharray="${dash}"/>`);
  const [x,y]=pxy(r.finalState.position).split(',');svg.push(`<circle cx="${x}" cy="${y}" r="5" fill="${color}" stroke="white"/>`);
 }
 const target={x:f.target[0].value,y:f.target[1].value};const [x,y]=pxy(target).split(',');svg.push(`<circle cx="${x}" cy="${y}" r="5" fill="#111827"/>`);
 svg.push(`<text x="24" y="${top+385}" font-family="sans-serif" font-size="13">Referência: ${a.reason}, ${a.steps} passos. Experimental: ${b.reason}, ${b.steps} passos. Pontos coloridos: posições finais.</text>`);
}
svg.push('</svg>');fs.writeFileSync(path.join(out,'profile-comparison.svg'),svg.join('\n')+'\n');
console.log('Profile comparison SVG written.');
