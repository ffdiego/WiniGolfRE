// Static evaluation of saved Ghidra instructions. Does not execute WiniGolf.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const binary = fs.readFileSync(path.resolve(root, '../WiniGolf/WiniGolf.exe'));
if (crypto.createHash('sha256').update(binary).digest('hex') !== '3b50e7311d733a1afbd3a33b31e14f95564226e1310a230ffc375e6727a6fb53') throw Error('Binary hash mismatch');
const pe = binary.readUInt32LE(60), sectionStart = pe + 24 + binary.readUInt16LE(pe + 20);
const sections = Array.from({length: binary.readUInt16LE(pe + 6)}, (_, i) => {
  const p = sectionStart + i * 40;
  return {rva: binary.readUInt32LE(p + 12), size: binary.readUInt32LE(p + 16), raw: binary.readUInt32LE(p + 20)};
});
function fileOffset(va) {
  const rva = va - 0x400000, s = sections.find(s => rva >= s.rva && rva < s.rva + s.size);
  if (!s) throw Error('Unmapped address ' + va.toString(16));
  return s.raw + rva - s.rva;
}
function cstring(va) {
  const p = fileOffset(va), end = binary.indexOf(0, p);
  if (end < p || end - p > 100) throw Error('Invalid field name');
  return binary.toString('latin1', p, end);
}
const evidence = JSON.parse(fs.readFileSync(path.join(root, 'evidencias/inicializacao-campos-completa.json'), 'utf8'));
if (evidence.items.length !== 19051) throw Error('Incomplete instruction listing');
for (const ins of evidence.items) {
  const bytes = Buffer.from(ins.bytes, 'hex');
  if (!binary.subarray(fileOffset(parseInt(ins.address, 16)), fileOffset(parseInt(ins.address, 16)) + bytes.length).equals(bytes)) throw Error('Instruction byte mismatch ' + ins.address);
}
const regs = {EBX: 0, ESI: 1, EBP: -0x10000};
const memory = new Map(), stringAssignments = [];
function number(expr) {
  const expression = expr.replace(/^.*?\[/, '').replace(/\]$/, '');
  return expression.split(' + ').reduce((sum, term) => sum + term.split('*').reduce((product, p) => {
    p = p.trim();
    if (/^E[A-Z]{2}$/.test(p)) {
      if (!Number.isFinite(regs[p])) throw Error('Unknown register ' + p);
      return product * regs[p];
    }
    if (!/^-?(?:0x[0-9a-f]+|\d+)$/i.test(p)) throw Error('Unsupported expression ' + expr);
    return product * (p.startsWith('-') ? -Number(p.slice(1)) : Number(p));
  }, 1), 0) | 0;
}
let current;
function save(offset, value, extra = {}) { memory.set(offset, {value, instruction: current.address, objectOffset: '0x' + offset.toString(16), ...extra}); }
function reg(name) { if (!Number.isFinite(regs[name])) throw Error('Unknown register ' + name); return regs[name]; }
for (const ins of evidence.items) {
  const address = parseInt(ins.address, 16);
  if (address < 0x404ef3) continue;
  if (address >= 0x41b3f8) break; // All 36 blocks end here; following code initializes demo state.
  current = ins;
  try {
    const call = ins.instruction.match(/^CALL (0x[0-9a-f]+)$/i);
    if (call) {
      const target = Number(call[1]); let returned;
      if (target === 0x48c804) {
        const dest = reg('EAX'), source = reg('EDX');
        save(dest, cstring(source), {stringAddress: '0x' + source.toString(16)}); returned = dest;
      } else if (target === 0x48c8ec) {
        const source = memory.get(reg('EDX'));
        if (!source || typeof source.value !== 'string') throw Error('Unknown string source');
        save(reg('EAX'), source.value, {stringAddress: source.stringAddress, construction: source.instruction});
        stringAssignments.push(reg('EAX'));
      } else if (target !== 0x48c8bc) throw Error('Unmodeled call');
      regs.EAX = returned; regs.ECX = undefined; regs.EDX = undefined;
      continue;
    }
    const unary = ins.instruction.match(/^(INC|DEC) (.*)$/);
    if (unary) {
      const [, op, dst] = unary;
      if (/^E[A-Z]{2}$/.test(dst)) regs[dst] = (reg(dst) + (op === 'INC' ? 1 : -1)) | 0;
      else if (!dst.startsWith('dword ptr [EBP + ')) throw Error('Unexpected unary memory write');
      continue; // EBP-relative INC/DEC only maintain runtime exception cleanup depth.
    }
    const match = ins.instruction.match(/^(MOV|LEA|ADD|SHL|XOR) (.*?),(.*)$/);
    if (!match) throw Error('Unsupported instruction');
    const [, op, dst, src] = match;
    if (dst.startsWith('word ptr [EBP + ')) continue; // Runtime cleanup-state tag.
    if (dst.startsWith('dword ptr [')) {
      if (op !== 'MOV') throw Error('Unsupported memory operation');
      save(number(dst), number(src));
    } else {
      if (!/^(EAX|EBX|ECX|EDX|ESI)$/.test(dst)) throw Error('Unexpected register');
      if (op === 'XOR' && dst === src) regs[dst] = 0;
      else {
        const n = number(src);
        if (op === 'MOV' || op === 'LEA') regs[dst] = n;
        else if (op === 'ADD') regs[dst] = (reg(dst) + n) | 0;
        else if (op === 'SHL') regs[dst] = reg(dst) << n;
        else regs[dst] = reg(dst) ^ n;
      }
    }
  } catch (e) { throw Error(ins.address + ' ' + ins.instruction + ': ' + e.message); }
}
if (regs.ESI !== 36 || stringAssignments.length !== 36) throw Error('Field block count mismatch');
const fields = [], issues = [];
function read(offset) { const v = memory.get(offset); if (!v) throw Error('Missing assignment 0x' + offset.toString(16)); return v; }
function coordinate(offset) { return memory.get(offset) ?? {value:null,instruction:null,objectOffset:'0x'+offset.toString(16),status:'No explicit assignment in initializer; value not inferred.'}; }
for (let h = 1; h <= 36; h++) {
  const vertexCount = read(0x9cd54 + h*4), surfaceCount = read(0x9cde8 + h*4);
  if (vertexCount.value < 1 || vertexCount.value > 100 || surfaceCount.value < 1 || surfaceCount.value > 50) throw Error('Invalid counts');
  const vertices = Array.from({length: vertexCount.value}, (_, i) => ({id:i+1,
    x:coordinate(0x81bc0+h*400+i*4), y:coordinate(0x85590+h*400+i*4), z:coordinate(0x88f60+h*400+i*4)}));
  const surfaces = Array.from({length:surfaceCount.value}, (_, s) => {
    const count = read(0x9b070+h*200+s*4);
    if (count.value < 3 || count.value > 8) throw Error('Invalid surface size');
    return {id:s+1, vertexCount:count,
      vertices:Array.from({length:count.value}, (_, e) => read(0x8c950+h*1600+s*32+e*4)),
      neighbors:Array.from({length:count.value}, (_, e) => read(0x9cea0+h*3200+s*32+e*4))};
  });
  const name = read(0xc7ff0+h*4);
  const field = {field:h, name:name.value, nameEvidence:name, vertexCount,surfaceCount,vertices,surfaces,
    target:[0xc7abc,0xc7b50,0xc7be4].map(b=>read(b+h*4)),
    start:[0xc7c78,0xc7d0c,0xc7da0].map(b=>read(b+h*4)),
    initialSurface:read(0xc7e34+h*4), unclassifiedParameter:read(0xc7ec8+h*4), par:read(0xc7f5c+h*4)};
  function issue(type, detail) { issues.push({field:h,name:field.name,type,...detail}); }
  for(const v of vertices) if([v.x.value,v.y.value,v.z.value].includes(null)) issue('unassigned-coordinate',{vertex:v.id,referencedBySurfaces:surfaces.filter(s=>s.vertices.some(i=>i.value===v.id)).map(s=>s.id)});
  for (const s of surfaces) {
    if (s.vertices.some(v=>v.value<1||v.value>vertices.length)) throw Error('Invalid vertex index');
    s.neighbors.forEach((n,i)=> {
      if (!n.value) return;
      const other=surfaces[n.value-1], a=s.vertices[i].value,b=s.vertices[(i+1)%s.vertices.length].value;
      if (!other) throw Error('Invalid neighbor index');
      if (!other.vertices.some((v,j)=>v.value===b&&other.vertices[(j+1)%other.vertices.length].value===a&&other.neighbors[j].value===s.id)) issue('nonreciprocal-edge',{surface:s.id,edge:i+1,neighbor:n.value,a,b});
    });
    const [a,b,c]=s.vertices.slice(0,3).map(v=>vertices[v.value-1]);
    if (b.x.value===a.x.value || c.y.value===b.y.value) issue('zero-height-denominator',{surface:s.id});
  }
  if (field.initialSurface.value<1||field.initialSurface.value>surfaces.length) throw Error('Invalid initial surface');
  field.heightRange=[Math.min(...vertices.map(v=>v.z.value).filter(v=>v!==null)),Math.max(...vertices.map(v=>v.z.value).filter(v=>v!==null))];
  fields.push(field);
}
const outDir=path.join(root,'dados/campos'); fs.mkdirSync(outDir,{recursive:true});
for(const field of fields) fs.writeFileSync(path.join(outDir,String(field.field).padStart(2,'0')+'.json'),JSON.stringify(field,null,2)+'\n');
const summary={sourceSha256:crypto.createHash('sha256').update(binary).digest('hex'), instructionCount:evidence.items.length,
  method:'Static subset evaluation; string helper calls modeled, volatile registers invalidated; all instruction bytes checked against PE. No execution.',
  fields:fields.map(f=>({field:f.field,name:f.name,vertices:f.vertexCount.value,surfaces:f.surfaceCount.value,par:f.par.value,heightRange:f.heightRange})),issues};
fs.writeFileSync(path.join(root,'dados/campos-resumo.json'),JSON.stringify(summary,null,2)+'\n');
console.log(JSON.stringify(summary,null,2));
