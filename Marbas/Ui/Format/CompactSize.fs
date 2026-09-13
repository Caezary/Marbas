module Marbas.Ui.Format.CompactSize

let private units = [| "B"; "KB"; "MB"; "GB"; "TB"; "PB"; "EB" |]

[<Literal>]
let private UnitShift = 10

[<Literal>]
let private UnitStep = 1024UL

let rec private reach (whole: uint64) steps =
    if whole < UnitStep then
        whole, steps
    else
        reach (whole >>> UnitShift) (steps + 1)

let private roundedTenths (bytes: uint64) whole steps =
    let shift = UnitShift * steps
    let remainder = bytes - (whole <<< shift)
    let half = 1UL <<< (shift - 1)
    whole * 10UL + ((remainder * 10UL + half) >>> shift)

let describe (bytes: int64) =
    let exact = uint64 bytes

    match reach exact 0 with
    | whole, 0 -> $"{whole} B"
    | whole, steps ->
        let tenths, unit =
            match roundedTenths exact whole steps with
            | carried when carried = UnitStep * 10UL -> 10UL, steps + 1
            | tenths -> tenths, steps

        $"{tenths / 10UL}.{tenths % 10UL} {units[unit]}"
