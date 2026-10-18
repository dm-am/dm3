// Shared file size formatting (SSOT for byte counts shown in the UI)

/** One decimal place with the Russian decimal comma: "15,2", not "15.2". */
const oneDecimal = (value: number) => value.toFixed(1).replace(".", ",");

/**
 * Format a byte count as a human-readable size: "512 Б", "12,3 КБ", "1,5 МБ".
 * Mirrors the format used across upload tables site-wide.
 */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} Б`;
  if (bytes < 1024 * 1024) return `${oneDecimal(bytes / 1024)} КБ`;
  return `${oneDecimal(bytes / (1024 * 1024))} МБ`;
}
