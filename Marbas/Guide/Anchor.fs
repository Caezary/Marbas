namespace Marbas.Guide

type Anchor = private Anchor of string

module Anchor =

    let create text = Anchor text

    let value (Anchor text) = text
