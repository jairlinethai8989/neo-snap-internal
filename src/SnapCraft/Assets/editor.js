const canvas = document.querySelector('#canvas');
const ctx = canvas.getContext('2d');
const stage = document.querySelector('#stage');
const colorInput = document.querySelector('#colorInput');
const sizeInput = document.querySelector('#sizeInput');
const blurInput = document.querySelector('#blurInput');
let objects = [];
let selected = -1;
let activeTool = 'select';
let gesture = null;
let baseImage = null;
let textEditor = null;
let cropRect = null;
let cropDraft = null;
let cropPointer = null;
let cropScrollFrame = 0;
const undoStack = [];
const redoStack = [];

function showToast(message) {
  const toast = document.querySelector('#toast');
  toast.textContent = message;
  toast.classList.add('show');
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => toast.classList.remove('show'), 2200);
}

function checkpoint() {
  undoStack.push(JSON.stringify({ objects, cropRect }));
  if (undoStack.length > 80) undoStack.shift();
  redoStack.length = 0;
}

function loadImage(src) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('อ่านภาพไม่ได้'));
    image.src = src;
  });
}

function textMetrics(item) {
  const fontSize = Math.max(12, item.size * 4);
  const lineHeight = fontSize * 1.25;
  ctx.font = `600 ${fontSize}px SnapSans, sans-serif`;
  const lines = (item.text || '').split('\n');
  return { lines, fontSize, lineHeight, width: Math.max(12, ...lines.map((line) => ctx.measureText(line || ' ').width)), height: Math.max(lineHeight, lines.length * lineHeight) };
}

function bounds(item) {
  if (item.tool === 'text') {
    const metrics = textMetrics(item);
    return { x: item.x1, y: item.y1, width: metrics.width, height: metrics.height };
  }
  if (item.tool === 'pen') {
    const xs = item.points.map((point) => point.x);
    const ys = item.points.map((point) => point.y);
    return { x: Math.min(...xs), y: Math.min(...ys), width: Math.max(1, Math.max(...xs) - Math.min(...xs)), height: Math.max(1, Math.max(...ys) - Math.min(...ys)) };
  }
  return { x: Math.min(item.x1, item.x2), y: Math.min(item.y1, item.y2), width: Math.max(1, Math.abs(item.x2 - item.x1)), height: Math.max(1, Math.abs(item.y2 - item.y1)) };
}

function drawPath(item) {
  ctx.beginPath();
  if (item.points.length === 1) {
    ctx.arc(item.points[0].x, item.points[0].y, item.size / 2, 0, Math.PI * 2);
    ctx.fill();
    return;
  }
  ctx.moveTo(item.points[0].x, item.points[0].y);
  for (let index = 1; index < item.points.length - 1; index += 1) {
    const current = item.points[index];
    const next = item.points[index + 1];
    ctx.quadraticCurveTo(current.x, current.y, (current.x + next.x) / 2, (current.y + next.y) / 2);
  }
  const last = item.points.at(-1);
  ctx.lineTo(last.x, last.y);
  ctx.stroke();
}

function drawItem(item) {
  ctx.save();
  ctx.strokeStyle = item.color;
  ctx.fillStyle = item.color;
  ctx.lineWidth = item.size;
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  const box = bounds(item);
  if (item.tool === 'pen') drawPath(item);
  else if (item.tool === 'line') { ctx.beginPath(); ctx.moveTo(item.x1, item.y1); ctx.lineTo(item.x2, item.y2); ctx.stroke(); }
  else if (item.tool === 'arrow') {
    const angle = Math.atan2(item.y2 - item.y1, item.x2 - item.x1);
    const head = item.size * 3 + 10;
    ctx.beginPath(); ctx.moveTo(item.x1, item.y1); ctx.lineTo(item.x2, item.y2);
    ctx.moveTo(item.x2, item.y2); ctx.lineTo(item.x2 - head * Math.cos(angle - Math.PI / 6), item.y2 - head * Math.sin(angle - Math.PI / 6));
    ctx.moveTo(item.x2, item.y2); ctx.lineTo(item.x2 - head * Math.cos(angle + Math.PI / 6), item.y2 - head * Math.sin(angle + Math.PI / 6)); ctx.stroke();
  } else if (item.tool === 'box') ctx.strokeRect(box.x, box.y, box.width, box.height);
  else if (item.tool === 'block') ctx.fillRect(box.x, box.y, box.width, box.height);
  else if (item.tool === 'blur') {
    const padding = item.blur * 3;
    const x = Math.max(0, Math.floor(box.x - padding - cropRect.x));
    const y = Math.max(0, Math.floor(box.y - padding - cropRect.y));
    const width = Math.min(canvas.width - x, Math.ceil(box.width + padding * 2));
    const height = Math.min(canvas.height - y, Math.ceil(box.height + padding * 2));
    if (width > 0 && height > 0) {
      const sample = document.createElement('canvas'); sample.width = width; sample.height = height;
      sample.getContext('2d').drawImage(canvas, x, y, width, height, 0, 0, width, height);
      ctx.beginPath(); ctx.rect(box.x, box.y, box.width, box.height); ctx.clip(); ctx.filter = `blur(${item.blur}px)`; ctx.drawImage(sample, x + cropRect.x, y + cropRect.y);
    }
  } else if (item.tool === 'text') {
    const metrics = textMetrics(item); ctx.font = `600 ${metrics.fontSize}px SnapSans, sans-serif`; ctx.textBaseline = 'top';
    metrics.lines.forEach((line, index) => ctx.fillText(line, item.x1, item.y1 + index * metrics.lineHeight));
  }
  ctx.restore();
}

