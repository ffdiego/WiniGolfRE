// Read-only PE resource extraction. Never loads or executes the original program.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const exe = fs.readFileSync(path.resolve(root, '../WiniGolf/WiniGolf.exe'));
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const executableHash = hash(exe);
if (executableHash !== '3b50e7311d733a1afbd3a33b31e14f95564226e1310a230ffc375e6727a6fb53') throw Error('Unexpected executable');
const pe = exe.readUInt32LE(60), optional = pe + 24;
if (exe.toString('ascii', pe, pe + 4) !== 'PE\0\0' || exe.readUInt16LE(optional) !== 0x10b) throw Error('Expected PE32');
const table = optional + exe.readUInt16LE(pe + 20);
const sections = Array.from({ length: exe.readUInt16LE(pe + 6) }, (_, i) => {
  const p = table + 40 * i;
  return { rva: exe.readUInt32LE(p + 12), size: exe.readUInt32LE(p + 16), raw: exe.readUInt32LE(p + 20) };
});
function offset(rva, length = 1) {
  const section = sections.find(s => rva >= s.rva && rva + length <= s.rva + s.size);
  if (!section) throw Error('Unmapped resource RVA');
  const p = section.raw + rva - section.rva;
  if (p + length > exe.length) throw Error('Resource exceeds file');
  return p;
}
const resourceRva = exe.readUInt32LE(optional + 112);
const base = offset(resourceRva);
const resources = [], visited = new Set();
function name(value) {
  if (!(value & 0x80000000)) return value;
  const p = base + (value & 0x7fffffff), count = exe.readUInt16LE(p);
  return exe.toString('utf16le', p + 2, p + 2 + 2 * count);
}
function directory(relative, names) {
  if (names.length > 4 || visited.has(relative)) throw Error('Invalid resource directory');
  visited.add(relative);
  const p = base + relative, count = exe.readUInt16LE(p + 12) + exe.readUInt16LE(p + 14);
  for (let i = 0; i < count; i++) {
    const q = p + 16 + i * 8, names2 = [...names, name(exe.readUInt32LE(q))];
    const target = exe.readUInt32LE(q + 4);
    if (target & 0x80000000) directory(target & 0x7fffffff, names2);
    else {
      const data = base + target, rva = exe.readUInt32LE(data), size = exe.readUInt32LE(data + 4);
      const fileOffset = offset(rva, size), bytes = exe.subarray(fileOffset, fileOffset + size);
      resources.push({ names: names2, rva, fileOffset, size, bytes });
    }
  }
}
directory(0, []);
const destination = path.join(root, 'simulator/assets/audio');
fs.mkdirSync(destination, { recursive: true });
const sounds = resources.filter(r => r.bytes.toString('ascii', 0, 4) === 'RIFF' && r.bytes.toString('ascii', 8, 12) === 'WAVE').map(r => {
  const filename = String(r.names[1]).toLowerCase().replace(/[^a-z0-9_-]/g, '_') + '.wav';
  let format, dataSize;
  for (let p = 12; p + 8 <= r.bytes.length;) {
    const id = r.bytes.toString('ascii', p, p + 4), size = r.bytes.readUInt32LE(p + 4);
    if (p + 8 + size > r.bytes.length) throw Error('Invalid WAV chunk');
    if (id === 'fmt ' && size >= 16) format = { encoding: r.bytes.readUInt16LE(p + 8), channels: r.bytes.readUInt16LE(p + 10), sampleRate: r.bytes.readUInt32LE(p + 12), byteRate: r.bytes.readUInt32LE(p + 16), bitsPerSample: r.bytes.readUInt16LE(p + 22) };
    if (id === 'data') dataSize = size;
    p += 8 + size + (size & 1);
  }
  if (!format || dataSize === undefined) throw Error('Missing WAV format/data');
  const output = path.join(destination, filename);
  if (resources.some(other => other !== r && other.names[1] === r.names[1] && other.bytes.toString('ascii', 0, 4) === 'RIFF')) throw Error('Ambiguous sound name');
  fs.writeFileSync(output, r.bytes);
  return { resourcePath: r.names, rva: '0x' + r.rva.toString(16), fileOffset: '0x' + r.fileOffset.toString(16), size: r.size, sha256: hash(r.bytes), file: path.relative(root, output).replaceAll('\\', '/'), format, durationSeconds: dataSize / format.byteRate };
});
if (!sounds.length) throw Error('No sound resources found');
fs.writeFileSync(path.join(root, 'docs/dados/sons-embutidos.json'), JSON.stringify({ executableHash, method: 'PE resource directory; original WAV payloads copied byte-for-byte', sounds }, null, 2) + '\n');
console.log(JSON.stringify(sounds.map(s => ({ resource: s.resourcePath, file: s.file, format: s.format, duration: s.durationSeconds })), null, 2));
