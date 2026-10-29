// Audit shared edges using original vertex IDs; no geometry repair or game execution.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),report=[];
for(let h=1;h<=36;h++) {
 const f=JSON.parse(fs.readFileSync(path.join(root,'dados/campos',String(h).padStart(2,'0')+'.json')));
 const edges=new Map();
 for(const s of f.surfaces) s.vertices.forEach((v,i)=>{
  const a=v.value,b=s.vertices[(i+1)%s.vertices.length].value,key=[a,b].sort((a,b)=>a-b).join(':');
  const entry={surface:s.id,edge:i+1,a,b,neighbor:s.neighbors[i].value,neighborInstruction:s.neighbors[i].instruction};
  if(!edges.has(key))edges.set(key,[]);edges.get(key).push(entry);
 });
 const internalWalls=[];
 for(const entries of edges.values()) {
  if(entries.length>2)throw Error('Nonmanifold ID edge in '+h);
  if(entries.length===2&&entries.every(e=>e.neighbor===0)) {
   if(entries[0].a!==entries[1].b||entries[0].b!==entries[1].a)throw Error('Unexpected winding');
   internalWalls.push({vertices:[entries[0].a,entries[0].b],coordinates:[entries[0].a,entries[0].b].map(i=>f.vertices[i-1]).map(v=>[v.x.value,v.y.value,v.z.value]),sides:entries});
  }
 }
 const graph=f.surfaces.map(s=>({surface:s.id,neighbors:s.neighbors.filter(n=>n.value!==0).map(n=>n.value)}));
 const reachable=new Set([f.initialSurface.value]),queue=[f.initialSurface.value];
 while(queue.length)for(const n of graph[queue.shift()-1].neighbors)if(!reachable.has(n)){reachable.add(n);queue.push(n);}
 report.push({field:h,name:f.name,internalWalls,passageGraph:graph,reachableFromStart:[...reachable].sort((a,b)=>a-b)});
}
fs.writeFileSync(path.join(root,'dados/arestas-internas.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report.filter(f=>f.internalWalls.length).map(f=>({field:f.field,name:f.name,internalWalls:f.internalWalls.length})),null,2));
console.log('Splitt:',JSON.stringify(report[24],null,2));
