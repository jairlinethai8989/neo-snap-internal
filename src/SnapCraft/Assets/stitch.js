async function stitchScrollTiles(tiles, crop, advances) {
  if (!Array.isArray(tiles) || !tiles.length) throw new Error('ไม่มีภาพพื้นที่เลื่อน');
  const load = (src) => new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('อ่านภาพพื้นที่เลื่อนไม่ได้'));
    image.src = src;
  });
  const first = await load(tiles[0]);
  const ratioX = first.width / crop.viewportWidth;
  const ratioY = first.height / crop.viewportHeight;
  const x = crop.preCropped ? 0 : Math.round(crop.x * ratioX);
  const y = crop.preCropped ? 0 : Math.round(crop.y * ratioY);
  const width = crop.preCropped ? first.width : Math.max(1, Math.min(first.width - x, Math.round(crop.width * ratioX)));
  const height = crop.preCropped ? first.height : Math.max(1, Math.min(first.height - y, Math.round(crop.height * ratioY)));
  if (width < 20 || height < 20) throw new Error('พื้นที่ที่เลือกเล็กเกินไป');

  function extract(image) {
    if (image.width !== first.width || image.height !== first.height) throw new Error('ขนาดภาพเปลี่ยนระหว่างจับภาพ กรุณาจับใหม่');
    const surface = document.createElement('canvas');
    surface.width = width; surface.height = height;
    const context = surface.getContext('2d', { willReadFrequently: true });
    context.drawImage(image, x, y, width, height, 0, 0, width, height);
    const pixels = context.getImageData(0, 0, width, height).data;
    const signatures = new Uint32Array(height);
    const step = Math.max(5, Math.floor(width / 72));
    for (let row = 0; row < height; row += 1) {
      let hash = 2166136261;
      for (let column = 4; column < width - 4; column += step) {
        const at = (row * width + column) * 4;
        hash = Math.imul(hash ^ pixels[at], 16777619);
        hash = Math.imul(hash ^ pixels[at + 1], 16777619);
        hash = Math.imul(hash ^ pixels[at + 2], 16777619);
      }
      signatures[row] = hash >>> 0;
    }
    return { surface, signatures };
  }

  function similarity(previous, current, shift) {
    const overlap = height - shift;
    let equal = 0, checks = 0;
    for (let row = 3; row < overlap - 3; row += Math.max(3, Math.floor(overlap / 60))) {
      checks += 1;
      if (previous[row + shift] === current[row]) equal += 1;
    }
    return checks ? equal / checks : 0;
  }

  function findShift(previous, current) {
    if (similarity(previous, current, 0) > 0.96) return 0;
    const expected = Math.round(height * 0.72);
    const positions = new Map();
    const limit = Math.floor(height * 0.9);
    for (let row = 1; row < height; row += 1) {
      const hash = previous[row];
      if (!positions.has(hash)) positions.set(hash, []);
      positions.get(hash).push(row);
    }
    const votes = new Map();
    for (let row = 1; row < Math.min(100, Math.floor(height * 0.35)); row += 3) {
      for (const match of positions.get(current[row]) || []) {
        const shift = match - row;
        if (shift > 0 && shift <= limit) votes.set(shift, (votes.get(shift) || 0) + 1);
      }
    }
    const candidates = [...votes.entries()].sort((a, b) => b[1] - a[1]).slice(0, 20);
    let best = { shift: expected, score: 0 };
    for (const [shift] of candidates) {
      const score = similarity(previous, current, shift);
      if (score > best.score || (score === best.score && Math.abs(shift - expected) < Math.abs(best.shift - expected))) best = { shift, score };
    }
    return best.score >= 0.72 ? best.shift : expected;
  }

  const parts = [];
  let previous = extract(first);
  parts.push({ surface: previous.surface, from: 0, height });
  let totalHeight = height;
  for (let index = 1; index < tiles.length; index += 1) {
    const current = extract(await load(tiles[index]));
    const shift = advances ? advances[index] : findShift(previous.signatures, current.signatures);
    if (!Number.isInteger(shift) || shift < 0 || shift >= height) throw new Error('ระยะต่อภาพไม่ถูกต้อง กรุณาจับภาพใหม่');
    if (shift === 0) break;
    if (totalHeight + shift > 30000 || width * (totalHeight + shift) > 100000000) break;
    parts.push({ surface: current.surface, from: height - shift, height: shift });
    totalHeight += shift;
    previous = current;
    if (index % 3 === 0) await new Promise((resolve) => setTimeout(resolve, 0));
  }
  const output = document.createElement('canvas');
  output.width = width; output.height = totalHeight;
  const result = output.getContext('2d');
  let offset = 0;
  for (const part of parts) {
    result.drawImage(part.surface, 0, part.from, width, part.height, 0, offset, width, part.height);
    offset += part.height;
  }
  return output.toDataURL('image/png');
}
