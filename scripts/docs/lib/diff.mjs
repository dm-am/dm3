// A line diff of the committed document against the counted one, for --check to
// print: "-" what the file says and the count no longer does, "+" the reverse.
// Longest common subsequence over lines; the document is a few hundred lines.

export function lineDiff(before, after) {
  const a = before.split("\n");
  const b = after.split("\n");
  const width = b.length + 1;
  const table = new Uint32Array((a.length + 1) * width);
  for (let i = a.length - 1; i >= 0; i--) {
    for (let j = b.length - 1; j >= 0; j--) {
      table[i * width + j] =
        a[i] === b[j]
          ? table[(i + 1) * width + j + 1] + 1
          : Math.max(table[(i + 1) * width + j], table[i * width + j + 1]);
    }
  }
  const lines = [];
  let i = 0;
  let j = 0;
  while (i < a.length || j < b.length) {
    if (i < a.length && j < b.length && a[i] === b[j]) {
      i++;
      j++;
    } else if (j < b.length && (i === a.length || table[i * width + j + 1] > table[(i + 1) * width + j])) {
      lines.push(`+${b[j++]}`);
    } else {
      lines.push(`-${a[i++]}`);
    }
  }
  return lines;
}
