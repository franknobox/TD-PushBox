const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');

const audioDir = path.resolve(__dirname, '../../Assets/TD_PushBox/Audio/Gameplay');
const source = fs.readFileSync(path.join(audioDir, '338287__werra__piano-pedal-pressed-organ-pull-stop-sound.wav'));
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
assert.equal(format.readUInt16LE(0), 1);
assert.equal(format.readUInt16LE(14), 16);
const channels = format.readUInt16LE(2);
const rate = format.readUInt32LE(4);
const align = format.readUInt16LE(12);
assert.equal(align, channels * 2);
const start = Math.round(0.14 * rate);
const frames = Math.round((0.95 - 0.14) * rate);
assert.ok((start + frames) * align <= pcm.length);
const output = Buffer.alloc(44 + frames * align);
output.write('RIFF', 0);
output.writeUInt32LE(output.length - 8, 4);
output.write('WAVEfmt ', 8);
output.writeUInt32LE(16, 16);
format.copy(output, 20, 0, 16);
output.write('data', 36);
output.writeUInt32LE(frames * align, 40);
// Keep sample rate and sample spacing unchanged: trim only, without time stretching.
const fadeIn = Math.round(0.003 * rate);
const fadeOut = Math.round(0.03 * rate);
for (let frame = 0; frame < frames; frame++) {
  const envelope = Math.min(1, frame / fadeIn, (frames - 1 - frame) / fadeOut);
  for (let channel = 0; channel < channels; channel++) {
    const sample = pcm.readInt16LE((start + frame) * align + channel * 2);
    output.writeInt16LE(Math.round(sample * envelope), 44 + frame * align + channel * 2);
  }
}
assert.equal(output.readInt16LE(44), 0);
assert.equal(output.readInt16LE(output.length - 2), 0);
fs.writeFileSync(path.join(audioDir, 'PlateChange.wav'), output);
console.log(JSON.stringify({ file: 'PlateChange.wav', start: 0.14, end: 0.95,
  seconds: frames / rate, rate, channels, playbackSpeed: 1 }));
