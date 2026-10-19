// The addresses an image, a link or a [tip] picture is shown from (BbAddress).

import { hasWhiteSpace, trimWhiteSpace } from "./bbcodeSpelling";

/** Why the server would not show an address: the three ways it fails BbAddress.Accept. */
type AddressFault = "scheme" | "malformed" | "local";

/**
 * BbAddress.Accept: http or https, no space or control character, and no host
 * of the reader's own machine or network, since the reader's browser fetches it.
 */
export function isSafeAddress(url: string): boolean {
  return addressFault(url) === null;
}

function addressFault(url: string): AddressFault | null {
  const trimmed = trimWhiteSpace(url);
  const lower = trimmed.toLowerCase();
  if (!lower.startsWith("http://") && !lower.startsWith("https://")) {
    return "scheme";
  }
  if (hasSpaceOrControl(trimmed) || !writtenAsTheServerReadsIt(trimmed)) {
    return "malformed";
  }
  let host: string;
  try {
    host = new URL(trimmed).hostname.toLowerCase();
  } catch {
    return "malformed";
  }
  return isBlockedHost(host) ? "local" : null;
}

/**
 * What an address field of the editor's panels says under an address the
 * server would not show, or null when it would or the field is empty.
 */
export function addressRefusal(value: string): string | null {
  const trimmed = trimWhiteSpace(value);
  if (!trimmed) return null;
  switch (addressFault(trimmed)) {
    case null:
      return null;
    case "scheme":
      return "Адрес начинается с http:// или https://";
    case "malformed":
      return "Адрес записан с ошибкой";
    case "local":
      return "Адрес ведет в локальную сеть, такой сайт не покажет";
  }
}

/**
 * The picture of a [tip], trimmed, or null when the server refuses it
 * (BbAddress.ReadTipImage): a safe address, or a site path with one slash and
 * no backslash, since a browser reads a second slash or a backslash as another
 * host.
 */
export function tipImageAddress(value: string): string | null {
  const trimmed = trimWhiteSpace(value);
  const sitePath =
    trimmed.length > 1 &&
    trimmed[0] === "/" &&
    trimmed[1] !== "/" &&
    !trimmed.includes("\\") &&
    !hasSpaceOrControl(trimmed);
  return sitePath || isSafeAddress(value) ? trimmed : null;
}

/**
 * Whether .NET's Uri takes the address's shape: the browser's URL also reads a
 * backslash before the path, an empty authority, a percent sign in the host
 * and an empty label, each its own way, which the server refuses.
 */
function writtenAsTheServerReadsIt(address: string): boolean {
  const rest = address.slice(address.indexOf("//") + 2);
  const end = rest.search(/[/?#\\]/);
  if (end >= 0 && rest[end] === "\\") return false;
  const authority = end < 0 ? rest : rest.slice(0, end);
  const host = authority.slice(authority.lastIndexOf("@") + 1);
  return host.length > 0 && !host.includes("%") && !host.includes("..");
}

/** A space or a control character (RFC 3986), white space as the server reads it. */
function hasSpaceOrControl(text: string): boolean {
  for (let index = 0; index < text.length; index++) {
    const code = text.charCodeAt(index);
    if (code <= 0x1f || (code >= 0x7f && code <= 0x9f)) return true;
  }
  return hasWhiteSpace(text);
}

/** The loopback, private and link-local ranges, as the server lists them. */
function isBlockedIPv4(octets: number[]): boolean {
  const [a, b] = octets;
  return (
    a === 0 ||
    a === 10 ||
    a === 127 ||
    (a === 100 && b >= 64 && b <= 127) ||
    (a === 169 && b === 254) ||
    (a === 172 && b >= 16 && b <= 31) ||
    (a === 192 && b === 168)
  );
}

/** Eight groups of an IPv6 literal, or null when it is not one. */
function ipv6Groups(literal: string): number[] | null {
  let text = literal;
  const tail: number[] = [];
  const dotted = /(\d+)\.(\d+)\.(\d+)\.(\d+)$/.exec(text);
  if (dotted) {
    const [a, b, c, d] = dotted.slice(1).map(Number);
    tail.push((a << 8) | b, (c << 8) | d);
    text = text.slice(0, dotted.index) + "0";
    text = text.replace(/:0$/, ":");
  }
  const halves = text.split("::");
  if (halves.length > 2) return null;
  const parse = (part: string): number[] | null => {
    if (!part) return [];
    const groups = part.split(":").filter((group) => group !== "");
    const values = groups.map((group) => parseInt(group, 16));
    return values.some((value) => Number.isNaN(value) || value > 0xffff)
      ? null
      : values;
  };
  const head = parse(halves[0]);
  const rest = halves.length === 2 ? parse(halves[1]) : [];
  if (!head || !rest) return null;
  const known = head.length + rest.length + tail.length;
  if (known > 8 || (halves.length === 1 && known !== 8)) return null;
  const zeros = new Array<number>(8 - known).fill(0);
  return [...head, ...zeros, ...rest, ...tail];
}

function isBlockedHost(hostname: string): boolean {
  const host = hostname.replace(/\.$/, "");
  if (host === "localhost" || host.endsWith(".localhost")) return true;

  const v4 = /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/.exec(host);
  if (v4) return isBlockedIPv4(v4.slice(1).map(Number));

  if (!host.startsWith("[") || !host.endsWith("]")) return false;
  const groups = ipv6Groups(host.slice(1, -1));
  if (!groups) return true;
  const [g0, , , , , g5, g6, g7] = groups;
  const allZeroUpTo = (n: number) => groups.slice(0, n).every((g) => g === 0);
  if (allZeroUpTo(5) && g5 === 0xffff) {
    return isBlockedIPv4([g6 >> 8, g6 & 0xff, g7 >> 8, g7 & 0xff]);
  }
  if (allZeroUpTo(7) && (g7 === 0 || g7 === 1)) return true;
  return (
    (g0 & 0xffc0) === 0xfe80 ||
    (g0 & 0xffc0) === 0xfec0 ||
    (g0 & 0xfe00) === 0xfc00
  );
}

/**
 * The address a link may point at, or "#" when the server would refuse it.
 *
 * @example
 * sanitizeUrl("javascript:alert(1)") // => "#"
 * sanitizeUrl(" https://example.com/ ")    // => "https://example.com/"
 */
export function sanitizeUrl(url: string): string {
  return isSafeAddress(url ?? "") ? trimWhiteSpace(url) : "#";
}
