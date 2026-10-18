// Types of the part of api-volatile.mjs the TypeScript side of the stand uses.
export declare const REPEATED: {
  route: string;
  reason: string;
  list: (body: any) => any[] | undefined;
  moment: string;
  key: (row: any) => string;
}[];
export declare function collapseRepeated(
  body: any,
  rule: (typeof REPEATED)[number],
  written: (moment: unknown) => boolean,
): void;
