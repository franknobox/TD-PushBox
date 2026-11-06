const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const root = path.resolve(__dirname, '../..');
const audioDir = path.join(root, 'Assets/TD_PushBox/Audio/Gameplay');
const source = fs.readFileSync(path.join(audioDir, '90936__robinhood76__01497-brawling-2.wav'));
assert.equal(source.toString('ascii', 0, 4), 'RIFF');
assert.equal(source.toString('ascii', 8, 12), 'WAVE');
let format, pcm;
for (let offset = 12; offset + 8 <= source.length;) {
  const id = source.toString('ascii', offset, offset + 4);
  const size = source.readUInt32LE(offset + 4);
  assert.ok(offset + 8 + size <= source.length);
  if (id === 'fmt ') format = source.subarray(offset + 8, offset + 8 + size);
  if (id === 'data') pcm = source.subarray(offset + 8, offset + 8 + size);
  offset += 8 + size + size % 2;
}
assert.ok(format && pcm);
assert.equal(format.readUInt16LE(0), 1, 'Expected PCM');
assert.equal(format.readUInt16LE(14), 16, 'Expected 16-bit audio');
const channels = format.readUInt16LE(2);
const rate = format.readUInt32LE(4);
const align = format.readUInt16LE(12);
assert.equal(align, channels * 2);

const cuts = [
  { name: 'CrateMove1.wav', start: 0.30, end: 0.62 },
  { name: 'CrateMove2.wav', start: 1.65, end: 1.97 },
];
for (const cut of cuts) {
  const start = Math.round(cut.start * rate);
  const frames = Math.round((cut.end - cut.start) * rate);
  assert.ok((start + frames) * align <= pcm.length);
  const samples = new Float64Array(frames * channels);
  const fadeIn = Math.round(0.008 * rate);
  const fadeOut = Math.round(0.035 * rate);
  let energy = 0, peak = 0;
  for (let frame = 0; frame < frames; frame++) {
    const envelope = Math.min(1, frame / fadeIn, (frames - 1 - frame) / fadeOut);
    for (let channel = 0; channel < channels; channel++) {
      const index = frame * channels + channel;
      const value = pcm.readInt16LE((start + frame) * align + channel * 2) / 32768 * envelope;
      samples[index] = value;
      energy += value * value;
      peak = Math.max(peak, Math.abs(value));
    }
  }
  const rms = Math.sqrt(energy / samples.length);
  const gain = Math.min(10 ** (-20 / 20) / rms, 0.85 / peak);
  const output = Buffer.alloc(44 + frames * align);
  output.write('RIFF', 0);
  output.writeUInt32LE(output.length - 8, 4);
  output.write('WAVEfmt ', 8);
  output.writeUInt32LE(16, 16);
  format.copy(output, 20, 0, 16);
  output.write('data', 36);
  output.writeUInt32LE(frames * align, 40);
  for (let i = 0; i < samples.length; i++) output.writeInt16LE(Math.round(samples[i] * gain * 32767), 44 + i * 2);
  fs.writeFileSync(path.join(audioDir, cut.name), output);
  assert.equal(output.readInt16LE(44), 0);
  assert.equal(output.readInt16LE(output.length - 2), 0);
  console.log(JSON.stringify({ ...cut, seconds: frames / rate, channels, rate,
    rmsDb: +(20 * Math.log10(rms * gain)).toFixed(2), peak: +(peak * gain).toFixed(3) }));
}
