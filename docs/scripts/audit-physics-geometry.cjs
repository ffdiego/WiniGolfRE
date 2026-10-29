// Independent structural checks of assumptions used by the physical specification.
const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'..'),errors=[],rows=[];
let surfaces=0,sloped=0;
for(let h=1;h<=36;h++){
 const f=JSON.parse(fs.readFileSync(path.join(root,'dados/campos',String(h).padStart(2,'0')+'.json')));
 for(const s of f.surfaces){
  surfaces++;
  const p=s.vertices.map(v=>f.vertices[v.value-1]).map(v=>({x:v.x.value,y:v.y.value,z:v.z.value}));
  if(p.some(v=>Object.values(v).some(n=>!Number.isFinite(n))))throw Error('Unknown coordinate referenced by polygon');
  const [a,b,c]=p,dx=b.x-a.x,dy=c.y-b.y,dzx=b.z-a.z,dzy=c.z-b.z;
  if(!(dx>0&&dy<0))errors.push({field:h,surface:s.id,type:'orientation-or-denominator'});
  const residual=Math.max(...p.map(q=>Math.abs(a.z+dzx*(q.x-a.x)/dx+dzy*(q.y-a.y)/dy-q.z)));
  if(residual>1e-9)errors.push({field:h,surface:s.id,type:'not-on-height-plane',residual});
  let twiceArea=0,positiveTurns=0;
  for(let i=0;i<p.length;i++){
   const q=p[i],r=p[(i+1)%p.length],t=p[(i+2)%p.length];
   twiceArea+=q.x*r.y-r.x*q.y;
   if((r.x-q.x)*(t.y-r.y)-(r.y-q.y)*(t.x-r.x)>0)positiveTurns++;
  }
  if(twiceArea>=0||positiveTurns)errors.push({field:h,surface:s.id,type:'not-clockwise-convex'});
  if(dzx||dzy)sloped++;
  rows.push({field:h,surface:s.id,dx,dy,dzx,dzy,maxHeightResidual:residual,clockwise:twiceArea<0,convex:positiveTurns===0});
 }
}
const report={method:'Geometry checks against mathematical preconditions, not execution of game or physics simulation.',fields:36,surfaces,slopedSurfaces:sloped,errors,surfacesChecked:rows};
fs.writeFileSync(path.join(root,'dados/auditoria-geometria-fisica.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({fields:36,surfaces,slopedSurfaces:sloped,errors},null,2));
if(errors.length)process.exitCode=1;
