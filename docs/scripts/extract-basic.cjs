// Read saved Ghidra evidence only. Never opens or executes the game or its database.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const evidence = JSON.parse(fs.readFileSync(path.join(root, 'evidencias/campo-basic-ghidra.json'), 'utf8'));
const regs = { EBX: 0, ESI: 1, EBP: -0x10000 };
const memory = new Map();
function value(expr) {
  return expr.replace(/^.*?\[/, '').replace(/\]$/, '').split(' + ').reduce((sum, term) => {
    const parts = term.split('*');
    const n = parts.reduce((product, part) => {
      part = part.trim();
      if (Object.hasOwn(regs, part)) return product * regs[part];
      if (/^(?:0x[0-9a-f]+|-?\d+)$/i.test(part)) return product * Number(part);
      throw new Error('Unsupported expression: ' + expr);
    }, 1);
    return sum + n;
  }, 0);
}
for (const ins of evidence.courseInitialization) {
  const addr = parseInt(ins.address, 16);
  if (addr < 0x404ef3) continue;
  if (addr >= 0x405406) break; // Before string construction/runtime calls.
  const match = ins.instruction.match(/^(MOV|LEA|ADD|SHL|XOR) (.*?),(.*)$/);
  if (!match) throw new Error('Unsupported instruction at ' + ins.address);
  const [, op, dst, src] = match;
  if (dst.startsWith('dword ptr [')) {
    if (op !== 'MOV') throw new Error('Unexpected write');
    memory.set(value(dst), { value: value(src) | 0, instruction: ins.address });
  } else {
    if (!/^(EAX|EBX|ECX|EDX|ESI)$/.test(dst)) throw new Error('Unexpected register');
    const n = value(src);
    if (op === 'MOV' || op === 'LEA') regs[dst] = n;
    else if (op === 'ADD') regs[dst] += n;
    else if (op === 'SHL') regs[dst] *= 2 ** n;
    else regs[dst] = regs[dst] ^ n;
  }
}
function read(offset) {
  const item = memory.get(offset);
  if (!item) throw new Error('Missing explicit assignment: ' + offset.toString(16));
  return { ...item, objectOffset: '0x' + offset.toString(16) };
}
const field = 1;
const vertexCount = read(0x9cd54 + field * 4);
const surfaceCount = read(0x9cde8 + field * 4);
const vertices = Array.from({ length: vertexCount.value }, (_, i) => ({
  id: i + 1,
  x: read(0x81bc0 + field * 400 + i * 4),
  y: read(0x85590 + field * 400 + i * 4),
  z: read(0x88f60 + field * 400 + i * 4)
}));
const surfaces = Array.from({ length: surfaceCount.value }, (_, i) => {
  const count = read(0x9b070 + field * 200 + i * 4);
  return { id: i + 1, vertexCount: count,
    vertices: Array.from({ length: count.value }, (_, j) => read(0x8c950 + field * 1600 + i * 32 + j * 4)),
    neighbors: Array.from({ length: count.value }, (_, j) => read(0x9cea0 + field * 3200 + i * 32 + j * 4)) };
});
const result = { field, name: 'Basic', nameAddress: '0x0048e5eb',
  method: 'Evaluation of MOV/LEA/ADD/SHL/XOR from saved disassembly; no game execution.',
  vertexCount, surfaceCount, vertices, surfaces,
  target: [0xc7abc, 0xc7b50, 0xc7be4].map(b => read(b + field * 4)),
  start: [0xc7c78, 0xc7d0c, 0xc7da0].map(b => read(b + field * 4)),
  initialSurface: read(0xc7e34 + field * 4),
  unclassifiedParameter: read(0xc7ec8 + field * 4),
  parCandidate: read(0xc7f5c + field * 4) };
for (const s of surfaces) {
  if (s.vertices.some(v => v.value < 1 || v.value > vertices.length)) throw new Error('Invalid vertex');
  s.neighbors.forEach((n, i) => {
    if (!n.value) return;
    const other = surfaces[n.value - 1];
    const a = s.vertices[i].value, b = s.vertices[(i + 1) % s.vertices.length].value;
    if (!other || !other.vertices.some((v, j) => v.value === b && other.vertices[(j + 1) % other.vertices.length].value === a && other.neighbors[j].value === s.id)) throw new Error('Nonreciprocal edge');
  });
}
fs.writeFileSync(path.join(root, 'dados/basic.json'), JSON.stringify(result, null, 2) + '\n');
console.log(`Basic: ${vertices.length} vertices, ${surfaces.length} surfaces; reciprocal links validated.`);
