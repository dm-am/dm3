/**
 * The character sheet on the wire.
 *
 * A specification and its possible values are the same shape wherever a sheet
 * is served: the schema of a game, and the template a game starts from. They
 * live here rather than in either slice because both read them, and a copy in
 * each is how the two would come to disagree.
 */

/**
 * Constraints a specification puts on its value.
 * @see src/DM.Domain.Core/Enums/AttributeSpecificationType.cs
 */
export enum AttributeSpecificationType {
  Text = "Text",
  Number = "Number",
  TextList = "TextList",
  NumberList = "NumberList",
  TextNumberList = "TextNumberList",
  BbCode = "BbCode",
}

export interface AttributeValueSpecification {
  value: string;
  modifier: number | null;
}

export interface AttributeSpecification {
  id: string;
  title: string;
  required: boolean;
  type: AttributeSpecificationType;
  /** Order index within the schema */
  order: number;
  /** Show on game main page as descriptor (at most one per schema) */
  isDescriptor: boolean;
  /** Hide from other players (only GM and owner can see) */
  isHidden: boolean;
  /** Maximal length - only for Text/Number/BbCode types */
  maxLength?: number | null;
  /**
   * Possible values, empty for every type but TextList/NumberList/TextNumberList.
   *
   * An array always, never null: the server declares the field required and
   * refuses a null with a validation error, so a draft that means "no values"
   * carries an empty array.
   */
  values: AttributeValueSpecification[];
}

/**
 * The character sheet of one game, as its details answer serves it.
 *
 * The version is a number without meaning: the client compares it to nothing
 * and only hands back the one it was given, so a replacement written against a
 * sheet somebody else has already changed is refused instead of overwriting it.
 */
export interface GameAttributeSchema {
  specifications: AttributeSpecification[];
  version: number;
}

/**
 * Replacement of a game's whole sheet. Partial edits have no meaning here: the
 * set that arrives is the set the game keeps, and the values of every
 * specification it drops go with them.
 */
export interface ReplaceGameAttributeSchemaRequest {
  specifications: AttributeSpecification[];
  /** The version the sheet was read at; the server refuses a body without one. */
  version: number;
}
