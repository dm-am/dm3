// Types of unordered.mjs for the TypeScript side of the stand.
export declare const UNORDERED: {
  route: string;
  reason: string;
  order: (body: any, written: (moment: unknown) => boolean) => void;
}[];
export declare const addressOf: (route: string) => RegExp;