function render(showSelection = true) {
  if (!baseImage) return;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.drawImage(baseImage, cropRect.x, cropRect.y, cropRect.width, cropRect.height, 0, 0, canvas.width, canvas.height);
  ctx.save();
  ctx.translate(-cropRect.x, -cropRect.y);
  objects.forEach(drawItem);
  if (showSelection && objects[selected] && !textEditor) {
    const box = bounds(objects[selected]);
    const scale = canvas.width / canvas.getBoundingClientRect().width;
    ctx.save(); ctx.strokeStyle = '#0b8b6d'; ctx.lineWidth = 1.5 * scale; ctx.setLineDash([6 * scale, 4 * scale]);
    ctx.strokeRect(box.x - 5 * scale, box.y - 5 * scale, box.width + 10 * scale, box.height + 10 * scale); ctx.setLineDash([]);
    ctx.fillStyle = '#fff'; ctx.fillRect(box.x + box.width - 6 * scale, box.y + box.height - 6 * scale, 12 * scale, 12 * scale); ctx.strokeRect(box.x + box.width - 6 * scale, box.y + box.height - 6 * scale, 12 * scale, 12 * scale); ctx.restore();
  }
  ctx.restore();
  if (showSelection && cropDraft) {
    const x = cropDraft.x - cropRect.x, y = cropDraft.y - cropRect.y;
    const right = x + cropDraft.width, bottom = y + cropDraft.height;
    ctx.save(); ctx.fillStyle = '#081b14aa';
    ctx.fillRect(0, 0, canvas.width, y); ctx.fillRect(0, bottom, canvas.width, canvas.height - bottom);
    ctx.fillRect(0, y, x, cropDraft.height); ctx.fillRect(right, y, canvas.width - right, cropDraft.height);
    ctx.strokeStyle = '#72e7bd'; ctx.lineWidth = Math.max(2, canvas.width / canvas.getBoundingClientRect().width * 2);
    ctx.strokeRect(x, y, cropDraft.width, cropDraft.height); ctx.restore();
  }
}

function canvasPoint(event) {
  const rect = canvas.getBoundingClientRect();
  return { x: cropRect.x + (event.clientX - rect.left) * canvas.width / rect.width, y: cropRect.y + (event.clientY - rect.top) * canvas.height / rect.height };
}

function updateCropDraft() {
  if (gesture?.mode !== 'crop' || !cropPointer) return;
  const point = canvasPoint(cropPointer);
  const x = Math.max(cropRect.x, Math.min(cropRect.x + cropRect.width, point.x));
  const y = Math.max(cropRect.y, Math.min(cropRect.y + cropRect.height, point.y));
  cropDraft = { x: Math.min(gesture.start.x, x), y: Math.min(gesture.start.y, y), width: Math.abs(x - gesture.start.x), height: Math.abs(y - gesture.start.y) };
  render();
}

function stopCropScroll() {
  if (cropScrollFrame) cancelAnimationFrame(cropScrollFrame);
  cropScrollFrame = 0;
  cropPointer = null;
}

function scrollWhileCropping() {
  if (gesture?.mode !== 'crop' || !cropPointer) { stopCropScroll(); return; }
  const box = stage.getBoundingClientRect();
  const edge = 56;
  const upper = Math.max(0, box.top + edge - cropPointer.clientY);
  const lower = Math.max(0, cropPointer.clientY - (box.bottom - edge));
  const distance = lower - upper;
  if (distance) {
    stage.scrollTop += Math.sign(distance) * Math.min(30, Math.max(3, Math.abs(distance) * 0.5));
  }
  cropScrollFrame = requestAnimationFrame(scrollWhileCropping);
}

