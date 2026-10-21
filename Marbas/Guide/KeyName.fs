namespace Marbas.Guide

type KeyName = private KeyName of string

module KeyName =

    let create text = KeyName text

    let value (KeyName text) = text
