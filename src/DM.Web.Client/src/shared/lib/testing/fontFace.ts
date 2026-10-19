/**
 * The width of a line of text in a shipped face without a browser: advances
 * from hmtx through cmap, with the GPOS kern feature (PT Sans has no kern
 * table). Every user calibrates it against a width measured in Chrome.
 * Spec-only.
 */
import { readFileSync } from "node:fs";

export interface Face {
  unitsPerEm: number;
  width(text: string, size: number, tracking: number): number;
}

type Tables = Record<string, { off: number; len: number }>;

/** A class-based pair subtable of GPOS, read far enough to look a pair up. */
interface Classed {
  covered: Set<number>;
  first: Map<number, number>;
  second: Map<number, number>;
  rows: number;
  cols: number;
  record: number;
  base: number;
  format: number;
}

/** The kern feature of GPOS: explicit pairs and the class-based subtables. */
interface Kerning {
  pairs: Map<string, number>;
  classed: Classed[];
}

/** The table directory: where each table of the face starts, and its length. */
function tablesOf(b: Buffer): Tables {
  const tables: Tables = {};
  const count = b.readUInt16BE(4);
  for (let i = 0; i < count; i++) {
    const o = 12 + 16 * i;
    tables[b.toString("ascii", o, o + 4)] = {
      off: b.readUInt32BE(o + 8),
      len: b.readUInt32BE(o + 12),
    };
  }
  return tables;
}

/** The glyph of each character, from `cmap`. */
function glyphsOf(
  b: Buffer,
  tables: Tables,
  file: string,
): Map<number, number> {
  // cmap: the Windows BMP subtable, format 4, is the one every text here needs.
  const cmap = tables.cmap.off;
  const subtables = b.readUInt16BE(cmap + 2);
  let chosen = -1;
  for (let i = 0; i < subtables; i++) {
    const o = cmap + 4 + 8 * i;
    const platform = b.readUInt16BE(o);
    const encoding = b.readUInt16BE(o + 2);
    const off = cmap + b.readUInt32BE(o + 4);
    if (platform === 3 && encoding === 1 && b.readUInt16BE(off) === 4)
      chosen = off;
  }
  if (chosen < 0) throw new Error("no format 4 cmap in " + file);

  const glyphs = new Map<number, number>();
  {
    const t = chosen;
    const segX2 = b.readUInt16BE(t + 6);
    const segments = segX2 / 2;
    const endOff = t + 14;
    const startOff = endOff + segX2 + 2;
    const deltaOff = startOff + segX2;
    const rangeOff = deltaOff + segX2;
    for (let s = 0; s < segments; s++) {
      const end = b.readUInt16BE(endOff + 2 * s);
      const start = b.readUInt16BE(startOff + 2 * s);
      const delta = b.readInt16BE(deltaOff + 2 * s);
      const range = b.readUInt16BE(rangeOff + 2 * s);
      for (let c = start; c <= end && c !== 0xffff; c++) {
        let g: number;
        if (range === 0) g = (c + delta) & 0xffff;
        else {
          const at = rangeOff + 2 * s + range + 2 * (c - start);
          if (at + 1 >= b.length) continue;
          g = b.readUInt16BE(at);
          if (g) g = (g + delta) & 0xffff;
        }
        if (g) glyphs.set(c, g);
      }
    }
  }
  return glyphs;
}

const coverage = (b: Buffer, o: number) => {
  const map = new Map<number, number>();
  const format = b.readUInt16BE(o);
  const n = b.readUInt16BE(o + 2);
  if (format === 1) {
    for (let i = 0; i < n; i++) map.set(b.readUInt16BE(o + 4 + 2 * i), i);
  } else {
    for (let i = 0; i < n; i++) {
      const r = o + 4 + 6 * i;
      const start = b.readUInt16BE(r);
      const end = b.readUInt16BE(r + 2);
      const index = b.readUInt16BE(r + 4);
      for (let c = start; c <= end; c++) map.set(c, index + (c - start));
    }
  }
  return map;
};
const classDef = (b: Buffer, o: number) => {
  const map = new Map<number, number>();
  const format = b.readUInt16BE(o);
  if (format === 1) {
    const start = b.readUInt16BE(o + 2);
    const n = b.readUInt16BE(o + 4);
    for (let i = 0; i < n; i++)
      map.set(start + i, b.readUInt16BE(o + 6 + 2 * i));
  } else {
    const n = b.readUInt16BE(o + 2);
    for (let i = 0; i < n; i++) {
      const r = o + 4 + 6 * i;
      const start = b.readUInt16BE(r);
      const end = b.readUInt16BE(r + 2);
      const cls = b.readUInt16BE(r + 4);
      for (let c = start; c <= end; c++) map.set(c, cls);
    }
  }
  return map;
};
const valueSize = (format: number) => {
  let n = 0;
  for (let i = 0; i < 8; i++) if (format & (1 << i)) n++;
  return n * 2;
};
const xAdvance = (b: Buffer, o: number, format: number) => {
  if (!(format & 0x0004)) return 0;
  let at = 0;
  if (format & 0x0001) at += 2;
  if (format & 0x0002) at += 2;
  return b.readInt16BE(o + at);
};