function setTool(tool) {
  if (tool !== 'crop' && gesture?.mode === 'crop') { gesture = null; stopCropScroll(); }
  if (tool !== 'crop') cropDraft = null;
  activeTool = tool;
  if (tool !== 'select') selected = -1;
  document.querySelectorAll('[data-tool]').forEach((button) => button.classList.toggle('active', button.dataset.tool === tool));
  document.querySelector('#cropActions').hidden = tool !== 'crop';
  canvas.style.cursor = tool === 'select' ? 'default' : tool === 'text' ? 'text' : 'crosshair';
  render();
}

function segmentDistance(point, a, b) {
  const dx = b.x - a.x, dy = b.y - a.y;
  const t = Math.max(0, Math.min(1, ((point.x - a.x) * dx + (point.y - a.y) * dy) / (dx * dx + dy * dy || 1)));
  return Math.hypot(point.x - a.x - t * dx, point.y - a.y - t * dy);
}

function hitTest(point, item) {
  const tolerance = Math.max(10, item.size * 1.5);
  if (item.tool === 'pen') return item.points.some((p, index) => index && segmentDistance(point, item.points[index - 1], p) <= tolerance);
  if (item.tool === 'line' || item.tool === 'arrow') return segmentDistance(point, { x: item.x1, y: item.y1 }, { x: item.x2, y: item.y2 }) <= tolerance;
  const box = bounds(item);
  return point.x >= box.x - tolerance && point.x <= box.x + box.width + tolerance && point.y >= box.y - tolerance && point.y <= box.y + box.height + tolerance;
}

function moveItem(item, original, dx, dy) {
  if (item.tool === 'pen') item.points = original.points.map((point) => ({ x: point.x + dx, y: point.y + dy }));
  else { item.x1 = original.x1 + dx; item.y1 = original.y1 + dy; if ('x2' in original) { item.x2 = original.x2 + dx; item.y2 = original.y2 + dy; } }
}

function resizeItem(item, original, point) {
  const box = bounds(original);
  const scale = Math.max(0.05, Math.max((point.x - box.x) / box.width, (point.y - box.y) / box.height));
  if (item.tool === 'pen') item.points = original.points.map((p) => ({ x: box.x + (p.x - box.x) * scale, y: box.y + (p.y - box.y) * scale }));
  else if (item.tool === 'text') item.size = Math.max(2, Math.min(64, original.size * scale));
  else { item.x1 = box.x + (original.x1 - box.x) * scale; item.y1 = box.y + (original.y1 - box.y) * scale; item.x2 = box.x + (original.x2 - box.x) * scale; item.y2 = box.y + (original.y2 - box.y) * scale; }
}

function openTextEditor(point, event, editIndex = -1) {
  textEditor?.remove();
  const existing = objects[editIndex];
  const textarea = document.createElement('textarea');
  textarea.className = 'canvas-text-editor'; textarea.placeholder = 'พิมพ์ข้อความ…'; textarea.value = existing?.text || '';
  const scale = canvas.getBoundingClientRect().width / canvas.width;
  textarea.style.left = `${Math.min(innerWidth - 280, Math.max(12, event.clientX))}px`;
  textarea.style.top = `${Math.min(innerHeight - 150, Math.max(12, event.clientY))}px`;
  textarea.style.color = existing?.color || colorInput.value;
  textarea.style.fontSize = `${Math.max(16, (existing?.size || Number(sizeInput.value)) * 4 * scale)}px`;
  document.body.append(textarea); textEditor = textarea;
  // Focus after the canvas pointer sequence finishes so pointerup cannot blur it immediately.
  setTimeout(() => { if (textEditor === textarea) { textarea.focus(); textarea.select(); } }, 0);
  let finished = false;
  const finish = (cancel = false) => {
    if (finished) return; finished = true;
    const value = textarea.value.trim(); textarea.remove(); textEditor = null;
    if (!cancel && value) {
      checkpoint();
      if (existing) existing.text = value;
      else { objects.push({ tool: 'text', x1: point.x, y1: point.y, text: value, color: colorInput.value, size: Number(sizeInput.value), blur: Number(blurInput.value) }); selected = objects.length - 1; }
    }
    setTool('select'); render();
  };
  textarea.addEventListener('keydown', (keyEvent) => { if (keyEvent.key === 'Escape') finish(true); if (keyEvent.key === 'Enter' && (keyEvent.ctrlKey || keyEvent.metaKey)) finish(false); });
  textarea.addEventListener('blur', () => finish(false));
}

