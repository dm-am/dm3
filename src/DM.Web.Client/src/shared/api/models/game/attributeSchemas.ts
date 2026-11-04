// The character sheet on the wire, read by the schema of a game and by the
// template a game starts from.

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
   * Empty for every type but the lists; never null, which the server refuses.
   */
  values: AttributeValueSpecification[];
}

/**
 * The version is only handed back, so a replacement written against a sheet
 * somebody else changed is refused.
 */
export interface GameAttributeSchema {
  specifications: AttributeSpecification[];
  version: number;
}

/** The whole sheet: values of every specification it drops go with them. */
export interface ReplaceGameAttributeSchemaRequest {
  specifications: AttributeSpecification[];
  /** The version the sheet was read at; the server refuses a body without one. */
  version: number;
}