/** GPOS, the kern feature only: explicit pairs and the class-based subtable. */
function kerningOf(b: Buffer, g: number): Kerning {
  const pairs = new Map<string, number>();
  const classed: Classed[] = [];

  const featureList = g + b.readUInt16BE(g + 6);
  const lookupList = g + b.readUInt16BE(g + 8);
  const wanted = new Set<number>();
  const features = b.readUInt16BE(featureList);
  for (let i = 0; i < features; i++) {
    const o = featureList + 2 + 6 * i;
    if (b.toString("ascii", o, o + 4) !== "kern") continue;
    const feature = featureList + b.readUInt16BE(o + 4);
    const lookups = b.readUInt16BE(feature + 2);
    for (let j = 0; j < lookups; j++)
      wanted.add(b.readUInt16BE(feature + 4 + 2 * j));
  }

  const lookups = b.readUInt16BE(lookupList);
  for (let i = 0; i < lookups; i++) {
    if (!wanted.has(i)) continue;
    const lookup = lookupList + b.readUInt16BE(lookupList + 2 + 2 * i);
    const declared = b.readUInt16BE(lookup);
    const tableCount = b.readUInt16BE(lookup + 4);
    for (let j = 0; j < tableCount; j++) {
      let at = lookup + b.readUInt16BE(lookup + 6 + 2 * j);
      let type = declared;
      if (type === 9) {
        type = b.readUInt16BE(at + 2);
        at = at + b.readUInt32BE(at + 4);
      }
      if (type !== 2) continue;
      const format = b.readUInt16BE(at);
      const first = b.readUInt16BE(at + 4);
      const second = b.readUInt16BE(at + 6);
      if (format === 1) {
        const covered = coverage(b, at + b.readUInt16BE(at + 2));
        const n = b.readUInt16BE(at + 8);
        const s1 = valueSize(first);
        const s2 = valueSize(second);
        const back = new Map<number, number>();
        for (const [glyph, index] of covered) back.set(index, glyph);
        for (let k = 0; k < n; k++) {
          const set = at + b.readUInt16BE(at + 10 + 2 * k);
          const values = b.readUInt16BE(set);
          const left = back.get(k);
          for (let q = 0; q < values; q++) {
            const r = set + 2 + q * (2 + s1 + s2);
            const right = b.readUInt16BE(r);
            const x = xAdvance(b, r + 2, first);
            if (x) pairs.set(left + "/" + right, x);
          }
        }
      } else if (format === 2) {
        classed.push({
          covered: new Set(coverage(b, at + b.readUInt16BE(at + 2)).keys()),
          first: classDef(b, at + b.readUInt16BE(at + 8)),
          second: classDef(b, at + b.readUInt16BE(at + 10)),
          rows: b.readUInt16BE(at + 12),
          cols: b.readUInt16BE(at + 14),
          record: valueSize(first) + valueSize(second),
          base: at + 16,
          format: first,
        });
      }
    }
  }
  return { pairs, classed };
}

/** The kerning of a pair of glyphs, from what kerningOf read. */
function kernFrom(
  b: Buffer,
  { pairs, classed }: Kerning,
  left: number,
  right: number,
): number {
  const direct = pairs.get(left + "/" + right);
  if (direct) return direct;
  for (const t of classed) {
    if (!t.covered.has(left)) continue;
    const a = t.first.get(left) ?? 0;
    const c = t.second.get(right) ?? 0;
    if (a >= t.rows || c >= t.cols) continue;
    const at = t.base + (a * t.cols + c) * t.record;
    if (!(t.format & 0x0004)) return 0;
    let off = 0;
    if (t.format & 0x0001) off += 2;
    if (t.format & 0x0002) off += 2;
    const x = b.readInt16BE(at + off);
    if (x) return x;
  }
  return 0;
}

export function loadFace(file: string): Face {
  const b = readFileSync(file);
  const tables = tablesOf(b);
  const unitsPerEm = b.readUInt16BE(tables.head.off + 18);
  const longMetrics = b.readUInt16BE(tables.hhea.off + 34);
  const glyphs = glyphsOf(b, tables, file);

  const hmtx = tables.hmtx.off;
  const advance = (g: number) =>
    b.readUInt16BE(hmtx + 4 * Math.min(g, longMetrics - 1));

  const kerning: Kerning = tables.GPOS
    ? kerningOf(b, tables.GPOS.off)
    : { pairs: new Map(), classed: [] };
  const kern = (left: number, right: number) =>
    kernFrom(b, kerning, left, right);

  return {
    unitsPerEm,
    width(text, size, tracking) {
      const chars = [...text];
      let units = 0;
      let prev: number | null = null;
      for (const ch of chars) {
        const glyph = glyphs.get(ch.codePointAt(0)!);
        if (glyph === undefined) throw new Error("no glyph for " + ch);
        units += advance(glyph);
        if (prev !== null) units += kern(prev, glyph);
        prev = glyph;
      }
      // Tracking lands after every character, the last one included, which is
      // what the engine does with letter-spacing.
      return (units / unitsPerEm) * size + tracking * chars.length;
    },
  };
}