canvas.addEventListener('pointerdown', (event) => {
  if (!baseImage || event.button !== 0 || textEditor) return;
  const point = canvasPoint(event);
  if (activeTool === 'text') { openTextEditor(point, event); return; }
  canvas.setPointerCapture(event.pointerId);
  if (activeTool === 'crop') {
    cropDraft = { x: point.x, y: point.y, width: 0, height: 0 };
    cropPointer = { clientX: event.clientX, clientY: event.clientY };
    gesture = { mode: 'crop', start: point };
    cropScrollFrame = requestAnimationFrame(scrollWhileCropping);
    render(); return;
  }
  if (activeTool === 'select') {
    const currentBox = objects[selected] && bounds(objects[selected]);
    const scale = canvas.width / canvas.getBoundingClientRect().width;
    const resize = currentBox && Math.hypot(point.x - currentBox.x - currentBox.width, point.y - currentBox.y - currentBox.height) < 14 * scale;
    if (!resize) selected = objects.findLastIndex((item) => hitTest(point, item));
    if (selected >= 0) { checkpoint(); gesture = { mode: resize ? 'resize' : 'move', start: point, original: structuredClone(objects[selected]) }; syncControls(); }
    render(); return;
  }
  checkpoint();
  const common = { tool: activeTool, color: colorInput.value, size: Number(sizeInput.value), blur: Number(blurInput.value) };
  const item = activeTool === 'pen' ? { ...common, points: [point] } : { ...common, x1: point.x, y1: point.y, x2: point.x, y2: point.y };
  objects.push(item); selected = objects.length - 1; gesture = { mode: 'draw', start: point }; render();
});

canvas.addEventListener('pointermove', (event) => {
  if (!gesture) return;
  const point = canvasPoint(event);
  if (gesture.mode === 'crop') {
    cropPointer = { clientX: event.clientX, clientY: event.clientY };
    updateCropDraft(); return;
  }
  const item = objects[selected];
  if (gesture.mode === 'draw' && item.tool === 'pen') {
    const last = item.points.at(-1); if (Math.hypot(point.x - last.x, point.y - last.y) > 1.5) item.points.push(point);
  } else if (gesture.mode === 'draw') { item.x2 = point.x; item.y2 = point.y; }
  else if (gesture.mode === 'move') {
    const box = bounds(gesture.original); const dx = Math.max(cropRect.x - box.x, Math.min(cropRect.x + cropRect.width - box.x - box.width, point.x - gesture.start.x)); const dy = Math.max(cropRect.y - box.y, Math.min(cropRect.y + cropRect.height - box.y - box.height, point.y - gesture.start.y)); moveItem(item, gesture.original, dx, dy);
  } else resizeItem(item, gesture.original, point);
  render();
});

function endGesture() { if (!gesture) return; const mode = gesture.mode; gesture = null; if (mode === 'crop') stopCropScroll(); else setTool('select'); syncControls(); }
canvas.addEventListener('pointerup', endGesture); canvas.addEventListener('pointercancel', endGesture);
stage.addEventListener('scroll', () => { if (gesture?.mode === 'crop') updateCropDraft(); });
canvas.addEventListener('dblclick', (event) => { const point = canvasPoint(event); const index = objects.findLastIndex((item) => item.tool === 'text' && hitTest(point, item)); if (index >= 0) { selected = index; openTextEditor(point, event, index); } });

function syncControls() {
  const item = objects[selected];
  if (item) { colorInput.value = item.color; sizeInput.value = Math.round(item.size); blurInput.value = item.blur || 16; }
  document.querySelector('#colorValue').textContent = colorInput.value.toUpperCase();
  document.querySelector('#sizeValue').textContent = `${sizeInput.value} px`;
  document.querySelector('#blurValue').textContent = `${blurInput.value} px`;
}

