namespace Marbas.Domain

type ProductVersion = ProductVersion of string

module ProductVersion =

    let value (ProductVersion text) = text