document.querySelectorAll('[data-tool]').forEach((button) => button.onclick = () => setTool(button.dataset.tool));
for (const [input, property] of [[colorInput, 'color'], [sizeInput, 'size'], [blurInput, 'blur']]) {
  input.addEventListener('change', () => { const item = objects[selected]; if (item) { checkpoint(); item[property] = property === 'color' ? input.value : Number(input.value); } syncControls(); render(); });
  input.addEventListener('input', syncControls);
}
document.querySelectorAll('[data-color]').forEach((button) => button.onclick = () => { const item = objects[selected]; if (item) { checkpoint(); item.color = button.dataset.color; } colorInput.value = button.dataset.color; syncControls(); render(); });
function removeSelected() { if (selected < 0) return; checkpoint(); objects.splice(selected, 1); selected = -1; render(); }
function history(backward) { const from = backward ? undoStack : redoStack, to = backward ? redoStack : undoStack; if (!from.length) return; to.push(JSON.stringify({ objects, cropRect })); const state = JSON.parse(from.pop()); objects = state.objects; cropRect = state.cropRect; canvas.width = cropRect.width; canvas.height = cropRect.height; selected = -1; cropDraft = null; updateImageInfo(); fitCanvas(); }
function updateImageInfo() { document.querySelector('#imageInfo').textContent = `${canvas.width.toLocaleString()} × ${canvas.height.toLocaleString()} px`; }
document.querySelector('#applyCrop').onclick = () => {
  if (!cropDraft || cropDraft.width < 20 || cropDraft.height < 20) { showToast('ลากกรอบครอบตัดบนภาพก่อน'); return; }
  checkpoint();
  cropRect = { x: Math.round(cropDraft.x), y: Math.round(cropDraft.y), width: Math.round(cropDraft.width), height: Math.round(cropDraft.height) };
  canvas.width = cropRect.width; canvas.height = cropRect.height;
  updateImageInfo(); setTool('select'); fitCanvas();
};
document.querySelector('#cancelCrop').onclick = () => setTool('select');
document.querySelector('#deleteButton').onclick = removeSelected;
document.querySelector('#undoButton').onclick = () => history(true);
document.querySelector('#redoButton').onclick = () => history(false);
document.querySelector('#clearButton').onclick = () => { if (objects.length) { checkpoint(); objects = []; selected = -1; render(); } };
document.addEventListener('keydown', (event) => { if (event.target === textEditor) return; if (event.key === 'Delete' || event.key === 'Backspace') removeSelected(); if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') history(!event.shiftKey); if (event.key === 'Escape') { selected = -1; setTool('select'); } });

async function exportBlob() { render(false); try { return await new Promise((resolve, reject) => canvas.toBlob((blob) => blob ? resolve(blob) : reject(new Error('สร้าง PNG ไม่สำเร็จ')), 'image/png')); } finally { render(); } }
document.querySelector('#downloadButton').onclick = async () => { const url = URL.createObjectURL(await exportBlob()); const link = document.createElement('a'); link.href = url; link.download = `snapcraft-${Date.now()}.png`; link.click(); setTimeout(() => URL.revokeObjectURL(url), 60000); };
document.querySelector('#copyButton').onclick = async () => { try { await navigator.clipboard.write([new ClipboardItem({ 'image/png': exportBlob() })]); showToast('คัดลอกภาพแล้ว'); } catch { showToast('คัดลอกไม่ได้ กรุณาบันทึก PNG'); } };
document.querySelector('#homeLink').onclick = (event) => { event.preventDefault(); window.chrome?.webview?.postMessage('showLauncher'); };
function fitCanvas() { if (!baseImage) return; const available = Math.max(100, stage.clientWidth - 48); const scale = Math.min(1, available / canvas.width); canvas.style.width = `${canvas.width * scale}px`; canvas.style.height = `${canvas.height * scale}px`; render(); }
window.addEventListener('resize', fitCanvas);

(async function init() {
  try {
    await document.fonts.ready;
    const imagePath = new URLSearchParams(location.search).get('image');
    const payload = imagePath ? { type: 'image', dataUrl: imagePath } : null;
    if (!payload) throw new Error('ไม่พบภาพที่จับไว้'); if (payload.type === 'error') throw new Error(payload.message);
    let image = await loadImage(payload.type === 'scroll' ? await stitchScrollTiles(payload.tiles, payload.crop, payload.advances) : payload.dataUrl);
    if (payload.crop && payload.type !== 'scroll') {
      const ratio = image.width / payload.crop.viewportWidth; const crop = document.createElement('canvas'); crop.width = Math.round(payload.crop.width * ratio); crop.height = Math.round(payload.crop.height * ratio);
      crop.getContext('2d').drawImage(image, payload.crop.x * ratio, payload.crop.y * ratio, crop.width, crop.height, 0, 0, crop.width, crop.height); image = await loadImage(crop.toDataURL('image/png'));
    }
    baseImage = image; cropRect = { x: 0, y: 0, width: image.width, height: image.height }; canvas.width = image.width; canvas.height = image.height; updateImageInfo(); document.querySelector('#loading').hidden = true; fitCanvas(); if (payload.notice) showToast(payload.notice);
  } catch (error) { const loading = document.querySelector('#loading'); loading.innerHTML = ''; const message = document.createElement('p'); message.textContent = error.message; loading.append(message); }
})();

lucide.createIcons();
